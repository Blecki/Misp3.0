using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public static partial class Core
    {
        internal static List<CoreFunction> CoreFunctions;
        
        public static void AddCoreFunction(String Declaration, Func<List<Atom>, EvaluationContext, Atom> Implementation)
        {
            var parsedDeclaration = Parse(new StringIterator("(" + Declaration + ")"));

            if (parsedDeclaration.Type != AtomType.List) throw new InvalidOperationException();
            var list = parsedDeclaration as ListAtom;
            if (list.Value[0].Type != AtomType.Token) throw new InvalidOperationException();
            if (list.Value[1].Type != AtomType.List) throw new InvalidOperationException();
            foreach (var arg in (list.Value[1] as ListAtom).Value)
                if (arg.Type != AtomType.Token || arg.Modifier == Modifier.Evaluate || arg.Modifier == Modifier.Expand)
                    throw new InvalidOperationException();
            
            var result = new CoreFunction
            {
                Implementation = Implementation,
                Name = (list.Value[0] as TokenAtom).Value,
                ArgumentNames = new List<TokenAtom>((list.Value[1] as ListAtom).Value.Select(a => a as TokenAtom)),
            };
            
            CoreFunctions.Add(result);
        }

        public static void InitiateCore(Action<String> StandardOutput)
        {
            CoreFunctions = new List<CoreFunction>();

            AddCoreFunction("parse (str)", (args, c) =>
                {
                    if (args[0].Type != AtomType.String) throw new EvaluationError("Expected string as first argument to parse.");
                    return Parse(new StringIterator((args[0] as StringAtom).Value));
                });

            AddCoreFunction("eval (atom)", (args, c) =>
                {
                    return args[0].Evaluate(c);
                });

            #region Basic Math

            AddCoreFunction("+ (+value)", (args,c) =>
            {
                var realArgs = (args[0] as ListAtom).Value;
                foreach (var v in realArgs) if (v.Type != AtomType.Integer && v.Type != AtomType.Decimal) throw new EvaluationError("Incorrect argument type passed to +");
                if (realArgs.Count(v => v.Type == AtomType.Decimal) > 0)
                {
                    var sum = 0.0f;
                    foreach (var v in realArgs)
                    {
                        if (v.Type == AtomType.Integer) sum += (v as IntegerAtom).Value;
                        else sum += (v as DecimalAtom).Value;
                    }
                    return new DecimalAtom { Value = sum };
                }
                else
                {
                    var sum = 0;
                    foreach (var v in realArgs) sum += (v as IntegerAtom).Value;
                    return new IntegerAtom { Value = sum };
                }
            });

            #endregion

            #region Output

            AddCoreFunction("print (+arg)", (args, c) =>
                {
                    var builder = new StringBuilder();
                    foreach (var v in (args[0] as ListAtom).Value)
                        v.Emit(builder);
                    StandardOutput(builder.ToString());
                    return new StringAtom { Value = builder.ToString() };
                });

            AddCoreFunction("format (string *arg)", (args, c) =>
                {
                    if (args[0].Type != AtomType.String) throw new EvaluationError("First argument to format is not a string.");
                    var s = (args[0] as StringAtom).Value;
                    var a = (args[1] as ListAtom).Value.Select(v => v.GetSystemValue()).ToArray();
                    var r = String.Format(s, a);
                    return new StringAtom { Value = r };
                });

            #endregion

            #region Functions

            AddCoreFunction("func ('args 'body)", (args, c) =>
                {
                    if (args[0].Type != AtomType.List) throw new EvaluationError("Expected list of argument names as first argument to func.");

                    var function = new FunctionAtom();
                    function.DeclarationScope = c.ActiveScope;
                    function.Implementation = args[1];
                    function.ArgumentNames = new List<TokenAtom>();

                    foreach (var argumentName in (args[0] as ListAtom).Value)
                    {
                        if (argumentName.Type != AtomType.Token) throw new EvaluationError("Malformed argument list in func.");
                        if (argumentName.Modifier == Modifier.Expand) throw new EvaluationError("Expand modifier illegal on argument name in func.");
                        if (argumentName.Modifier == Modifier.Evaluate) throw new EvaluationError("Evaluate modifier illegal on argument name in func.");
                        function.ArgumentNames.Add(argumentName as TokenAtom);
                    }

                    return function;
                });

            AddCoreFunction("set-decl-scope (func scope)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Function) throw new EvaluationError("Expected function as first argument to set-decl-scope.");
                    if (args[1].Type != AtomType.Record) throw new EvaluationError("Expected record as second argument to set-decl-scope.");
                    (args[0] as FunctionAtom).DeclarationScope = (args[1] as RecordAtom);
                    return args[0];
                });

            #endregion

            #region Scope And Memory

            AddCoreFunction("let ('name value)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Token) throw new EvaluationError("Expected argument name as first argument to let.");
                    c.ActiveScope.Variables.Upsert((args[0] as TokenAtom).Value, args[1]);
                    return args[1];
                });

            AddCoreFunction("with ('vars 'code)", (args, c) =>
                {
                    if (args[0].Type != AtomType.List) throw new EvaluationError("Expected variable set as first argument to with.");

                    var scope = new RecordAtom { Variables = new Dictionary<string, Atom>(), Parent = c.ActiveScope };
                    foreach (var variable in (args[0] as ListAtom).Value)
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
                        result = args[1].Evaluate(c);
                    }
                    finally
                    {
                        c.ActiveScope = scope.Parent;
                    }
                    return result;
                });

            #endregion

            #region Records

            AddCoreFunction("set (object 'name value)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Record) throw new EvaluationError("First argument to set must be a record.");

                    if (args[1].Type != AtomType.Token) throw new EvaluationError("Expected member name as second argument to set.");

                    (args[0] as RecordAtom).Variables.Upsert((args[1] as TokenAtom).Value, args[2]);
                    return args[2];
                });

            AddCoreFunction("multi-set (object '*pairs)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Record) throw new EvaluationError("Expected record as first argument to multi-set.");

                    foreach (var pair in (args[1] as ListAtom).Value)
                    {
                        if (pair.Type != AtomType.List) throw new EvaluationError("Expected lists as repeating arguments to multi-set.");
                        var list = pair as ListAtom;
                        if (list.Value.Count != 2) throw new EvaluationError("Expected pairs as repeating arguments to multi-set.");
                        if (list.Value[0].Type != AtomType.Token) throw new EvaluationError("Expected token as first value in pair as repeating arguments to multi-set.");
                        var value = list.Value[1].Evaluate(c);
                        (args[0] as RecordAtom).Variables.Upsert((list.Value[0] as TokenAtom).Value, value);
                    }

                    return args[0];
                });

            AddCoreFunction("get (object 'name)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Record) throw new EvaluationError("First argument to get must be a record.");
                    if (args[1].Type != AtomType.Token) throw new EvaluationError("Expected member name as second argument to get.");

                    Atom value;
                    if ((args[0] as RecordAtom).Variables.TryGetValue((args[1] as TokenAtom).Value, out value))
                        return value;
                    else
                        throw new EvaluationError("Member not found on record.");
                });

            #endregion

            #region Lists

            AddCoreFunction("list (*values)", (args, c) =>
                {
                    return args[0]; // :D!!!!
                });

            AddCoreFunction("length (list-or-string)", (args, c) =>
                {
                    if (args[0].Type == AtomType.List) return new IntegerAtom { Value = (args[0] as ListAtom).Value.Count };
                    else if (args[0].Type == AtomType.String) return new IntegerAtom { Value = (args[0] as StringAtom).Value.Length };
                    else
                        throw new EvaluationError("Expected list or string as first argument to length.");
                });

            AddCoreFunction("index-get (list index)", (args, c) =>
                {
                    if (args[1].Type != AtomType.Integer) throw new EvaluationError("Expected integer index as second argument to index-get.");
                    if (args[0].Type == AtomType.List) return (args[0] as ListAtom).Value[(args[1] as IntegerAtom).Value];
                    else if (args[0].Type == AtomType.String) return new IntegerAtom { Value = (args[0] as StringAtom).Value[(args[1] as IntegerAtom).Value]};
                    else throw new EvaluationError("Expected list or string as first argument to index-get.");
                });

            AddCoreFunction("replace-at (list index value)", (args, c) =>
            {
                if (args[1].Type != AtomType.Integer) throw new EvaluationError("Expected integer index as second argument to replace-at.");
                if (args[0].Type != AtomType.List) throw new EvaluationError("Expected list as first argument to replace-at.");
                var r = new ListAtom { Value = new List<Atom>((args[0] as ListAtom).Value) };
                r.Value[(args[1] as IntegerAtom).Value] = args[2];
                return r;
            });

            AddCoreFunction("array (count 'code)", (args, c) =>
                {
                    var count = args[0];
                    if (count.Type != AtomType.Integer) throw new EvaluationError("Expected integer count as first argument to array.");

                    var r = new ListAtom() { Value = new List<Atom>() };
                    for (int i = 0; i < (count as IntegerAtom).Value; ++i)
                        r.Value.Add(args[1].Evaluate(c));
                    return r;
                });

            AddCoreFunction("last (+list)", (args, c) =>
                {
                    return (args[0] as ListAtom).Value.Last();
                });

            #endregion

            #region Serialization

            AddCoreFunction("serialize (record)", (args, c) =>
                {
                    if (args[0].Type != AtomType.Record) throw new EvaluationError("Expect record as first argument to serialize.");
                    var serializer = new SerializationContext();
                    var builder = new StringBuilder();
                    serializer.Serialize(args[0] as RecordAtom, builder);
                    return new StringAtom { Value = builder.ToString() };
                });

            AddCoreFunction("to-int (value)", (args, c) =>
                {
                    if (args[0].Type == AtomType.Integer) return args[0];
                    else if (args[0].Type == AtomType.Decimal) return new IntegerAtom { Value = (int)(args[0] as DecimalAtom).Value };
                    else if (args[0].Type == AtomType.String)
                    {
                        int v = 0;
                        if (Int32.TryParse((args[0] as StringAtom).Value, out v))
                            return new IntegerAtom { Value = v };
                    }
                    throw new EvaluationError("Could not convert value to integer.");
                });

            #endregion

            #region Files

            AddCoreFunction("write-all (file text)", (args, c) =>
                {
                    if (args[0].Type != AtomType.String) throw new EvaluationError("Expected string as first argument to write-all.");
                    if (args[1].Type != AtomType.String) throw new EvaluationError("Expected string as second argument to write-all.");

                    System.IO.File.WriteAllText((args[0] as StringAtom).Value, (args[1] as StringAtom).Value);
                    return args[1];
                });

            AddCoreFunction("read-all (file)", (args, c) =>
                {
                    if (args[0].Type != AtomType.String) throw new EvaluationError("Expected string as first argument to read-all.");
                    return new StringAtom { Value = System.IO.File.ReadAllText((args[0] as StringAtom).Value) };
                });

            #endregion
        }
    }
}
