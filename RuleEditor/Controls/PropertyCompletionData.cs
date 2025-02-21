using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media;
using RuleEditor.ViewModels;
using RuleEditor.Models;

namespace RuleEditor.Controls
{
    public class PropertyCompletionData : ICompletionData
    {
        private readonly PropertyInfo _propertyInfo;

        public PropertyCompletionData(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public ImageSource Image => null;

        public string Text => _propertyInfo.Name;

        public object Content => $"{_propertyInfo.Name} : {_propertyInfo.Type}";

        public object Description => $"Property of type {_propertyInfo.Type}";

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}