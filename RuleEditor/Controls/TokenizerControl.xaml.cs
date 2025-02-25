using RuleEditor.Controls;
using System;
using System.Collections.Generic;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace RuleEditor.ViewModels.Version2
{
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
            
            var text = inputBox.Text;
            if (text.EndsWith(" ") || text.EndsWith("\n"))
            {
                CreateTokenFromInput();
            }
        }

        private void CreateTokenFromInput()
        {
            var text = inputBox.Text.Trim();
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

            // Insert token before the input box
            tokenPanel.Children.Insert(tokenPanel.Children.Count - 1, token);
            tokens.Add(token);

            // Clear input
            inputBox.Text = "";

            // Raise event
            TokenAdded?.Invoke(this, new TokenEventArgs(token));
        }

        private void Token_Removed(object sender, TokenRemovedEventArgs e)
        {
            if (e.RemovedToken != null)
            {
                tokenPanel.Children.Remove(e.RemovedToken);
                tokens.Remove(e.RemovedToken);
                TokenRemoved?.Invoke(this, new TokenEventArgs(e.RemovedToken));
            }
        }

        private TokenType DetermineTokenType(string text)
        {
            // Simple logic to determine token type
            if (IsOperator(text)) return TokenType.Operator;
            if (IsValue(text)) return TokenType.Value;
            return TokenType.Property;
        }

        private List<string> GetSuggestionsForType(string text)
        {
            // This should be populated from the ViewModel's available properties/operators
            return new List<string>();
        }

        private bool IsOperator(string text)
        {
            return text == "==" || text == "!=" || text == ">" || text == "<" ||
                   text == ">=" || text == "<=" || text == "AND" || text == "OR" ||
                   text == "CONTAINS" || text == "STARTSWITH" || text == "ENDSWITH";
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
        public int CaretPosition => inputBox.CaretIndex;

        // Get text before the caret
        public string GetTextBeforeCaret()
        {
            if (inputBox.CaretIndex < 0) return string.Empty;
            
            // Get text before caret in the inputBox
            return inputBox.Text.Substring(0, inputBox.CaretIndex);
        }

        // Get the current text - TextBox doesn't have paragraphs
        public string GetCurrentText()
        {
            // TextBox doesn't have paragraphs like RichTextBox
            // Just return the current text
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
            inputBox.CaretIndex = inputBox.Text.Length;
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