using RuleEditor.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace RuleEditor.ViewModels.Version2
{
    // Converter to show/hide elements based on string content
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    // Converter to invert boolean values and convert to Visibility
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Enum to track the current state of rule creation
    public enum RuleInputState
    {
        Property,
        Operation,
        Value
    }

    public partial class TokenizerControl : UserControl
    {
        public event EventHandler<TokenEventArgs> TokenAdded;
        public event EventHandler<TokenEventArgs> TokenRemoved;
        public event EventHandler<TokenEventArgs> TokenChanged;
        public event EventHandler<TextChangedEventArgs> TextChanged;

        private List<TokenControl> tokens = new List<TokenControl>();
        private RuleInputState currentState = RuleInputState.Property;
        
        // Lists of valid properties and operations
        private List<string> validProperties = new List<string>
        {
            "Name", "Age", "Email", "IsActive", "Balance", "LastLoginDate"
        };
        
        private List<string> validOperations = new List<string>
        {
            "==", "!=", ">", "<", ">=", "<=", "CONTAINS", "STARTSWITH", "ENDSWITH"
        };
        
        private List<string> logicalOperators = new List<string>
        {
            "AND", "OR", "NOT"
        };

        public TokenizerControl()
        {
            InitializeComponent();
            
            // Initialize the ComboBox with suggestions based on current state
            UpdateComboBoxSuggestions();
        }

        private void UpdateComboBoxSuggestions()
        {
            // Clear existing items
            inputBox.Items.Clear();
            
            // Always allow typing, but we'll validate the input differently based on state
            inputBox.IsEditable = true;
            
            // Add suggestions based on current state
            switch (currentState)
            {
                case RuleInputState.Property:
                    foreach (var property in validProperties)
                    {
                        inputBox.Items.Add(property);
                    }
                    // Allow logical operators if we have at least one complete rule
                    if (HasCompleteRule())
                    {
                        foreach (var op in logicalOperators)
                        {
                            inputBox.Items.Add(op);
                        }
                    }
                    break;
                    
                case RuleInputState.Operation:
                    foreach (var operation in validOperations)
                    {
                        inputBox.Items.Add(operation);
                    }
                    break;
                    
                case RuleInputState.Value:
                    // For values, we don't restrict input, so no suggestions needed
                    // But we could add some common values as suggestions
                    inputBox.Items.Add("true");
                    inputBox.Items.Add("false");
                    inputBox.Items.Add("0");
                    inputBox.Items.Add("100");
                    break;
            }
        }
        
        // Check if we have at least one complete rule (property-operation-value)
        private bool HasCompleteRule()
        {
            return tokens.Count >= 3 && 
                   tokens.Count % 3 == 0; // Complete rules should have tokens in multiples of 3
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrEmpty(GetInputText().Trim()))
                {
                    // Create token from current input
                    CreateTokenFromInput();
                    
                    // Focus the input box again to prepare for next token
                    inputBox.Focus();
                }
                else if (tokens.Count > 0)
                {
                    // If input is empty and we have tokens, focus the first token
                    tokens[0].Focus();
                }
                
                e.Handled = true;
            }
            else if (e.Key == Key.Back && string.IsNullOrEmpty(GetInputText()) && tokens.Count > 0)
            {
                // If backspace is pressed on empty input and we have tokens, remove the last token
                var lastToken = tokens[tokens.Count - 1];
                Token_Removed(lastToken, new TokenRemovedEventArgs(lastToken));
                e.Handled = true;
            }
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Forward the TextChanged event
            TextChanged?.Invoke(this, e);
            
            var text = GetInputText();
            if (text.EndsWith(" ") || text.EndsWith("\n"))
            {
                CreateTokenFromInput();
            }
        }

        private string GetInputText()
        {
            // Get text from the editable ComboBox
            return inputBox.Text;
        }

        private void CreateTokenFromInput()
        {
            var text = GetInputText().Trim();
            if (string.IsNullOrEmpty(text)) return;
            
            // Validate input based on current state
            if (!ValidateInput(text))
            {
                // If invalid, clear the input and don't create a token
                inputBox.Text = string.Empty;
                return;
            }

            var token = new TokenControl
            {
                DataContext = new TokenViewModel
                {
                    Text = text,
                    Type = DetermineTokenType(text),
                    Suggestions = GetSuggestionsForType(text)
                }
            };
            
            token.TokenRemoved += Token_Removed;
            tokens.Add(token);
            tokenPanel.Children.Insert(tokenPanel.Children.Count - 1, token);
            
            // Clear the input box
            inputBox.Text = string.Empty;
            
            // Advance to the next state
            AdvanceState(text);
            
            // Update suggestions for the new state
            UpdateComboBoxSuggestions();
            
            // Notify that a token was added
            TokenAdded?.Invoke(this, new TokenEventArgs(token));
        }
        
        private bool ValidateInput(string text)
        {
            switch (currentState)
            {
                case RuleInputState.Property:
                    // Must be a valid property or logical operator
                    return validProperties.Contains(text) || 
                           (HasCompleteRule() && logicalOperators.Contains(text));
                    
                case RuleInputState.Operation:
                    // Must be a valid operation
                    return validOperations.Contains(text);
                    
                case RuleInputState.Value:
                    // Values can be anything
                    return true;
                    
                default:
                    return false;
            }
        }
        
        private void AdvanceState(string text)
        {
            // If we added a logical operator, stay in Property state
            if (logicalOperators.Contains(text))
            {
                currentState = RuleInputState.Property;
                return;
            }
            
            // Otherwise advance to the next state
            switch (currentState)
            {
                case RuleInputState.Property:
                    currentState = RuleInputState.Operation;
                    break;
                    
                case RuleInputState.Operation:
                    currentState = RuleInputState.Value;
                    break;
                    
                case RuleInputState.Value:
                    // After a value, we go back to property for the next rule
                    currentState = RuleInputState.Property;
                    break;
            }
        }

        private void Token_Removed(object sender, TokenRemovedEventArgs e)
        {
            var token = e.RemovedToken;
            var tokenIndex = tokens.IndexOf(token);
            
            tokenPanel.Children.Remove(token);
            tokens.Remove(token);
            
            // Recalculate the current state based on remaining tokens
            RecalculateState();
            
            // Update suggestions
            UpdateComboBoxSuggestions();
            
            // Notify that a token was removed
            TokenRemoved?.Invoke(this, new TokenEventArgs(token));
        }
        
        private void RecalculateState()
        {
            // If no tokens, we're at the beginning (Property state)
            if (tokens.Count == 0)
            {
                currentState = RuleInputState.Property;
                return;
            }
            
            // Calculate state based on the number of non-logical tokens
            int nonLogicalTokens = tokens.Count(t => !logicalOperators.Contains(t.Text));
            
            switch (nonLogicalTokens % 3)
            {
                case 0:
                    currentState = RuleInputState.Property;
                    break;
                case 1:
                    currentState = RuleInputState.Operation;
                    break;
                case 2:
                    currentState = RuleInputState.Value;
                    break;
            }
        }

        private TokenType DetermineTokenType(string text)
        {
            if (IsOperator(text))
                return TokenType.Operator;
            else if (IsValue(text))
                return TokenType.Value;
            else
                return TokenType.Property;
        }

        private List<string> GetSuggestionsForType(string text)
        {
            // Return suggestions based on token type
            if (IsOperator(text))
            {
                return validOperations.Concat(logicalOperators).ToList();
            }
            else if (IsProperty(text))
            {
                return validProperties;
            }
            else
            {
                // For values, return some common values
                return new List<string> { "true", "false", "0", "100" };
            }
        }
        
        private bool IsProperty(string text)
        {
            return validProperties.Contains(text);
        }

        private bool IsOperator(string text)
        {
            return validOperations.Contains(text) || logicalOperators.Contains(text);
        }

        private bool IsValue(string text)
        {
            return text.StartsWith("'") || text.StartsWith("\"") ||
                   double.TryParse(text, out _) || bool.TryParse(text, out _);
        }

        // Add method to get all tokens
        public IEnumerable<TokenControl> GetTokens()
        {
            return tokens;
        }

        // Expose the inputBox's caret position
        public int CaretPosition 
        { 
            get 
            {
                var textBox = inputBox.Template.FindName("PART_EditableTextBox", inputBox) as TextBox;
                return textBox?.CaretIndex ?? 0;
            } 
        }

        // Get text before the caret
        public string GetTextBeforeCaret()
        {
            var textBox = inputBox.Template.FindName("PART_EditableTextBox", inputBox) as TextBox;
            if (textBox == null || textBox.CaretIndex < 0) return string.Empty;
            
            // Get text before caret in the inputBox
            return textBox.Text.Substring(0, textBox.CaretIndex);
        }

        // Get the current text
        public string GetCurrentText()
        {
            return inputBox.Text;
        }

        // Set the expression by creating tokens from a string
        public void SetExpression(string expression)
        {
            // Clear existing tokens
            foreach (var token in tokens.ToList())
            {
                tokenPanel.Children.Remove(token);
                tokens.Remove(token);
            }
            
            // Split expression into tokens and add them
            var words = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Reset state
            currentState = RuleInputState.Property;
            
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;
                
                // Validate input based on current state
                if (!ValidateInput(word))
                {
                    // Skip invalid tokens
                    continue;
                }
                
                var token = new TokenControl
                {
                    DataContext = new TokenViewModel
                    {
                        Text = word,
                        Type = DetermineTokenType(word),
                        Suggestions = GetSuggestionsForType(word)
                    }
                };
                
                token.TokenRemoved += Token_Removed;
                tokens.Add(token);
                tokenPanel.Children.Insert(tokenPanel.Children.Count - 1, token);
                
                // Advance to the next state
                AdvanceState(word);
            }
            
            // Update suggestions for the current state
            UpdateComboBoxSuggestions();
        }

        private void TokenPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Set focus to the input box when the panel is clicked
            inputBox.Focus();
            
            // Position the caret at the end of the text
            var textBox = inputBox.Template.FindName("PART_EditableTextBox", inputBox) as TextBox;
            if (textBox != null)
            {
                textBox.CaretIndex = textBox.Text.Length;
            }
        }
        
        // Override the Focus method to set focus to the input box
        public new bool Focus()
        {
            return inputBox.Focus();
        }
        
        private void InputBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When an item is selected from the dropdown, create a token immediately
            if (inputBox.SelectedItem != null)
            {
                // Set the text to the selected item
                inputBox.Text = inputBox.SelectedItem.ToString();
                
                // Create a token from the selected item
                CreateTokenFromInput();
                
                // Clear the selection
                inputBox.SelectedItem = null;
            }
        }
        
        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // For Property and Operation states, enforce selection from the list
            if (currentState != RuleInputState.Value)
            {
                var text = GetInputText().Trim();
                if (!string.IsNullOrEmpty(text) && !ValidateInput(text))
                {
                    // Clear invalid input
                    inputBox.Text = string.Empty;
                }
            }
            else if (!string.IsNullOrEmpty(GetInputText().Trim()))
            {
                // For Value state, create a token when focus is lost if there's text
                CreateTokenFromInput();
            }
        }
    }

    public class TokenEventArgs : EventArgs
    {
        public TokenControl Token { get; private set; }

        public TokenEventArgs(TokenControl token)
        {
            Token = token;
        }
    }
}