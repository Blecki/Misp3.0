﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public class SerializationContext
    {
        private List<RecordAtom> AtomsToWrite;
        private List<FunctionAtom> Functions;

        public void Serialize(RecordAtom RootAtom, StringBuilder Into)
        {
            if (RootAtom.Type != AtomType.Record) throw new InvalidOperationException();

            PrepareForSerialization(RootAtom);

            Into.AppendFormat("(with (\n  (atoms (array {0} []))\n   (funcs '(\n", AtomsToWrite.Count);
            foreach (var func in Functions)
            {
                Into.Append("      (func (");
                Into.Append(String.Join(" ", func.ArgumentNames));
                Into.Append(") ");
                func.Implementation.Emit(Into);
                Into.Append(")\n");
            }
            Into.Append("   )))\n\n   (last\n");
            for (int i = 0; i < Functions.Count; ++i)
            {
                Into.AppendFormat("      (set-decl-scope (index-get funcs {0}) (index-get atoms {1}))\n", i, AtomsToWrite.IndexOf(Functions[i].DeclarationScope));
            }
            Into.Append("\n");
            for (int i = 0; i < AtomsToWrite.Count; ++i)
            {
                Into.Append("      (multi-set (index-get atoms " + i + ") ");
                SerializeRecordMembers(AtomsToWrite[i], Into);
                Into.Append(")\n");
            }
            Into.AppendFormat("\n\n      (index-get atoms {0})\n   )\n)", AtomsToWrite.Count - 1);
        }

        private void SerializeRecordMembers(RecordAtom Atom, StringBuilder Into)
        {
            foreach (var pair in Atom.Variables)
            {
                Into.Append("(" + pair.Key + " ");
                SerializeAtom(pair.Value, Into);
                Into.Append(")");
            }
        }

        private void SerializeAtom(Atom Atom, StringBuilder Into)
        {
            switch (Atom.Type)
            {
                case AtomType.Decimal:
                    Into.AppendFormat("{0:0.0########}", (Atom as DecimalAtom).Value);
                    break;
                case AtomType.Integer:
                    Into.Append((Atom as IntegerAtom).Value);
                    break;
                case AtomType.Token:
                    Into.Append((Atom as TokenAtom).Value);
                    break;
                case AtomType.String:
                    Into.Append("\"" + (Atom as StringAtom).Value + "\"");
                    break;
                case AtomType.List:
                    Into.Append("'(");
                    foreach (var item in (Atom as ListAtom).Value)
                    {
                        SerializeAtom(item, Into);
                        Into.Append(" ");
                    }
                    Into.Append(")");
                    break;
                case AtomType.Record:
                    Into.AppendFormat("(index-get atoms {0})", AtomsToWrite.IndexOf(Atom as RecordAtom));
                    break;
                case AtomType.Function:
                    Into.AppendFormat("(index-get funcs {0})", Functions.IndexOf(Atom as FunctionAtom));
                    break;
            }
        }

        private void PrepareForSerialization(Atom RootAtom)
        {
            AtomsToWrite = new List<RecordAtom>();
            Functions = new List<FunctionAtom>();

            __EnumerateAtomsForSerialization(RootAtom);
        }

        private void __EnumerateAtomsForSerialization(Atom Atom)
        {
            switch (Atom.Type)
            {
                case AtomType.Decimal:
                case AtomType.Integer:
                case AtomType.String:
                case AtomType.Token:
                    return;
                case AtomType.List:
                    foreach (var particle in (Atom as ListAtom).Value)
                        __EnumerateAtomsForSerialization(particle);
                    break;
                case AtomType.Record:
                    if (AtomsToWrite.Contains(Atom)) return;
                    AtomsToWrite.Add(Atom as RecordAtom);
                        foreach (var memberPair in (Atom as RecordAtom).Variables)
                            __EnumerateAtomsForSerialization(memberPair.Value);
                    break;
                case AtomType.Function:
                    if (Functions.Contains(Atom)) return;
                    Functions.Add(Atom as FunctionAtom);
                    __EnumerateAtomsForSerialization((Atom as FunctionAtom).DeclarationScope);
                    __EnumerateAtomsForSerialization((Atom as FunctionAtom).Implementation);
                    break;
            }
        }

        
    }
}
