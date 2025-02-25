using RuleEditor.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
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

    public partial class TokenizerControl : UserControl
    {
        public event EventHandler<TokenEventArgs> TokenAdded;
        public event EventHandler<TokenEventArgs> TokenRemoved;
        public event EventHandler<TokenEventArgs> TokenChanged;
        public event EventHandler<TextChangedEventArgs> TextChanged;

        private List<TokenControl> tokens = new List<TokenControl>();

        public TokenizerControl()
        {
            InitializeComponent();
            
            // Initialize the ComboBox with suggestions
            UpdateComboBoxSuggestions();
        }

        private void UpdateComboBoxSuggestions()
        {
            // Clear existing items
            inputBox.Items.Clear();
            
            // Add property suggestions
            foreach (var property in GetAllSuggestions())
            {
                inputBox.Items.Add(property);
            }
        }

        private List<string> GetAllSuggestions()
        {
            // This method should return all possible suggestions
            // For now, return a basic list that can be expanded later
            return new List<string>
            {
                "Name", "Age", "IsActive", "Balance", "Email", "LastLoginDate",
                "AND", "OR", "NOT", ">", "<", ">=", "<=", "==", "!=", 
                "CONTAINS", "STARTSWITH", "ENDSWITH"
            };
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                e.Handled = true;
                CreateTokenFromInput();
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
            
            // Notify that a token was added
            TokenAdded?.Invoke(this, new TokenEventArgs(token));
        }

        private void Token_Removed(object sender, TokenRemovedEventArgs e)
        {
            var token = e.RemovedToken;
            tokenPanel.Children.Remove(token);
            tokens.Remove(token);
            
            // Notify that a token was removed
            TokenRemoved?.Invoke(this, new TokenEventArgs(token));
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
            // This would be expanded to provide context-sensitive suggestions
            return GetAllSuggestions();
        }

        private bool IsOperator(string text)
        {
            return text == "AND" || text == "OR" || text == "NOT" ||
                   text == ">" || text == "<" || text == ">=" || text == "<=" ||
                   text == "==" || text == "!=" || text == "CONTAINS" ||
                   text == "STARTSWITH" || text == "ENDSWITH";
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
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;
                
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
            }
            
            // Clear the input box
            inputBox.Text = string.Empty;
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