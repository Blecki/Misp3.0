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

            #region Functions

            CoreFunctions.Add("func", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of args passed to func.");
                    if (args[1].Type != AtomType.List) throw new EvaluationError("Expected list of argument names as first argument to func.");

                    var function = new FunctionAtom();
                    function.DeclarationScope = c.ActiveScope;
                    function.Implementation = args[2];
                    function.ArgumentNames = new List<String>();

                    foreach (var argumentName in (args[1] as ListAtom).Value)
                    {
                        if (argumentName.Type != AtomType.Token) throw new EvaluationError("Malformed argument list in func.");
                        function.ArgumentNames.Add((argumentName as TokenAtom).Value);
                    }

                    return function;
                });

            #endregion

            #region Scope And Memory

            CoreFunctions.Add("let", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of arguments passed to let.");
                    if (args[1].Type != AtomType.Token) throw new EvaluationError("Expected argument name as first argument to let.");
                    var value = args[2].Evaluate(c);
                    c.ActiveScope.Variables.Upsert((args[1] as TokenAtom).Value, value);
                    return value;
                });

            CoreFunctions.Add("with", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of arguments passed to with.");
                    if (args[1].Type != AtomType.List) throw new EvaluationError("Expected variable set as first argument to with.");

                    var scope = new RecordAtom { Variables = new Dictionary<string, Atom>(), Parent = c.ActiveScope };
                    foreach (var variable in (args[1] as ListAtom).Value)
                    {
                        if (variable.Type != AtomType.List || (variable as ListAtom).Value.Count != 2) throw new EvaluationError("Expected pairs in variable set in first argument to with.");
                        var name = (variable as ListAtom).Value[0];
                        var value = (variable as ListAtom).Value[1];
                        if (name.Type != AtomType.Token) throw new EvaluationError("Expected variable name in pair in first argument to with.");
                        scope.Variables.Upsert((name as TokenAtom).Value, value.Evaluate(c));
                    }

                    c.ActiveScope = scope;
                    Atom result = null;
                    try
                    {
                        result = args[2].Evaluate(c);
                    }
                    finally
                    {
                        c.ActiveScope = scope.Parent;
                    }
                    return result;
                });

            #endregion

            #region Records

            CoreFunctions.Add("set", (args, c) =>
                {
                    if (args.Count != 4) throw new EvaluationError("Incorrect number of arguments passed to set.");

                    var obj = args[1].Evaluate(c);
                    if (obj.Type != AtomType.Record) throw new EvaluationError("First argument to set must be a record.");

                    if (args[2].Type != AtomType.Token) throw new EvaluationError("Expected member name as second argument to set.");

                    var value = args[3].Evaluate(c);

                    (obj as RecordAtom).Variables.Upsert((args[2] as TokenAtom).Value, value);
                    return value;
                });

            CoreFunctions.Add("get", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of arguments passed to get.");

                    var obj = args[1].Evaluate(c);
                    if (obj.Type != AtomType.Record) throw new EvaluationError("First argument to set must be a record.");

                    if (args[2].Type != AtomType.Token) throw new EvaluationError("Expected member name as second argument to set.");

                    Atom value;
                    if ((obj as RecordAtom).Variables.TryGetValue((args[2] as TokenAtom).Value, out value))
                        return value;
                    else
                        throw new EvaluationError("Member not found on record.");
                });

            #endregion

            #region Lists

            CoreFunctions.Add("length", (args, c) =>
                {
                    if (args.Count != 2) throw new EvaluationError("Incorrect number of arguments passed to length.");
                    var l = PrepareStandardArgumentList(args, c);
                    if (l[0].Type == AtomType.List) return new IntegerAtom { Value = (l[0] as ListAtom).Value.Count };
                    else if (l[0].Type == AtomType.String) return new IntegerAtom { Value = (l[0] as StringAtom).Value.Length };
                    else
                        throw new EvaluationError("Expected list or string as first argument to length.");
                });

            CoreFunctions.Add("index-get", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of arguments passed to index-get.");
                    var l = PrepareStandardArgumentList(args, c);
                    if (l[1].Type != AtomType.Integer) throw new EvaluationError("Expected integer index as second argument to index-get.");
                    if (l[0].Type == AtomType.List) return (l[0] as ListAtom).Value[(l[1] as IntegerAtom).Value];
                    else if (l[0].Type == AtomType.String) return new IntegerAtom { Value = (l[0] as StringAtom).Value[(l[1] as IntegerAtom).Value]};
                    else throw new EvaluationError("Expected list or string as first argument to index-get.");
                });

            CoreFunctions.Add("replace-at", (args, c) =>
            {
                if (args.Count != 4) throw new EvaluationError("Incorrect number of arguments passed to replace-at.");
                var l = PrepareStandardArgumentList(args, c);
                if (l[1].Type != AtomType.Integer) throw new EvaluationError("Expected integer index as second argument to replace-at.");
                if (l[0].Type != AtomType.List) throw new EvaluationError("Expected list as first argument to replace-at.");
                var r = new ListAtom { Value = new List<Atom>((l[0] as ListAtom).Value) };
                r.Value[(l[1] as IntegerAtom).Value] = l[2];
                return r;
            });

            CoreFunctions.Add("array", (args, c) =>
                {
                    if (args.Count != 3) throw new EvaluationError("Incorrect number of arguments passed to array.");
                    var count = args[1].Evaluate(c);
                    if (count.Type != AtomType.Integer) throw new EvaluationError("Expected integer count as first argument to array.");

                    var r = new ListAtom() { Value = new List<Atom>() };
                    for (int i = 0; i < (count as IntegerAtom).Value; ++i)
                        r.Value.Add(args[2].Evaluate(c));
                    return r;
                });

            #endregion

            #region Serialization

            CoreFunctions.Add("serialize", (args, c) =>
                {
                    var l = PrepareStandardArgumentList(args, c);
                    if (l.Count != 1) throw new EvaluationError("Incorrect number of arguments passed to serialize.");
                    if (l[0].Type != AtomType.Record) throw new EvaluationError("Expect record as first argument to serialize.");
                    var serializer = new SerializationContext();
                    var builder = new StringBuilder();
                    serializer.Serialize(l[0] as RecordAtom, builder);
                    return new StringAtom { Value = builder.ToString() };
                });

            #endregion

            #region Files

            CoreFunctions.Add("write-all", (args, c) =>
                {
                    var l = PrepareStandardArgumentList(args, c);
                    if (l.Count != 2) throw new EvaluationError("Incorrect number of arguments passed to write-all.");
                    if (l[0].Type != AtomType.String) throw new EvaluationError("Expected string as first argument to write-all.");
                    if (l[1].Type != AtomType.String) throw new EvaluationError("Expected string as second argument to write-all.");

                    System.IO.File.WriteAllText((l[0] as StringAtom).Value, (l[1] as StringAtom).Value);
                    return l[1];
                });

            #endregion
        }
    }
}
