using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MISPLIB
{
    public class ParseError : Exception
    {
        public int Place = 0;

        public ParseError(String Message, int Place) : base(Message)
        {
            this.Place = Place;
        }
    }

    public static partial class Core
    {
        public static Atom Parse(StringIterator Iterator)
        {
            var result = ParseAtom(Iterator);
            if (!Iterator.AtEnd) throw new ParseError("Did not consume all input", Iterator.place);
            return result;
        }

        private static void SkipWhitespace(this StringIterator Iterator)
        {
            while (!Iterator.AtEnd && " \t\r\n".Contains(Iterator.Next))
                Iterator.Advance();
            if (Iterator.AtEnd)
                throw new ParseError("Unexpected end of input", Iterator.place);
        }

        private static Atom ParseAtom(StringIterator Iterator)
        {
            if (Iterator.AtEnd) return null;

            var modifier = Modifier.None;
            Atom result = null;

            if (Iterator.Next == '\'')
            {
                Iterator.Advance();
                modifier = Modifier.Quote;
            }
            else if (Iterator.Next == '$')
            {
                Iterator.Advance();
                modifier = Modifier.Expand;
            }
            else if (Iterator.Next == ':')
            {
                Iterator.Advance();
                modifier = Modifier.Evaluate;
            }

            if (Iterator.AtEnd) throw new ParseError("Unexpected end of input", Iterator.place);

            if (Iterator.Next == '(')
            {
                Iterator.Advance();
                Iterator.SkipWhitespace();

                var list = new ListAtom { Value = new List<Atom>() };
                
                while (!Iterator.AtEnd && Iterator.Next != ')')
                {
                    list.Value.Add(ParseAtom(Iterator));
                    Iterator.SkipWhitespace();
                }

                Iterator.Advance();
                result = list;
            }
            else if (Iterator.Next == '[')
            {
                if (modifier != Modifier.None) throw new ParseError("Modifiers not allowed on record.", Iterator.place);

                Iterator.Advance();
                Iterator.SkipWhitespace();

                var record = new RecordAtom();
                while (!Iterator.AtEnd && Iterator.Next != ']')
                {
                    var listItem = ParseAtom(Iterator) as ListAtom;
                    if (listItem == null) throw new ParseError("Expected list inside record", Iterator.place);
                    if (listItem.Value.Count != 2) throw new ParseError("Malformed record", Iterator.place);
                    var entryName = listItem.Value[0] as TokenAtom;
                    if (entryName == null) throw new ParseError("Malformed record", Iterator.place);
                    record.Variables.Upsert(entryName.Value, listItem.Value[1]);
                }

                Iterator.Advance();
                result = record;
            }
            else if ("0123456789-".Contains(Iterator.Next))
            {
                if (modifier != Modifier.None) throw new ParseError("Modifiers not allowed on literals.", Iterator.place);
                result = ParseNumber(Iterator);
            }
            else if (Iterator.Next == '\"')
            {
                if (modifier != Modifier.None) throw new ParseError("Modifiers not allowed on literals.", Iterator.place);
                result = ParseString(Iterator);
            }
            else
            {
                result = ParseToken(Iterator);
                if (result.Type == AtomType.Token && (result as TokenAtom).Value == "nil")
                    result = new NilAtom();
            }

            result.Modifier = modifier;
            
            return result;
        }

        private static Atom ParseString(StringIterator Iterator)
        {
            var stringValue = "";
            Iterator.Advance();
            while (!Iterator.AtEnd && Iterator.Next != '\"')
            {
                stringValue += Iterator.Next;
                Iterator.Advance();

                //Todo: Escape chars.
            }

            if (Iterator.AtEnd) throw new ParseError("Unexpected end of input in string literal.", Iterator.place);

            Iterator.Advance();

            if (!Iterator.AtEnd && !" \t\n\r)".Contains(Iterator.Next))
                throw new ParseError("Expected end of token", Iterator.place);

            return new StringAtom { Value = stringValue };
        }

        private static Atom ParseToken(StringIterator Iterator)
        {
            var tokenValue = "";
            while (!Iterator.AtEnd && !" \t\r\n()".Contains(Iterator.Next))
            {
                tokenValue += Iterator.Next;
                Iterator.Advance();
            }
            return new TokenAtom { Value = tokenValue };
        }

        private static Atom ParseNumber(StringIterator Iterator)
        {
            var numberString = "";
            bool decimalFound = false;
            bool negate = false;

            if (Iterator.Next == '-')
            {
                negate = true;
                Iterator.Advance();
            }

            if (Iterator.AtEnd || !"0123456789".Contains(Iterator.Next))
            {
                Iterator.Rewind();
                return ParseToken(Iterator);
            }

            while (!Iterator.AtEnd && "0123456789".Contains(Iterator.Next))
            {
                numberString += Iterator.Next;
                Iterator.Advance();
            }

            if (!Iterator.AtEnd && Iterator.Next == '.')
            {
                decimalFound = true;
                numberString += '.';
                Iterator.Advance();

                while (!Iterator.AtEnd && "0123456789".Contains(Iterator.Next))
                {
                    numberString += Iterator.Next;
                    Iterator.Advance();
                }
            }

            if (!Iterator.AtEnd && !" \t\n\r)".Contains(Iterator.Next))
                throw new ParseError("Expected end of token", Iterator.place);

            if (decimalFound)
            {
                var number = Convert.ToSingle(numberString);
                if (negate) number = -number;
                return new DecimalAtom { Value = number };
            }
            else
            {
                var number = Convert.ToInt32(numberString);
                if (negate) number = -number;
                return new IntegerAtom { Value = number };
            }
        }
    }
}
