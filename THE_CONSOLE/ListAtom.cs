using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public class ListAtom : Atom
    {
        public List<Atom> Value;
        public override AtomType Type { get { return AtomType.List; } }

        protected override void ImplementEmit(StringBuilder Into)
        {
            Into.Append("(");
            for (int i = 0; i < Value.Count; ++i)
            {
                Value[i].Emit(Into);
                if (i < (Value.Count - 1)) Into.Append(" ");
            }
            Into.Append(")");
        }

        public override object GetSystemValue() { return this; }

        public override Atom Evaluate(EvaluationContext Context)
        {
            if (Modifier == MISPLIB.Modifier.Quote) return new ListAtom { Value = Value, Modifier = MISPLIB.Modifier.None };

            if (Value.Count == 0) throw new EvaluationError("Can't evaluate empty list.");

            if (Value[0].Type == AtomType.Token)
            {
                var functionToken = Value[0] as TokenAtom;

                if (Core.CoreFunctions.ContainsKey(functionToken.Value))
                {
                    var argList = new List<Atom>();
                    argList.Add(Value[0]);
                    for (int i = 1; i < Value.Count; ++i)
                    {
                        if (Value[i].Modifier == MISPLIB.Modifier.Evaluate)
                            argList.Add(Value[i].Evaluate(Context));
                        else
                            argList.Add(Value[i]);
                    }
                    return Core.CoreFunctions[functionToken.Value](argList, Context);
                }
            }

            var firstAtom = Value[0].Evaluate(Context);
            if (firstAtom.Type != AtomType.Function) throw new EvaluationError("First atom in evaluated list must be a function atom.");
            var function = firstAtom as FunctionAtom;

            var argumentList = new List<Atom>();

            for (int i = 1; i < Value.Count; ++i)
            {
                var sourceArgument = Value[i];

                if (argumentList.Count >= function.ArgumentNames.Count) throw new EvaluationError("Too many arguments to function.");

                if (function.ArgumentNames[argumentList.Count].Modifier == MISPLIB.Modifier.Quote)
                {
                    if (sourceArgument.Modifier == MISPLIB.Modifier.Expand) throw new EvaluationError("Expanding arguments cannot be passed to quoted arguments.");
                    else if (sourceArgument.Modifier == MISPLIB.Modifier.Evaluate)
                        argumentList.Add(sourceArgument.Evaluate(Context));
                    else
                        argumentList.Add(sourceArgument);
                }
                else
                {
                    var argument = sourceArgument.Evaluate(Context);

                    if (sourceArgument.Modifier == MISPLIB.Modifier.Evaluate)
                        argumentList.Add(argument.Evaluate(Context));
                    else if (sourceArgument.Modifier == MISPLIB.Modifier.Expand && argument.Type == AtomType.List)
                        argumentList.AddRange((argument as ListAtom).Value);
                    else
                        argumentList.Add(argument);
                }
            }

            if (argumentList.Count != function.ArgumentNames.Count) throw new EvaluationError("Incorrect number of arguments passed to function.");

            var saveScope = Context.ActiveScope;
            Context.ActiveScope = function.DeclarationScope;
            for (int i = 0; i < function.ArgumentNames.Count; ++i)
                Context.ActiveScope.Variables.Upsert(function.ArgumentNames[i].Value, argumentList[i]);
            var result = function.Implementation.Evaluate(Context);
            Context.ActiveScope = saveScope;

            return result;
        }
    }
}
