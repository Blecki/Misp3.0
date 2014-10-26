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
        Paragraph OutputRoot = new Paragraph();

        public MainWindow()
        {
            InitializeComponent();
            TextBox_TextChanged(null, null);

            MISPLIB.Core.InitiateCore(s =>
            {
                OutputBox.AppendText(s);
                OutputBox.ScrollToEnd();
            });

            MISPLIB.Core.CoreFunctions.Add("recall", (args, c) =>
                {
                    if (args.Count != 2) throw new MISPLIB.EvaluationError("Incorrect number of arguments passed to recall.");
                    var func = args[1].Evaluate(c);
                    var builder = new StringBuilder();
                    func.Emit(builder);
                    InputBox.Text = builder.ToString();
                    return func;
                });

            MISPLIB.Core.CoreFunctions.Add("@", (args, c) =>
                {
                    return GlobalScope;
                });

            OutputBox.Document.Blocks.Add(OutputRoot);

            var buildVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var run = new Run("MISP 3.0 Build " + buildVersion + "\n") { Foreground = Brushes.Red };
            OutputRoot.Inlines.Add(run);
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
                var saveInput = InputBox.Text.Trim();

                try
                {
                    OutputRoot.Inlines.Add(new Run(saveInput + "\n") { Foreground = Brushes.Orange });
                    var parsedMisp = MISPLIB.Core.Parse(new MISPLIB.StringIterator(saveInput));
                    InputBox.Clear();

                    var evaluatedResult = MISPLIB.Core.Evaluate(parsedMisp, GlobalScope);
                    var outputBuilder = new StringBuilder();
                    evaluatedResult.Emit(outputBuilder);

                    OutputRoot.Inlines.Add(new Run(outputBuilder.ToString() + "\n") { Foreground = Brushes.ForestGreen });
                    OutputBox.ScrollToEnd();
                }
                catch (Exception x)
                {
                    OutputRoot.Inlines.Add(new Run(x.Message + "\n" + x.StackTrace + "\n") { Foreground = Brushes.Red });
                    InputBox.Text = saveInput;
                    OutputBox.ScrollToEnd();
                }

                e.Handled = true;
            }
        }
    }
}
