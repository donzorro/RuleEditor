using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Media;

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

            // Set initial focus
            this.Loaded += (s, e) => expressionTextBox.Focus();
            
            // Hook up text changed event
            expressionTextBox.TextChanged += ExpressionTextBox_TextChanged;
            expressionTextBox.MouseMove += ExpressionTextBox_MouseMove;
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

        private void ExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Update the view model with the new text
            _viewModel.ExpressionText = expressionTextBox.Text;
            
            // Update caret position in the view model
            _viewModel.CaretPosition = expressionTextBox.CaretIndex;
            
            // Clear any existing error adorners when text changes
            ClearErrorAdorners();
            
            // Show suggestions popup if there are suggestions available
            UpdateSuggestionsPopup();
            
            // Start validation timer
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

        private void ExpressionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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
            }
        }

        private void ApplySelectedSuggestion()
        {
            if (suggestionsList.SelectedItem == null)
                return;

            var suggestion = suggestionsList.SelectedItem.ToString();

            // Get the current token if any
            var currentToken = _viewModel.CurrentToken;

            if (currentToken != null)
            {
                // Replace the current token with the suggestion
                int selectionStart = currentToken.Position;
                int selectionLength = currentToken.Length;

                // Update the text
                string newText = expressionTextBox.Text.Substring(0, selectionStart) +
                                suggestion +
                                expressionTextBox.Text.Substring(selectionStart + selectionLength);

                expressionTextBox.Text = newText;

                // Set caret position after the inserted suggestion
                expressionTextBox.CaretIndex = selectionStart + suggestion.Length;
            }
            else
            {
                expressionTextBox.Text += suggestion;
                expressionTextBox.CaretIndex = expressionTextBox.Text.Length;
            }

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
            
            var points = new List<Point>();
            
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