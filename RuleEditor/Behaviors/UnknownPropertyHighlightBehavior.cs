using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using RuleEditor.ViewModels;

namespace RuleEditor.Behaviors
{
    public static class UnknownPropertyHighlightBehavior
    {
        public static void HighlightUnknownProperties(TextEditor editor, RuleEditorViewModel viewModel)
        {
            // Clear existing highlighting
            editor.TextArea.TextView.LineTransformers.Clear();

            // Add new highlighter
            editor.TextArea.TextView.LineTransformers.Add(
                new UnknownPropertyHighlighter(viewModel.UnknownProperties)
            );
        }
    }

    public class UnknownPropertyHighlighter : DocumentColorizingTransformer
    {
        private readonly System.Collections.ObjectModel.ObservableCollection<string> _unknownProperties;

        public UnknownPropertyHighlighter(System.Collections.ObjectModel.ObservableCollection<string> unknownProperties)
        {
            _unknownProperties = unknownProperties;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (CurrentContext.Document == null)
                return;

            var lineText = CurrentContext.Document.GetText(line);

            foreach (var unknownProperty in _unknownProperties)
            {
                int index = 0;
                while ((index = lineText.IndexOf(unknownProperty, index)) != -1)
                {
                    ChangeLinePart(
                        line.Offset + index,
                        line.Offset + index + unknownProperty.Length,
                        (VisualLineElement element) =>
                        {
                            element.TextRunProperties.SetForegroundBrush(Brushes.Red);
                            element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)));
                        }
                    );
                    index += unknownProperty.Length;
                }
            }
        }
    }
}