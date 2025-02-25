using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RuleEditor.ViewModels.Version2
{
    public partial class RuleEditorView2 : UserControl
    {
        private RuleEditorViewModel2 _viewModel;
        private bool _isUpdatingText = false;

        public RuleEditorView2()
        {
            InitializeComponent();
            _viewModel = new RuleEditorViewModel2();
            DataContext = _viewModel;

            // Set up auto-completion popup
            InitializeAutoCompletion();

            // Set up token event handlers
            expressionEditor.TokenAdded += ExpressionEditor_TokenAdded;
            expressionEditor.TokenRemoved += ExpressionEditor_TokenRemoved;
            expressionEditor.TokenChanged += ExpressionEditor_TokenChanged;
            
            // Set focus to the expression editor when the control is loaded
            this.Loaded += (s, e) => expressionEditor.Focus();
            
            // Add click handler to set focus to the expression editor
            this.PreviewMouseDown += (s, e) => 
            {
                if (!e.Handled)
                {
                    expressionEditor.Focus();
                }
            };
        }

        private void InitializeAutoCompletion()
        {
            // Subscribe to the inputBox's TextChanged event through our custom event
            expressionEditor.TextChanged += (s, e) =>
            {
                if (!_isUpdatingText)
                {
                    ShowCompletionWindow();
                }
            };
        }

        private void ShowCompletionWindow()
        {
            // Get text before caret using the new method
            string textBeforeCaret = expressionEditor.GetTextBeforeCaret();
            if (string.IsNullOrEmpty(textBeforeCaret)) return;

            // Show completion based on context
            var (suggestions, startIndex) = _viewModel.GetSuggestions(textBeforeCaret);
            if (suggestions != null && suggestions.Count > 0)
            {
                // Show popup with suggestions
                ShowSuggestionsPopup(suggestions);
            }
        }

        private void ShowSuggestionsPopup(System.Collections.Generic.List<string> suggestions)
        {
            // Implementation for showing suggestions popup
            // This would be similar to the AvalonEdit version but using WPF's Popup control
        }

        private void ExpressionEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;

            _isUpdatingText = true;
            try
            {
                // Get text from tokens
                string text = GetTextFromTokens();
                _viewModel.ExpressionCode = text;

                // Highlight unknown properties
                HighlightUnknownProperties();
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
       

        private string GetTextFromTokens()
        {
            // Collect text from all tokens
            StringBuilder sb = new StringBuilder();
            foreach (var token in expressionEditor.GetTokens())
            {
                if (sb.Length > 0)
                    sb.Append(" ");
                sb.Append(token.Text);
            }
            return sb.ToString();
        }

        private void HighlightUnknownProperties()
        {
            // Get unknown properties from ViewModel
            string text = GetTextFromTokens();
            var unknownProperties = _viewModel.GetUnknownProperties(text);
            
            // Highlight tokens that match unknown properties
            foreach (var token in expressionEditor.GetTokens())
            {
                if (unknownProperties.Contains(token.Text))
                {
                    token.SetErrorHighlight(true);
                }
                else
                {
                    token.SetErrorHighlight(false);
                }
            }
        }

        private void ExpressionEditor_TokenAdded(object sender, TokenEventArgs e)
        {
            UpdateExpressionFromTokens();
        }

        private void ExpressionEditor_TokenRemoved(object sender, TokenEventArgs e)
        {
            UpdateExpressionFromTokens();
        }

        private void ExpressionEditor_TokenChanged(object sender, TokenEventArgs e)
        {
            UpdateExpressionFromTokens();
        }

        private void UpdateExpressionFromTokens()
        {
            // Convert tokens to expression string
            var expression = string.Join(" ", expressionEditor.GetTokens().Select(t => t.Text));
            _viewModel.ExpressionCode = expression;
        }

        private void FormatButton_Click(object sender, RoutedEventArgs e)
        {
            var formattedExpression = _viewModel.FormatExpression(_viewModel.ExpressionCode);
            // Convert formatted expression back to tokens
            expressionEditor.SetExpression(formattedExpression);
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = _viewModel.ValidateExpression(_viewModel.ExpressionCode);
            validationMessage.Text = isValid ? "Expression is valid" : "Expression is invalid";
            validationMessage.Foreground = isValid ? Brushes.Green : Brushes.Red;
        }
    }
}
