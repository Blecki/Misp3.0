using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public static partial class Core
    {
        internal static Dictionary<String, Func<List<Atom>, EvaluationContext, Atom>> CoreFunctions;

        private static List<Atom> PrepareStandardArgumentList(List<Atom> Arguments, EvaluationContext Context)
        {
            var result = new List<Atom>();
            for (int i = 1; i < Arguments.Count; ++i)
            {
                var evaluatedAtom = Arguments[i].Evaluate(Context);
                if (Arguments[i].Modifier == MISPLIB.Modifier.Expand && evaluatedAtom is ListAtom)
                    result.AddRange((evaluatedAtom as ListAtom).Value);
                else
                    result.Add(evaluatedAtom);
            }
            return result;
        }

        public static void InitiateCore(Action<String> StandardOutput)
        {
            CoreFunctions = new Dictionary<String, Func<List<Atom>, EvaluationContext, Atom>>();

            #region Basic Math

            CoreFunctions.Add("+", (args,c) =>
            {
                var l = PrepareStandardArgumentList(args, c);
                foreach (var v in l) if (v.Type != AtomType.Integer && v.Type != AtomType.Decimal) throw new EvaluationError("Incorrect argument type passed to +");
                if (l.Count(v => v.Type == AtomType.Decimal) > 0)
                {
                    var sum = 0.0f;
                    foreach (var v in l)
                    {
                        if (v.Type == AtomType.Integer) sum += (v as IntegerAtom).Value;
                        else sum += (v as DecimalAtom).Value;
                    }
                    return new DecimalAtom { Value = sum };
                }
                else
                {
                    var sum = 0;
                    foreach (var v in l) sum += (v as IntegerAtom).Value;
                    return new IntegerAtom { Value = sum };
                }
            });

            #endregion

            #region Output

            CoreFunctions.Add("print", (args, c) =>
                {
                    var l = PrepareStandardArgumentList(args, c);
                    var builder = new StringBuilder();
                    foreach (var v in l)
                        v.Emit(builder);
                    StandardOutput(builder.ToString());
                    return new StringAtom { Value = builder.ToString() };
                });

            CoreFunctions.Add("format", (args, c) =>
                {
                    var l = PrepareStandardArgumentList(args, c);
                    if (l.Count == 0) throw new EvaluationError("Not enough arguments passed to format.");
                    if (l[0].Type != AtomType.String) throw new EvaluationError("First argument to format is not a string.");
                    var s = (l[0] as StringAtom).Value;
                    var a = l.GetRange(1, l.Count - 1).Select(v => v.GetSystemValue()).ToArray();
                    var r = String.Format(s, a);
                    return new StringAtom { Value = r };
                });

            #endregion
        }
    }
}
