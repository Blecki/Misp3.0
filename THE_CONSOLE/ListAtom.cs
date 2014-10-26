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
                    return Core.CoreFunctions[functionToken.Value](Value, Context);
            }

            var argumentList = new List<Atom>();
            foreach (var atom in Value)
            {
                var evaluatedAtom = atom.Evaluate(Context);
                if (atom.Modifier == MISPLIB.Modifier.Expand && evaluatedAtom is ListAtom)
                    argumentList.AddRange((evaluatedAtom as ListAtom).Value);
                else
                    argumentList.Add(evaluatedAtom);
            }

            if (argumentList[0].Type != AtomType.Function) throw new EvaluationError("First item in list is not a function.");

            var function = argumentList[0] as FunctionAtom;
            if (argumentList.Count != function.ArgumentNames.Count + 1) throw new EvaluationError("Function argument count mismatch.");

            var saveScope = Context.ActiveScope;
            Context.ActiveScope = function.DeclarationScope;
            for (int i = 0; i < function.ArgumentNames.Count; ++i)
                Context.ActiveScope.Variables.Upsert(function.ArgumentNames[i], argumentList[i + 1]);
            var result = function.Implementation.Evaluate(Context);
            Context.ActiveScope = saveScope;

            return result;
        }
    }
}
