using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private RuleEditorControl3ViewModel _viewModel;
        private DispatcherTimer _validationTimer;
        private bool _isNavigatingSuggestions = false;

        public RuleEditorControl3()
        {
            InitializeComponent();

            _viewModel = new RuleEditorControl3ViewModel();
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
            expressionTextBox.SelectionChanged += ExpressionTextBox_SelectionChanged;
            expressionTextBox.MouseMove += ExpressionTextBox_MouseMove;
            expressionTextBox.KeyUp += ExpressionTextBox_KeyUp;
            expressionTextBox.GotFocus += ExpressionTextBox_GotFocus;
        }

        private void ExpressionTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // When textbox gains focus, show suggestions
            _viewModel.CaretPosition = expressionTextBox.CaretIndex;
            UpdateSuggestionsPopup();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When SyntaxErrorObjects changes, update the error adorners
            if (e.PropertyName == nameof(RuleEditorControl3ViewModel.SyntaxErrorObjects))
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

        // Always show and update suggestions popup
        UpdateSuggestionsPopup();
    }

        private void ExpressionTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            // Update caret position when arrow keys are used
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                _viewModel.CaretPosition = expressionTextBox.CaretIndex;
                UpdateSuggestionsPopup();
            }
        }

        private void UpdateSuggestionsPopup()
        {
        
            // Always update the suggestions list before deciding whether to show the popup
            suggestionsList.ItemsSource = _viewModel.Suggestions;

            // Hide the popup if there are no suggestions
            if (_viewModel.Suggestions == null || _viewModel.Suggestions.Count == 0)
            {
                suggestionsPopup.IsOpen = false;
                return;
            }

            // Always show the popup when the textbox is focused and there are suggestions
            suggestionsPopup.IsOpen = expressionTextBox.IsFocused;

            // Try to auto-select the best suggestion based on caret position and prefix
            string prefix = "";
            if (_viewModel.CurrentToken != null && _viewModel.CaretPosition >= _viewModel.CurrentToken.Position)
            {
                int prefixLength = Math.Max(0, _viewModel.CaretPosition - _viewModel.CurrentToken.Position);
                prefix = _viewModel.CurrentToken.Value?.Substring(0, Math.Min(prefixLength, _viewModel.CurrentToken.Value.Length)) ?? "";
            }

            // Find the best match in the suggestions list
            var match = suggestionsList.Items
                .Cast<string>()
                .FirstOrDefault(s => !string.IsNullOrEmpty(prefix) && s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match != null)
                suggestionsList.SelectedItem = match;
            else if (suggestionsList.Items.Count > 0)
                suggestionsList.SelectedIndex = 0;
            else
                suggestionsList.SelectedIndex = -1;


            suggestionsList.ScrollIntoView(suggestionsList.SelectedItem);
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

            if (e.Key == Key.Back)
            {
                int caret = expressionTextBox.CaretIndex;
                string text = expressionTextBox.Text;

                // Ensure caret is not at start or end of text
                if (caret > 0 && caret < text.Length)
                {
                    char before = text[caret - 1];
                    char after = text[caret];

                    // Check if both sides are matching quotes
                    if ((before == '\'' && after == '\'') || (before == '"' && after == '"'))
                    {
                        // Remove both quotes
                        string newText = text.Remove(caret - 1, 2);

                        // Update text and caret
                        expressionTextBox.Text = newText;
                        expressionTextBox.CaretIndex = caret - 1;
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Handle Space key when dropdown is open - apply suggestion then add space
            if (e.Key == Key.Space && suggestionsPopup.IsOpen)
            {
                if (suggestionsList.SelectedItem != null)
                {
                    ApplySelectedSuggestion();
                    // The space will be added by TextChanged, so mark as handled to prevent double space
                    e.Handled = true;
                    return;
                }
            }

            // Handle Space key when dropdown is closed - just show suggestions
            if (e.Key == Key.Space && !suggestionsPopup.IsOpen)
            {
                // Allow the space to be added, then show suggestions
                // We'll update suggestions in TextChanged event
                e.Handled = false;
                return;
            }

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
                            var selectedSuggestion = suggestionsList.SelectedItem.ToString();
                            
                            // For AND/OR, add a space instead of a line break
                            if (selectedSuggestion.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                                selectedSuggestion.Equals("OR", StringComparison.OrdinalIgnoreCase))
                            {
                                ApplySelectedSuggestion();
                                // Space is already added by ApplySelectedSuggestion
                                e.Handled = true;
                            }
                            else
                            {
                                ApplySelectedSuggestion();
                                e.Handled = true;
                            }
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
            if (suggestionsList.SelectedItem == null) return;

            var suggestion = suggestionsList.SelectedItem.ToString();
            var caretPosition = expressionTextBox.CaretIndex;

            _viewModel.ApplySelectedSuggestion(suggestion, caretPosition);

            // Update the TextBox with the updated ExpressionText and CaretPosition
            _isInternalUpdate = true;
            expressionTextBox.Text = _viewModel.ExpressionText;
            expressionTextBox.CaretIndex = _viewModel.CaretPosition;
            _isInternalUpdate = false;
            
            // Close the suggestions popup after applying
            suggestionsPopup.IsOpen = false;
            
            // Update suggestions to show what comes next
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


}