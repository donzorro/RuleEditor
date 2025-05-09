﻿using RuleEditor.Controls;
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
using System.Windows.Media;

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

    // Converter to show watermark when text is empty
    public class WatermarkVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is string text && values[1] is bool hasFocus)
            {
                // Show watermark when text is empty and control doesn't have focus
                return string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
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
        public event EventHandler ExpressionChanged;

        private List<TokenControl> tokens = new List<TokenControl>();
        private RuleInputState currentState = RuleInputState.Property;
        private string watermarkText = "Select a property";
        
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
            tokens = new List<TokenControl>();
            currentState = RuleInputState.Property;                              
            
            // Initialize the input TokenControl
            var inputViewModel = new TokenViewModel
            {
                Text = string.Empty,
                Type = ConvertStateToTokenType(currentState),
                Suggestions = new List<string>() // Will be updated later
            };
            
            inputTokenControl.DataContext = inputViewModel;
            
            // Subscribe to events from the input TokenControl
            inputTokenControl.KeyDown += InputTokenControl_KeyDown; // Direct KeyDown on the control
            
            if (inputTokenControl.FindName("tokenComboBox") is ComboBox comboBox)
            {
                // Track selection changes and key events
                comboBox.SelectionChanged += InputTokenControl_SelectionChanged;
                comboBox.PreviewKeyDown += InputTokenControl_PreviewKeyDown;
                comboBox.LostFocus += InputTokenControl_LostFocus;
                
                // We need to wait until the template is applied to get the TextBox for text changes
                comboBox.Loaded += (s, e) =>
                {
                    var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                    if (textBox != null)
                    {
                        textBox.TextChanged += InputTextBox_TextChanged;
                        textBox.PreviewKeyDown += InputBox_KeyDown;
                    }
                };
            }
            
            // Update suggestions for the initial state
            UpdateInputTokenSuggestions();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            var inputBox = sender as TextBox;

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
            else if (e.Key == Key.Left)
            {
                // Get the TextBox inside the ComboBox
                var textBox = inputBox.Template.FindName("PART_EditableTextBox", inputBox) as TextBox;

                // If at the beginning of the text and we have tokens, navigate to the last token
                if (textBox != null && textBox.CaretIndex == 0 && tokens.Count > 0)
                {
                    tokens[tokens.Count - 1].Focus();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Right)
            {
                // Get the TextBox inside the ComboBox
                var textBox = inputBox.Template.FindName("PART_EditableTextBox", inputBox) as TextBox;

                // If at the end of the text and we have tokens, navigate to the first token
                if (textBox != null && textBox.CaretIndex == textBox.Text.Length && tokens.Count > 0)
                {
                    tokens[0].Focus();
                    e.Handled = true;
                }
            }
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Forward the TextChanged event
            TextChanged?.Invoke(this, e);
            
            if (sender is TextBox textBox)
            {
                var text = textBox.Text;
                if (text.EndsWith(" ") || text.EndsWith("\n"))
                {
                    CreateTokenFromInput();
                }
            }
        }

        private void UpdateInputTokenSuggestions()
        {

            if (inputTokenControl.DataContext is TokenViewModel viewModel)
            {
                viewModel.Suggestions = GetSuggestionsForType(viewModel.Text);
            }
        }
        
        private void InputTokenControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                comboBox.Text = comboBox.SelectedItem.ToString();
                CreateTokenFromInput();
                
                // Prevent focus from moving to the next control
                comboBox.Focus();
            }
        }
        
        private void InputTokenControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Similar logic to the original InputBox_PreviewKeyDown
            if (e.Key == Key.Left)
            {
                // Get the TextBox inside the ComboBox
                if (sender is ComboBox comboBox && 
                    comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox textBox)
                {
                    // If at the beginning of the text and we have tokens, navigate to the last token
                    if (textBox.CaretIndex == 0 && tokens.Count > 0)
                    {
                        tokens[tokens.Count - 1].Focus();
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Right)
            {
                // Get the TextBox inside the ComboBox
                if (sender is ComboBox comboBox && 
                    comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox textBox)
                {
                    // If at the end of the text and we have tokens, navigate to the first token
                    if (textBox.CaretIndex == textBox.Text.Length && tokens.Count > 0)
                    {
                        tokens[0].Focus();
                        e.Handled = true;
                    }
                }
            }
        }
        
        private void InputTokenControl_LostFocus(object sender, RoutedEventArgs e)
        {
            // Implement any necessary lost focus behavior
        }
        
        private void CreateTokenFromInput()
        {
            var text = GetInputText().Trim();
            if (string.IsNullOrEmpty(text)) return;
            
            // Validate input based on current state
            if (!ValidateInput(text))
            {
                // If invalid, clear the input and don't create a token
                if (inputTokenControl.DataContext is TokenViewModel vm)
                {
                    vm.Text = string.Empty;
                }
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
            
            // Clear the input control
            if (inputTokenControl.DataContext is TokenViewModel viewModel)
            {
                viewModel.Text = string.Empty;
            }
            
            // Advance to the next state
            AdvanceState(text);
            
            // Update suggestions for the new state
            UpdateInputTokenSuggestions();
            
            // Notify that a token was added
            TokenAdded?.Invoke(this, new TokenEventArgs(token));
            
            NotifyExpressionChanged();
        }
        
        // Check if we have at least one complete rule (property-operation-value)
        private bool HasCompleteRule()
        {
            return tokens.Count >= 3 && 
                   tokens.Count % 3 == 0; // Complete rules should have tokens in multiples of 3
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
            UpdateInputTokenSuggestions();
            
            // Notify that a token was removed
            TokenRemoved?.Invoke(this, new TokenEventArgs(token));
            
            NotifyExpressionChanged();
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
                switch (currentState)
                {
                    case RuleInputState.Property:
                        return validProperties;
                        break;

                    case RuleInputState.Operation:
                        return validOperations.Concat(logicalOperators).ToList();
                        break;

                    case RuleInputState.Value:
                        return new List<string> { "true", "false", "0", "100" };
                        break;
                }
            }
            return new List<string>();
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
                var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
                return textBox?.CaretIndex ?? 0;
            } 
        }

        // Get text before the caret
        public string GetTextBeforeCaret()
        {
            var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
            if (textBox == null || textBox.CaretIndex < 0) return string.Empty;
            
            // Get text before caret in the inputBox
            return textBox.Text.Substring(0, textBox.CaretIndex);
        }

        // Get the current text
        public string GetCurrentText()
        {
            return GetInputText();
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
            UpdateInputTokenSuggestions();
            
            NotifyExpressionChanged();
        }

        private void TokenPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Set focus to the input box when the panel is clicked
            inputTokenControl.Focus();
            
            // Position the caret at the end of the text
            var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
            if (textBox != null)
            {
                textBox.CaretIndex = textBox.Text.Length;
            }
        }
        
        // Override the Focus method to set focus to the input box
        public new bool Focus()
        {
            return inputTokenControl.Focus();
        }
        
        private void InputTokenControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrEmpty(GetInputText().Trim()))
                {
                    // Create token from current input
                    CreateTokenFromInput();
                    
                    // Focus the input box again to prepare for next token
                    inputTokenControl.Focus();
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
            else if (e.Key == Key.Left)
            {
                // Get the TextBox inside the ComboBox
                var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
                
                // If at the beginning of the text and we have tokens, navigate to the last token
                if (textBox != null && textBox.CaretIndex == 0 && tokens.Count > 0)
                {
                    tokens[tokens.Count - 1].Focus();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Right)
            {
                // Get the TextBox inside the ComboBox
                var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
                
                // If at the end of the text and we have tokens, navigate to the first token
                if (textBox != null && textBox.CaretIndex == textBox.Text.Length && tokens.Count > 0)
                {
                    tokens[0].Focus();
                    e.Handled = true;
                }
            }
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

        // Navigate to the next token or input box
        public void NavigateToNextToken(TokenControl currentToken)
        {
            int currentIndex = tokens.IndexOf(currentToken);
            
            if (currentIndex >= 0 && currentIndex < tokens.Count - 1)
            {
                // Focus the next token
                tokens[currentIndex + 1].Focus();
                
                // Position caret at the beginning of the text in the next token
                var nextToken = tokens[currentIndex + 1];
                var comboBox = nextToken.FindName("tokenComboBox") as ComboBox;
                var textBox = comboBox?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                if (textBox != null)
                {
                    textBox.CaretIndex = 0;
                }
            }
            else
            {
                // Focus the input box if we're at the last token
                inputTokenControl.Focus();
                
                // Position caret at the beginning of the text in the input box
                var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
                if (textBox != null)
                {
                    textBox.CaretIndex = 0;
                }
            }
        }
        
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            
            if (parentObject == null)
                return null;
                
            if (parentObject is T parent)
                return parent;
                
            return FindParent<T>(parentObject);
        }

        // Navigate to the previous token or input box
        public void NavigateToPreviousToken(TokenControl currentToken)
        {
            int currentIndex = tokens.IndexOf(currentToken);
            
            if (currentIndex > 0)
            {
                // Focus the previous token
                tokens[currentIndex - 1].Focus();
                
                // Position caret at the end of the text in the previous token
                var prevToken = tokens[currentIndex - 1];
                var comboBox = prevToken.FindName("tokenComboBox") as ComboBox;
                var textBox = comboBox?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                if (textBox != null)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                }
            }
            else if (currentIndex == 0)
            {
                // If we're at the first token, focus the input box
                inputTokenControl.Focus();
                
                // Position caret at the end of the text in the input box
                var textBox = inputTokenControl.Template.FindName("PART_EditableTextBox", inputTokenControl) as TextBox;
                if (textBox != null)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                }
            }
        }       
        
        // Handle PreviewKeyDown for the input box to ensure arrow key navigation works properly
   
        
        // Notify subscribers that the expression has changed
        private void NotifyExpressionChanged()
        {
            // Raise the ExpressionChanged event
            ExpressionChanged?.Invoke(this, EventArgs.Empty);
        }

        // Called by TokenControl when its text changes
        public void NotifyTokenChanged(TokenControl token)
        {
            // Notify that a token was changed
            TokenChanged?.Invoke(this, new TokenEventArgs(token));
            
            // Notify that the expression has changed
            NotifyExpressionChanged();
        }

        // Helper method to convert RuleInputState to TokenType
        private TokenType ConvertStateToTokenType(RuleInputState state)
        {
            switch (state)
            {
                case RuleInputState.Property:
                    return TokenType.Property;
                case RuleInputState.Operation:
                    return TokenType.Operator;
                case RuleInputState.Value:
                    return TokenType.Value;
                default:
                    return TokenType.Property;
            }
        }

        private string GetInputText()
        {
            // Get text from the input TokenControl's ComboBox
            if (inputTokenControl.DataContext is TokenViewModel viewModel)
            {
                return viewModel.Text;
            }
            return string.Empty;
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