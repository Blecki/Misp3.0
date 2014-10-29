using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public class RecordAtom : Atom
    {
        public override AtomType Type { get { return AtomType.Record; } }
        public RecordAtom Parent;
        public Dictionary<String, Atom> Variables = new Dictionary<String, Atom>();
        public override object GetSystemValue() { return this; }
        public override Atom Evaluate(EvaluationContext Context) { return new RecordAtom { Variables = new Dictionary<string, Atom>(Variables) }; }

        public bool TryGetValue(String Name, out Atom Value)
        {
            Value = null;
            if (Variables.TryGetValue(Name, out Value)) return true;
            if (Parent != null) return Parent.TryGetValue(Name, out Value);
            return false;
        }

        protected override void ImplementEmit(StringBuilder Into)
        {
            Into.Append("[");
            foreach (var value in Variables)
            {
                Into.Append("(");
                Into.Append(value.Key);
                Into.Append(" ");
                value.Value.Emit(Into);
                Into.Append(")");
            }
            Into.Append("]");
        }
    }
}
