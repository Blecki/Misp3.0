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
            String OpenFilePath = null;

            this.Title = "New Environment";

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

            MISPLIB.Core.CoreFunctions.Add("save", (args, c) =>
                {
                    var l = MISPLIB.Core.PrepareStandardArgumentList(args, c);
                    String saveFileName;
                    if (l.Count == 1)
                    {
                        if (l[0].Type != MISPLIB.AtomType.String) throw new MISPLIB.EvaluationError("Expected string as first argument to save.");
                        saveFileName = (l[0] as MISPLIB.StringAtom).Value;
                    }
                    else if (l.Count == 0)
                    {
                        if (String.IsNullOrEmpty(OpenFilePath))
                            throw new MISPLIB.EvaluationError("This environment has never been saved. Please supply a filename.");
                        saveFileName = OpenFilePath;
                    }
                    else
                        throw new MISPLIB.EvaluationError("Incorrect number of arguments passed to save.");

                    var serializer = new MISPLIB.SerializationContext();
                    var builder = new StringBuilder();
                    serializer.Serialize(GlobalScope, builder);

                    var dirName = System.IO.Path.GetDirectoryName(saveFileName);
                    if (!String.IsNullOrEmpty(dirName))
                        System.IO.Directory.CreateDirectory(dirName);

                    System.IO.File.WriteAllText(saveFileName, builder.ToString());
                    OpenFilePath = saveFileName;
                    this.Title = OpenFilePath;
                    return new MISPLIB.StringAtom { Value = OpenFilePath };
                });

            MISPLIB.Core.CoreFunctions.Add("load", (args, c) =>
                {
                    var l = MISPLIB.Core.PrepareStandardArgumentList(args, c);
                    if (l.Count != 1) throw new MISPLIB.EvaluationError("Incorrect number of arguments passed to load.");
                    if (l[0].Type != MISPLIB.AtomType.String) throw new MISPLIB.EvaluationError("Expected path as first argument to load.");

                    var text = System.IO.File.ReadAllText((l[0] as MISPLIB.StringAtom).Value);
                    var parsed = MISPLIB.Core.Parse(new MISPLIB.StringIterator(text));
                    var result = MISPLIB.Core.Evaluate(parsed, GlobalScope);
                    if (result.Type != MISPLIB.AtomType.Record) throw new MISPLIB.EvaluationError("Loading of file did not produce record.");
                    OpenFilePath = (l[0] as MISPLIB.StringAtom).Value;
                    this.Title = OpenFilePath;
                    GlobalScope = result as MISPLIB.RecordAtom;
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
                var code = "(" + saveInput + ")";

                try
                {
                    OutputRoot.Inlines.Add(new Run(code + "\n") { Foreground = Brushes.Orange });
                    var parsedMisp = MISPLIB.Core.Parse(new MISPLIB.StringIterator(code));
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
