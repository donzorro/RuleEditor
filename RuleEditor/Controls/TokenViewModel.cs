using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleEditor.Controls
{
    public class TokenViewModel : INotifyPropertyChanged
    {
        public string Text { get; set; }
        public TokenType Type { get; set; }
        public List<string> Suggestions { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public enum TokenType
    {
        Property,
        Operator,
        Value
    }
}
