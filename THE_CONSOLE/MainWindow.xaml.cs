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

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MISPLIB.RecordAtom GlobalScope = new MISPLIB.RecordAtom();

        public MainWindow()
        {
            InitializeComponent();
            TextBox_TextChanged(null, null);

            MISPLIB.Core.InitiateCore(s =>
            {
                OutputBox.AppendText(s);
                OutputBox.ScrollToEnd();
            });
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var realFontSize = this.InputBox.FontSize * this.InputBox.FontFamily.LineSpacing;
            var adjustedLineCount = System.Math.Max(this.InputBox.LineCount, 1) + 1;
            var newHeight = realFontSize * adjustedLineCount;
            if (newHeight > this.ActualHeight * 0.75f) newHeight = this.ActualHeight * 0.75f;
            if (newHeight < realFontSize * 2) newHeight = realFontSize * 2;
            this.BottomRow.Height = new GridLength(newHeight);
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Return)
            {
                try
                {
                    OutputBox.AppendText(InputBox.Text + "\n");
                    var parsedMisp = MISPLIB.Core.Parse(new MISPLIB.StringIterator(InputBox.Text));
                    InputBox.Clear();

                    var evaluatedResult = MISPLIB.Core.Evaluate(parsedMisp, GlobalScope);
                    var outputBuilder = new StringBuilder();
                    evaluatedResult.Emit(outputBuilder);
                    OutputBox.AppendText(outputBuilder.ToString() + "\n");
                    OutputBox.ScrollToEnd();
                }
                catch (Exception x)
                {
                    OutputBox.AppendText(x.Message);
                }

                e.Handled = true;
            }
        }
    }
}
