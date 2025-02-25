using RuleEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuleEditor.Controls
{
    public class TokenViewModel : ViewModelBase
    {
        public string Text
        {
            get => GetValue<string>();
            set => SetValue(value);
        }
        public TokenType Type
        {
            get => GetValue<TokenType>();
            set => SetValue(value);
        }
        public List<string> Suggestions 
        {
            get => GetValue<List<string>>();
            set => SetValue(value);
        }        
    }

    public enum TokenType
    {
        Property,
        Operator,
        Value
    }
}
