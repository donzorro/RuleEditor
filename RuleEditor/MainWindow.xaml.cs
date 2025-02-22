using RuleEditor.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Linq;
using System;
using RuleEditor.ViewModels;
using RuleEditor.Controls;
using RuleEditor.Behaviors;
using RuleEditor.Models;

namespace RuleEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
       
        public MainWindow()
        {
            this.DataContext = new MainViewModel();
            InitializeComponent();
            
           
        }

    }

    public class ValueCompletionData : ICompletionData
    {
        private readonly string _value;

        public ValueCompletionData(string value)
        {
            _value = value;
        }

        public ImageSource Image => null;
        public string Text => _value;
        public object Content => _value;
        public object Description => $"Value: {_value}";
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    public class OperatorCompletionData : ICompletionData
    {
        private readonly string _operator;
        private readonly PropertyInfo _currentProperty;

        public OperatorCompletionData(string op, PropertyInfo currentProperty = null)
        {
            _operator = op;
            _currentProperty = currentProperty;
        }

        public ImageSource Image => null;
        public string Text => _operator;
        public object Content => _operator;
        
        public object Description 
        { 
            get 
            {
                var baseDesc = GetOperatorDescription(_operator);
                if (_currentProperty != null)
                {
                    baseDesc += $"\nSupported for {_currentProperty.Type.Name}";
                }
                return baseDesc;
            }
        }

        public double Priority => -1;

        private string GetOperatorDescription(string op)
        {
            return op.ToUpper() switch
            {
                "==" => "Equality operator",
                "!=" => "Inequality operator",
                ">" => "Greater than operator",
                "<" => "Less than operator",
                ">=" => "Greater than or equal operator",
                "<=" => "Less than or equal operator",
                "CONTAINS" => "String contains operator",
                "STARTSWITH" => "String starts with operator",
                "ENDSWITH" => "String ends with operator",
                "AND" => "Logical AND operator",
                "OR" => "Logical OR operator",
                _ => "Operator"
            };
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            try 
            {
                // Ensure the completionSegment is within the document's range
                int offset = Math.Min(completionSegment.Offset, textArea.Document.TextLength);
                int length = Math.Min(completionSegment.Length, textArea.Document.TextLength - offset);

                // Add spaces around operators
                var textToInsert = $" {_operator} ";
                
                // Replace or insert text safely
                if (length > 0)
                {
                    textArea.Document.Replace(offset, length, textToInsert);
                }
                else
                {
                    textArea.Document.Insert(offset, textToInsert);
                }

                // Move caret after inserted text
                textArea.Caret.Offset = offset + textToInsert.Length;
            }
            catch (Exception ex)
            {
                // Log or handle the exception
                System.Diagnostics.Debug.WriteLine($"Error in operator completion: {ex.Message}");
            }
        }
    }
}