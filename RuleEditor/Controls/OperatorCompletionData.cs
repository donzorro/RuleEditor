using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Media;

public class OperatorCompletionData : ICompletionData
{
    private readonly string _operator;

    public OperatorCompletionData(string op)
    {
        _operator = op;
    }

    public ImageSource Image => null;

    public string Text => _operator;

    public object Content => _operator;

    public object Description => GetOperatorDescription(_operator);

    public double Priority => -1; // Show after properties

    private string GetOperatorDescription(string op)
    {
        return op.ToUpper() switch
        {
            "AND" => "Logical AND operator",
            "OR" => "Logical OR operator",
            ">" => "Greater than operator",
            "<" => "Less than operator",
            ">=" => "Greater than or equal operator",
            "<=" => "Less than or equal operator",
            "==" => "Equality operator",
            "!=" => "Inequality operator",
            "CONTAINS" => "String contains operator",
            "STARTSWITH" => "String starts with operator",
            "ENDSWITH" => "String ends with operator",
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