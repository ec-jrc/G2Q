using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QlikView.Qvx.QvxLibrary;

namespace QvGamsConnector
{
    public static class QueryExtractor
    {
        private static readonly char[] blankChars = { ' ', '\t', '\r', '\n' };
        private static readonly char[] lineBreakChars = { '\r', '\n' };

        /// <summary>
        /// Extracts the different components from the query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="fields"></param>
        /// <param name="table"></param>
        /// <param name="file"></param>
        /// <param name="where"></param>
        public static void ExtractQueryDetails(string query, out List<string> fields, out string table, out string file, out List<WhereCondition> where)
        {
            int index = 0;
            fields = collectSelect(query, ref index);
            collectFrom(query, ref index, out table, out file);
            where = collectWhere(query, ref index);
        }

        private static List<string> collectSelect(string text, ref int index)
        {
            string section = "SELECT";
            var fields = new List<string>();

            consumeExpectedText(text, "select", section, true, ref index);

            // collect first field
            do
            {
                fields.Add(readFieldName(text, section, ref index));
            }
            while (text.Length > index && consumeExpectedText(text, ",", section, false, ref index));
            return fields;
        }

        private static string readFieldName(string text, string section, ref int index)
        {
            consumeBlank(text, ref index);
            if (text[index].Equals('"'))
            {
                // field between double quotes
                return collectBetweenQuotes(text, section, ref index);
            }
            else
            {
                // field without double quotes
                char[] until = blankChars.Concat(new char[] { ',', '=' }).ToArray();
                return collectUntil(text, section, ref index, until);
            }
        }

        private static string collectFrom(string text, ref int index, out string table, out string file)
        {
            string section = "FROM";
            consumeExpectedText(text, "from", section, true, ref index);
            string from = collectBetweenQuotes(text, section, ref index).Trim();

            int auxIndex = 0;
            table = collectUntil(from, section, ref auxIndex, ' ').Trim();
            consumeExpectedText(from, "<", section, true, ref auxIndex);
            file = collectUntil(from, section, ref auxIndex, '>').Trim();

            return from;
        }

        private static List<WhereCondition> collectWhere(string text, ref int index)
        {
            List<WhereCondition> result = new List<WhereCondition>();
            string section = "WHERE";

            consumeBlank(text, ref index);
            if (text.Length > index && consumeExpectedText(text, "where", section, false, ref index))
            {
                do
                {
                    result.Add(collectWhereCondition(text, section, ref index));
                    consumeBlank(text, ref index);
                }
                while (text.Length > index && consumeExpectedText(text, "and", section, false, ref index));
            }

            return result;
        }

        private static WhereCondition collectWhereCondition(string text, string section, ref int index)
        {
            string field = readFieldName(text, section, ref index);
            consumeExpectedText(text, "=", section, true, ref index);
            string value = readWhereValue(text, section, ref index);
            return new WhereCondition { Field = field, Value = value };
        }

        private static string readWhereValue(string text, string section, ref int index)
        {
            char[] possibleOptions = new char[] { '"', '\'' };
            consumeBlank(text, ref index);
            if(possibleOptions.Contains(text[index]))
            {
                return collectBetween(text, text[index], section, ref index);
            } else
            {
                throw new QvxPleaseSendReplyException(QvxResult.QVX_SYNTAX_ERROR, String.Format("Error in the {0} section: The value of a condition should be enclosed between quotes", section));
            }
        }

        private static string collectBetweenSingleQuotes(string text, string section, ref int index)
        {
            return collectBetween(text, '\'', section, ref index);
        }

        private static string collectBetweenQuotes(string text, string section, ref int index)
        {
            return collectBetween(text, '\"', section, ref index);
        }

        private static string collectBetween(string text, char character, string section, ref int index)
        {
            consumeExpectedText(text, character.ToString(), section, true, ref index);
            string result = collectUntil(text, section, ref index, character);
            consumeExpectedText(text, character.ToString(), section, true, ref index);
            return result;
        }

        private static Boolean consumeExpectedText(string text, string expected, string section, Boolean throwError, ref int index)
        {
            consumeBlank(text, ref index);
            text = text.Substring(index);
            if (text.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            {
                index += expected.Length;

                #if DEBUG
                Console.WriteLine("consumeExpectedText -> Text: " + text);
                Console.WriteLine("consumeExpectedText -> Consume: " + expected);
                Console.WriteLine("consumeExpectedText -> New index: " + index);
                #endif

                return true;
            }
            else
            {
                if (throwError) throw new QvxPleaseSendReplyException(QvxResult.QVX_SYNTAX_ERROR, String.Format("Expected text not found in the {0} section: {1}", section, expected));
                else return false;
            }
        }

        private static void consumeBlank(string text, ref int index)
        {
            text = text.Substring(index);
            CharEnumerator chEnum = text.GetEnumerator();
            char current;
            if (chEnum.MoveNext())
            {
                current = chEnum.Current;
                while (chEnum.MoveNext() && blankChars.Contains(current))
                {
                    current = chEnum.Current;
                    index++;
                }
            }
            #if DEBUG
            Console.WriteLine("consumeBlank -> Text: " + text);
            Console.WriteLine("consumeBlank -> New index: " + index);
            #endif
        }

        private static string collectUntil(string text, string section, ref int index, params char[] until)
        {
            StringBuilder result = new StringBuilder();

            text = text.Substring(index);
            CharEnumerator chEnum = text.GetEnumerator();
            char current;
            bool found = false;
            while (chEnum.MoveNext())
            {
                current = chEnum.Current;
                if (until.Contains(current))
                {
                    found = true;
                    break;
                }
                else if (lineBreakChars.Contains(current))
                {
                    throw new QvxPleaseSendReplyException(QvxResult.QVX_SYNTAX_ERROR, String.Format("Expected character not found in the {0} section: {1}", section, string.Join(",", until)));
                }
                else
                {
                    result.Append(current);
                    index++;
                }
            }
            if(!found) throw new QvxPleaseSendReplyException(QvxResult.QVX_SYNTAX_ERROR, String.Format("Expected character not found in the {0} section: {1}", section, string.Join(",", until)));

            #if DEBUG
            Console.WriteLine("collectUntil -> Text: " + text);
            Console.WriteLine("collectUntil -> Until: " + until);
            Console.WriteLine("collectUntil -> New index: " + index);
            Console.WriteLine("collectUntil -> Collected: " + result.ToString());
            #endif
            return result.ToString();
        }

        public class WhereCondition
        {
            public string Field { get; set; }

            public string Value { get; set; }
        }
    }
}
