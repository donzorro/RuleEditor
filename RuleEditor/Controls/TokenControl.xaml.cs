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

namespace RuleEditor.Controls
{
    /// <summary>
    /// Interaction logic for TokenControl.xaml
    /// </summary>
    public partial class TokenControl : UserControl
    {
        public event EventHandler<TokenRemovedEventArgs> TokenRemoved;

        public TokenControl()
        {
            InitializeComponent();
        }

        private void RemoveToken_Click(object sender, RoutedEventArgs e)
        {
            // Notify parent that this token should be removed
            TokenRemoved?.Invoke(this, new TokenRemovedEventArgs(this));
        }

        public string Text
        {
            get
            {
                var viewModel = DataContext as TokenViewModel;
                return viewModel?.Text ?? string.Empty;
            }
        }

        public void SetErrorHighlight(bool isError)
        {
            // Apply red background for error, or restore normal background
            if (isError)
            {
                this.Background = new SolidColorBrush(Colors.LightPink);
            }
            else
            {
                this.Background = null; // Reset to default
            }
        }
        
        // Override the Focus method to set focus to the ComboBox
        public new bool Focus()
        {
            // Find the ComboBox in the visual tree
            var comboBox = this.FindName("tokenComboBox") as ComboBox;
            if (comboBox != null)
            {
                return comboBox.Focus();
            }
            return base.Focus();
        }
    }

    public class TokenRemovedEventArgs : EventArgs
    {
        public TokenControl RemovedToken { get; private set; }

        public TokenRemovedEventArgs(TokenControl token)
        {
            RemovedToken = token;
        }
    }
}
