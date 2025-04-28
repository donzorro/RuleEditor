using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;

namespace RuleEditor.ViewModels.Version3
{
    public partial class RuleEditorControl3 : UserControl
    {
        private RuleEditorViewModel3 _viewModel;
        private DispatcherTimer _validationTimer;
        private bool _isNavigatingSuggestions = false;

        public RuleEditorControl3()
        {
            InitializeComponent();

            _viewModel = new RuleEditorViewModel3();
            DataContext = _viewModel;

            // Set up validation timer
            _validationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _validationTimer.Tick += ValidationTimer_Tick;

            // Listen for changes to SyntaxErrorObjects property
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Set initial focus
            this.Loaded += (s, e) => expressionTextBox.Focus();
            
            // Hook up text changed event
            expressionTextBox.TextChanged += ExpressionTextBox_TextChanged;
            expressionTextBox.MouseMove += ExpressionTextBox_MouseMove;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When SyntaxErrorObjects changes, update the error adorners
            if (e.PropertyName == nameof(RuleEditorViewModel3.SyntaxErrorObjects))
            {
                DrawErrorUnderlines();
            }
        }

        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            _viewModel.ValidateExpressionSyntax();
            
            // After validation, draw squiggly lines under tokens with errors
            DrawErrorUnderlines();
        }

        private void DrawErrorUnderlines()
        {
            // Clear any existing adorners
            ClearErrorAdorners();
            
            // Add new adorners for syntax errors
            var layer = AdornerLayer.GetAdornerLayer(expressionTextBox);
            if (layer != null && _viewModel.SyntaxErrorObjects != null)
            {
                foreach (var error in _viewModel.SyntaxErrorObjects)
                {
                    var adorner = new SquigglyLineAdorner(expressionTextBox, error.Position, error.Length);
                    layer.Add(adorner);
                }
            }
        }

        private bool _isInternalUpdate = false;

        private void ExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInternalUpdate) return;

            _viewModel.ExpressionText = expressionTextBox.Text;
            _viewModel.CaretPosition = expressionTextBox.CaretIndex;
            ClearErrorAdorners();

            if (_lastKeyPressed != Key.Back && _lastKeyPressed != Key.Delete)
            {
                if (suggestionsList.Items.Count > 0 && !string.IsNullOrEmpty(_viewModel.CurrentToken?.Value))
                {
                    string typed = _viewModel.CurrentToken.Value;
                    string suggestion = suggestionsList.Items[0].ToString();

                    if (suggestion.StartsWith(typed, StringComparison.OrdinalIgnoreCase) && suggestion.Length > typed.Length)
                    {
                        int tokenStart = _viewModel.CurrentToken.Position;
                        int tokenEnd = tokenStart + typed.Length;
                        string before = expressionTextBox.Text.Substring(0, tokenStart);
                        string after = expressionTextBox.Text.Substring(tokenEnd);

                        string newText = before + suggestion + after;

                        if (expressionTextBox.Text != newText)
                        {
                            _isInternalUpdate = true;
                            expressionTextBox.Text = newText;
                            // Set selection directly
                            expressionTextBox.SelectionStart = tokenStart + typed.Length;
                            expressionTextBox.SelectionLength = suggestion.Length - typed.Length;
                            _isInternalUpdate = false;

                            // Update ViewModel directly, but do NOT raise PropertyChanged
                            _viewModel.ExpressionText = newText;
                            _viewModel.CaretPosition = expressionTextBox.CaretIndex;
                        }
                    }
                }
            }

            UpdateSuggestionsPopup();
            _validationTimer.Stop();
            _validationTimer.Start();
        }

        private void ExpressionTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isNavigatingSuggestions)
                return;

            // Update caret position in the view model
            _viewModel.CaretPosition = expressionTextBox.CaretIndex;

            // Show suggestions popup if there are suggestions available
            UpdateSuggestionsPopup();
        }

        private void UpdateSuggestionsPopup()
        {
            // Always update the suggestions list before deciding whether to show the popup
            suggestionsList.ItemsSource = _viewModel.Suggestions;
            
            if (_viewModel.Suggestions.Count > 0)
            {
                // Position the popup relative to the caret position
                suggestionsPopup.HorizontalOffset = CalculatePopupHorizontalOffset();
                
                // Show the popup
                suggestionsPopup.IsOpen = true;
                
                // Select the first item
                if (suggestionsList.Items.Count > 0)
                {
                    suggestionsList.SelectedIndex = 0;
                }
                
                // Make sure the popup stays open
                suggestionsPopup.StaysOpen = true;
            }
            else
            {
                // Close the popup if there are no suggestions
                suggestionsPopup.IsOpen = false;
            }
        }

        private double CalculatePopupHorizontalOffset()
        {
            // Get the caret position relative to the text box
            var caretPos = expressionTextBox.GetRectFromCharacterIndex(expressionTextBox.CaretIndex);
            return caretPos.X;
        }

        private double CalculatePopupVerticalOffset()
        {
            // Get the caret position relative to the text box
            var caretPos = expressionTextBox.GetRectFromCharacterIndex(expressionTextBox.CaretIndex);
            return caretPos.Bottom;
        }

        private Key _lastKeyPressed;
        private void ExpressionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _lastKeyPressed = e.Key;
            if (suggestionsPopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        // Navigate to the next suggestion
                        _isNavigatingSuggestions = true;
                        if (suggestionsList.SelectedIndex < suggestionsList.Items.Count - 1)
                        {
                            suggestionsList.SelectedIndex++;
                        }
                        suggestionsList.ScrollIntoView(suggestionsList.SelectedItem);
                        _isNavigatingSuggestions = false;
                        e.Handled = true;
                        break;

                    case Key.Up:
                        // Navigate to the previous suggestion
                        _isNavigatingSuggestions = true;
                        if (suggestionsList.SelectedIndex > 0)
                        {
                            suggestionsList.SelectedIndex--;
                        }
                        suggestionsList.ScrollIntoView(suggestionsList.SelectedItem);
                        _isNavigatingSuggestions = false;
                        e.Handled = true;
                        break;

                    case Key.Tab:
                    case Key.Enter:
                        // Apply the selected suggestion
                        if (suggestionsList.SelectedItem != null)
                        {
                            ApplySelectedSuggestion();
                            e.Handled = true;
                        }
                        break;

                    case Key.Escape:
                        // Close the suggestions popup
                        suggestionsPopup.IsOpen = false;
                        e.Handled = true;
                        break;
                }
            }

            // Start validation timer when typing
            if (!e.Handled && e.Key != Key.Up && e.Key != Key.Down)
            {
                _validationTimer.Stop();
                _validationTimer.Start();
            }
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This is handled by the keyboard navigation
        }

        private void SuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Tab:
                case Key.Enter:
                    // Apply the selected suggestion
                    if (suggestionsList.SelectedItem != null)
                    {
                        ApplySelectedSuggestion();
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    // Close the suggestions popup
                    suggestionsPopup.IsOpen = false;
                    expressionTextBox.Focus();
                    e.Handled = true;
                    break;
            }
        }

        private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Apply the selected suggestion on double-click
            if (suggestionsList.SelectedItem != null)
            {
                ApplySelectedSuggestion();
                expressionTextBox.Focus();
            }
        }


        private void ApplySelectedSuggestion()
        {
            if (suggestionsList.SelectedItem == null)
                return;

            var suggestion = suggestionsList.SelectedItem.ToString();
            var currentToken = _viewModel.CurrentToken;

            // Helper: Remove and restore the binding, to prevent caret jump/flicker
            void SetTextAndCaret(string newText, int caretIndex, int selectionLength = 0)
            {
                var binding = BindingOperations.GetBinding(expressionTextBox, TextBox.TextProperty);
                BindingOperations.ClearBinding(expressionTextBox, TextBox.TextProperty);
                expressionTextBox.Text = newText;
                expressionTextBox.CaretIndex = caretIndex;
                expressionTextBox.SelectionLength = selectionLength;
                if (binding != null)
                    BindingOperations.SetBinding(expressionTextBox, TextBox.TextProperty, binding);
                _viewModel.ExpressionText = newText;
                _viewModel.CaretPosition = caretIndex;
            }

            if (currentToken != null)
            {
                int selectionStart = currentToken.Position;
                int selectionLength = currentToken.Length;
                string existingToken = expressionTextBox.Text.Substring(selectionStart, selectionLength);

                // --- Logical operator special case ---
                if (expressionTextBox.CaretIndex == selectionStart + selectionLength &&
                    (suggestion == "AND" || suggestion == "OR" || suggestion == "NOT"))
                {
                    int caret = expressionTextBox.CaretIndex;
                    string text = expressionTextBox.Text;
                    bool needsSpace = (caret == 0 || text[caret - 1] != ' ');
                    string insertText = (needsSpace ? " " : "") + suggestion + " ";
                    string newText = text.Insert(caret, insertText);

                    // Only update if not already present
                    if (!text.Substring(caret).StartsWith(insertText))
                        SetTextAndCaret(newText, caret + insertText.Length, 0);
                    else
                        expressionTextBox.CaretIndex = caret + insertText.Length;
                }
                // --- Quotes special case ---
                else if (suggestion == "'" || suggestion == "\"")
                {
                    string quotes = suggestion + suggestion;
                    string tokenInText = expressionTextBox.Text.Substring(selectionStart, selectionLength);

                    // Only update if not already quoted
                    if (tokenInText != quotes)
                    {
                        string newText = expressionTextBox.Text.Substring(0, selectionStart) +
                                         quotes +
                                         expressionTextBox.Text.Substring(selectionStart + selectionLength);
                        SetTextAndCaret(newText, selectionStart + 1, 0);
                    }
                    else
                    {
                        expressionTextBox.CaretIndex = selectionStart + 1;
                        expressionTextBox.SelectionLength = 0;
                    }
                }
                // --- Default: property/field suggestion ---
                else
                {
                    string afterToken = expressionTextBox.Text.Substring(selectionStart + selectionLength);
                    bool needsSpace = string.IsNullOrEmpty(afterToken) || !char.IsWhiteSpace(afterToken[0]);
                    string suggestedText = suggestion + (needsSpace ? " " : "");

                    bool tokenMatches = existingToken == suggestion;
                    bool spaceMissing = needsSpace && !(afterToken.StartsWith(" "));
                    if (tokenMatches && !spaceMissing)
                    {
                        // Both suggestion and space present: just move caret
                        int caretIndex = selectionStart + suggestion.Length + (needsSpace ? 1 : 0);
                        expressionTextBox.CaretIndex = caretIndex;
                        expressionTextBox.SelectionLength = 0;
                    }
                    else if (tokenMatches && spaceMissing)
                    {
                        // Suggestion present but space missing: insert space
                        string newText = expressionTextBox.Text.Insert(selectionStart + suggestion.Length, " ");
                        SetTextAndCaret(newText, selectionStart + suggestion.Length + 1, 0);
                    }
                    else
                    {
                        // Apply full suggestion and space if needed
                        string newText = expressionTextBox.Text.Substring(0, selectionStart)
                                        + suggestedText
                                        + afterToken;
                        SetTextAndCaret(newText, selectionStart + suggestion.Length + (needsSpace ? 1 : 0), 0);
                    }
                }
            }
            else // No current token, just insert at caret
            {
                int caretIndex = expressionTextBox.CaretIndex;

                if (suggestion == "'" || suggestion == "\"")
                {
                    string quotes = suggestion + suggestion;
                    // Only update if not already quoted at caret
                    bool alreadyQuoted = expressionTextBox.Text.Length >= caretIndex + 2 &&
                                         expressionTextBox.Text.Substring(caretIndex, 2) == quotes;
                    if (!alreadyQuoted)
                    {
                        string newText = expressionTextBox.Text.Substring(0, caretIndex) +
                                         quotes +
                                         expressionTextBox.Text.Substring(caretIndex);
                        SetTextAndCaret(newText, caretIndex + 1, 0);
                    }
                    else
                    {
                        expressionTextBox.CaretIndex = caretIndex + 1;
                        expressionTextBox.SelectionLength = 0;
                    }
                }
                else
                {
                    string newText = expressionTextBox.Text.Substring(0, caretIndex) +
                                     suggestion + " " +
                                     expressionTextBox.Text.Substring(caretIndex);
                    // Only update if not already present at caret
                    bool alreadyPresent = expressionTextBox.Text.Substring(caretIndex).StartsWith(suggestion + " ");
                    if (!alreadyPresent)
                        SetTextAndCaret(newText, caretIndex + suggestion.Length + 1, 0);
                    else
                        expressionTextBox.CaretIndex = caretIndex + suggestion.Length + 1;
                }
            }

            _viewModel.UpdateTokens();
            _viewModel.UpdateCurrentToken();
            expressionTextBox.Focus();
            UpdateSuggestionsPopup();
        }

        private void ClearErrorAdorners()
        {
            var layer = AdornerLayer.GetAdornerLayer(expressionTextBox);
            if (layer != null)
            {
                var adorners = layer.GetAdorners(expressionTextBox);
                if (adorners != null)
                {
                    foreach (var adorner in adorners)
                    {
                        if (adorner is SquigglyLineAdorner)
                        {
                            layer.Remove(adorner);
                        }
                    }
                }
            }
        }

        private void ExpressionTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            // Get the position of the mouse in the text box
            var position = e.GetPosition(expressionTextBox);
            
            // Try to get the character index at the mouse position
            int charIndex = -1;
            try
            {
                charIndex = expressionTextBox.GetCharacterIndexFromPoint(position, false);
            }
            catch
            {
                // Ignore any exceptions that might occur
                return;
            }
            
            if (charIndex >= 0)
            {
                // Check if the mouse is over a token with an error
                var errorToken = _viewModel.Tokens?.FirstOrDefault(t => 
                    t.HasError && 
                    charIndex >= t.Position && 
                    charIndex < t.Position + t.Length);
                
                if (errorToken != null && !string.IsNullOrEmpty(errorToken.ErrorMessage))
                {
                    // Show the tooltip with the error message
                    expressionTextBox.ToolTip = errorToken.ErrorMessage;
                }
                else
                {
                    // Clear the tooltip if not over an error
                    expressionTextBox.ToolTip = null;
                }
            }
            else
            {
                // Clear the tooltip if not over text
                expressionTextBox.ToolTip = null;
            }
        }
    }

    public class SquigglyLineAdorner : Adorner
    {
        private readonly TextBox _textBox;
        private readonly int _start;
        private readonly int _length;

        public SquigglyLineAdorner(TextBox textBox, int start, int length) 
            : base(textBox)
        {
            _textBox = textBox;
            _start = start;
            _length = length;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Get the rectangle of the text range
            var rect = _textBox.GetRectFromCharacterIndex(_start);
            var endRect = _textBox.GetRectFromCharacterIndex(_start + _length);
            
            // Calculate the width of the text
            double width = endRect.X + endRect.Width - rect.X;
            
            // Draw a wavy line under the text
            var pen = new Pen(Brushes.Red, 1.5);
            
            // Create a wavy pattern
            const double wavySize = 3.0;
            var startX = rect.X;
            var y = rect.Bottom + 1;
            
            var points = new System.Collections.Generic.List<Point>();
            
            // Generate points for a wavy line
            for (double x = 0; x <= width; x += wavySize)
            {
                var point = new Point(startX + x, y + ((x / wavySize) % 2 == 0 ? wavySize : 0));
                points.Add(point);
            }
            
            // Draw the wavy line
            for (int i = 0; i < points.Count - 1; i++)
            {
                drawingContext.DrawLine(pen, points[i], points[i + 1]);
            }
        }
    }
}