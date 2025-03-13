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
            
            // Add key down handler for keyboard navigation
            this.KeyDown += TokenControl_KeyDown;
            
            // Make sure the ComboBox can receive keyboard focus
            if (this.FindName("tokenComboBox") is ComboBox comboBox)
            {
                comboBox.KeyDown += TokenComboBox_KeyDown;
                comboBox.PreviewKeyDown += TokenComboBox_PreviewKeyDown;
            }
        }

        private void TokenControl_KeyDown(object sender, KeyEventArgs e)
        {
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

        private void TokenComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle keyboard navigation
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                // Find the parent TokenizerControl
                var parent = FindParent<RuleEditor.ViewModels.Version2.TokenizerControl>(this);
                if (parent != null)
                {
                    // Navigate to the next token or input box
                    parent.NavigateToNextToken(this);
                    e.Handled = true;
                }
            }
        }

        private void TokenComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle keyboard navigation
            if (e.Key == Key.Left)
            {
                // Get the TextBox inside the ComboBox
                var comboBox = sender as ComboBox;
                var textBox = comboBox?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                
                // If at the beginning of the text, navigate to the previous token
                if (textBox != null && textBox.CaretIndex == 0)
                {
                    var parent = FindParent<RuleEditor.ViewModels.Version2.TokenizerControl>(this);
                    if (parent != null)
                    {
                        parent.NavigateToPreviousToken(this);
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Right)
            {
                // Get the TextBox inside the ComboBox
                var comboBox = sender as ComboBox;
                var textBox = comboBox?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                
                // If at the end of the text, navigate to the next token
                if (textBox != null && textBox.CaretIndex == textBox.Text.Length)
                {
                    var parent = FindParent<RuleEditor.ViewModels.Version2.TokenizerControl>(this);
                    if (parent != null)
                    {
                        parent.NavigateToNextToken(this);
                        e.Handled = true;
                    }
                }
            }
        }
        
        // Helper method to find a parent of a specific type
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            
            if (parentObject == null)
                return null;
                
            if (parentObject is T parent)
                return parent;
                
            return FindParent<T>(parentObject);
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
