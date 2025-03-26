using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using RuleEditor.Behaviors;
using RuleEditor.Controls;
using RuleEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RuleEditor.ViewModels
{
    /// <summary>
    /// Interaction logic for RuleEditorView.xaml
    /// </summary>
    public partial class RuleEditorView : UserControl
    {
        private CompletionWindow _completionWindow;
        private readonly string[] _triggerChars = { ".", " " };
        private readonly string[] _operators = { "AND", "OR", ">", "<", ">=", "<=", "==", "!=", "CONTAINS", "STARTSWITH", "ENDSWITH" };

        public RuleEditorView()
        {
            InitializeComponent();

            // Subscribe to changes in unknown properties
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RuleEditorViewModel.UnknownProperties.CollectionChanged +=
                    UnknownProperties_CollectionChanged;
            }

            ExpressionEditor.TextArea.TextEntering += TextArea_TextEntering;
            ExpressionEditor.TextArea.TextEntered += TextArea_TextEntered;

            // Subscribe to the TextChanged event
            ExpressionEditor.TextChanged += ExpressionEditor_TextChanged;

            // Set initial text if needed
            if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.RuleEditorViewModel.ExpressionCode))
            {
                ExpressionEditor.Text = vm.RuleEditorViewModel.ExpressionCode;
            }
        }


        private void UnknownProperties_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Highlight unknown properties whenever the collection changes
            if (DataContext is MainViewModel viewModel)
            {
                UnknownPropertyHighlightBehavior.HighlightUnknownProperties(
                    ExpressionEditor,
                    viewModel.RuleEditorViewModel
                );
            }
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (_completionWindow == null)
                return;

            // Don't close completion window on these characters
            if (_triggerChars.Contains(e.Text))
                return;

            // If the pressed key is not a letter or digit, close the completion window
            if (!char.IsLetterOrDigit(e.Text[0]))
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextArea textArea) || !(DataContext is MainViewModel viewModel))
                return;

            // Show completion window after typing a trigger character or when starting a new word
            if (ShouldShowCompletion(e.Text, textArea))
            {
                ShowCompletionWindow(textArea);
            }
        }

        private bool ShouldShowCompletion(string enteredText, TextArea textArea)
        {
            // Always show completion after a trigger character
            if (_triggerChars.Contains(enteredText))
                return true;

            // Get the current line text up to the caret
            var line = textArea.Document.GetLineByOffset(textArea.Caret.Offset);
            var lineText = textArea.Document.GetText(line.Offset, textArea.Caret.Offset - line.Offset);

            // Split into tokens
            var tokens = lineText.Split(new[] { ' ', '(', ')', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);

            // Always show completion at the start of a line or after an operator
            if (tokens.Length == 1 ||
                (tokens.Length > 1 && IsOperator(tokens[tokens.Length - 1])))
            {
                return true;
            }

            return false;
        }

        private bool IsOperator(string token)
        {
            var operators = new[]
            {
        "==", "!=", ">", "<", ">=", "<=",
        "CONTAINS", "STARTSWITH", "ENDSWITH",
        "AND", "OR"
    };
            return operators.Contains(token.ToUpper());
        }
        private void ShowCompletionWindow(TextArea textArea)
        {
            if (!(DataContext is MainViewModel viewModel))
                return;

            // Tokenize the current expression up to the caret
            var currentLineOffset = textArea.Document.GetLineByOffset(textArea.Caret.Offset);
            var lineText = textArea.Document.GetText(currentLineOffset.Offset, textArea.Caret.Offset - currentLineOffset.Offset);
            var tokens = Tokenize(lineText);

            // Determine the current context for suggestions
            var context = DetermineExpressionContext(tokens);

            // Create completion window
            _completionWindow = new CompletionWindow(textArea);
            var data = _completionWindow.CompletionList.CompletionData;

            // Determine the last property (if any) for type-based filtering
            var lastProperty = FindLastProperty(tokens);

            // Provide context-specific suggestions
            switch (context)
            {
                case ExpressionContext.Start:
                    // At the start of an expression, only show properties
                    AddPropertiesCompletion(data, viewModel);
                    break;

                case ExpressionContext.AfterProperty:
                    // After a property, show comparison operators specific to the property type
                    if (lastProperty != null)
                    {
                        AddOperatorsCompletion(data,
                            GetSupportedOperators(lastProperty),
                            lastProperty);
                    }
                    break;

                case ExpressionContext.AfterOperator:
                    // After an operator, show properties or values
                    AddPropertiesCompletion(data, viewModel);
                    AddValuesCompletion(data);
                    break;

                case ExpressionContext.AfterValue:
                    // After a value, show logical operators
                    AddOperatorsCompletion(data,
                        new[] { "AND", "OR" },
                        null);
                    break;

                default:
                    // Fallback to showing everything
                    AddPropertiesCompletion(data, viewModel);
                    AddOperatorsCompletion(data, _operators, null);
                    break;
            }

            // Show the completion window if we have suggestions
            if (data.Count > 0)
            {
                _completionWindow.Show();
                _completionWindow.Closed += (sender, args) => _completionWindow = null;
            }
        }

        private RulePropertyInfo FindLastProperty(List<string> tokens)
        {
            if (!(DataContext is MainViewModel viewModel))
                return null;

            // Search backwards for the last valid property
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var property = viewModel.RuleEditorViewModel.AvailableProperties
                    .FirstOrDefault(p => p.Name.Equals(tokens[i], StringComparison.OrdinalIgnoreCase));

                if (property != null)
                    return property;
            }

            return null;
        }

        private string[] GetSupportedOperators(RulePropertyInfo property)
        {
            var allOperators = new[]
            {
                "==", "!=", ">", "<", ">=", "<=",
                "CONTAINS", "STARTSWITH", "ENDSWITH"
            };

            // Filter operators based on property type
            return allOperators.Where(op => property.SupportsOperator(op)).ToArray();
        }

        private void AddOperatorsCompletion(
            IList<ICompletionData> data,
            string[] operators,
            RulePropertyInfo currentProperty)
        {
            foreach (var op in operators)
            {
                data.Add(new OperatorCompletionData(op, currentProperty));
            }
        }

        private enum ExpressionContext
        {
            Start,           // Beginning of expression
            AfterProperty,   // After a property name
            AfterOperator,   // After a comparison or logical operator
            AfterValue,      // After a value (number, string, etc.)
            Unknown          // Fallback context
        }

        private ExpressionContext DetermineExpressionContext(List<string> tokens)
        {
            if (tokens.Count == 0)
                return ExpressionContext.Start;

            var lastToken = tokens[tokens.Count - 1];

            // Check if the last token is a property
            if (IsProperty(lastToken))
                return ExpressionContext.AfterProperty;

            // Check if the last token is an operator
            if (IsComparisonOperator(lastToken) || IsLogicalOperator(lastToken))
                return ExpressionContext.AfterOperator;

            // Check if the last token is a value
            if (IsValue(lastToken))
                return ExpressionContext.AfterValue;

            return ExpressionContext.Unknown;
        }

        private void AddPropertiesCompletion(IList<ICompletionData> data, MainViewModel viewModel)
        {
            foreach (var prop in viewModel.RuleEditorViewModel.AvailableProperties)
            {
                data.Add(new PropertyCompletionData(prop));
            }
        }

        private void AddValuesCompletion(IList<ICompletionData> data)
        {
            // Add some common value suggestions
            data.Add(new ValueCompletionData("true"));
            data.Add(new ValueCompletionData("false"));
            data.Add(new ValueCompletionData("null"));
            data.Add(new ValueCompletionData("\"\""));  // Empty string
        }

        private List<string> Tokenize(string text)
        {
            // Simple tokenization - split by spaces and special characters
            return text.Split(new[] { ' ', '(', ')', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private bool IsProperty(string token)
        {
            // Check if the token is in the list of available properties
            return DataContext is MainViewModel vm &&
                   vm.RuleEditorViewModel.AvailableProperties
                      .Any(p => p.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsComparisonOperator(string token)
        {
            return new[] { ">", "<", ">=", "<=", "==", "!=", "CONTAINS", "STARTSWITH", "ENDSWITH" }
                .Contains(token.ToUpper());
        }

        private bool IsLogicalOperator(string token)
        {
            return new[] { "AND", "OR", "NOT" }
                .Contains(token.ToUpper());
        }

        private bool IsValue(string token)
        {
            // Simple value detection - could be expanded
            return token.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                   IsNumeric(token) ||
                   IsQuotedString(token);
        }

        private bool IsNumeric(string token)
        {
            return double.TryParse(token, out _);
        }

        private bool IsQuotedString(string token)
        {
            return (token.StartsWith("\"") && token.EndsWith("\"")) ||
                   (token.StartsWith("'") && token.EndsWith("'"));
        }

        private void ExpressionEditor_TextChanged(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // Update the Expression property in the ViewModel
                viewModel.RuleEditorViewModel.ExpressionCode = ExpressionEditor.Text;

                // Update the Document property if it exists
                if (viewModel.RuleEditorViewModel.ExpressionDocument != null)
                {
                    viewModel.RuleEditorViewModel.ExpressionDocument = ExpressionEditor.Document;
                }

                // Trigger validation
                viewModel.RuleEditorViewModel.ValidateExpression();
            }
        }

        private void PropertyList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is RulePropertyInfo property)
            {
                // Get the current caret position
                var caretOffset = ExpressionEditor.CaretOffset;

                // Get the current line text up to the caret
                var currentLine = ExpressionEditor.Document.GetLineByOffset(caretOffset);
                var lineText = ExpressionEditor.Document.GetText(currentLine.Offset, caretOffset - currentLine.Offset);

                // Determine if we need to add a space before the property
                var needsSpace = lineText.Length > 0 &&
                               !lineText.EndsWith(" ") &&
                               !lineText.EndsWith("(");

                // Insert the property name with proper spacing
                var textToInsert = needsSpace ? $" {property.Name}" : property.Name;
                ExpressionEditor.Document.Insert(caretOffset, textToInsert);

                // Move caret after the inserted text
                ExpressionEditor.CaretOffset = caretOffset + textToInsert.Length;

                // Focus back on the editor
                ExpressionEditor.Focus();
            }
        }
    }
}
