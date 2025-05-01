using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RuleEditor.ViewModels.Version3
{
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
