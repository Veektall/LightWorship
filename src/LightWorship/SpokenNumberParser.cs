using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LightWorship
{
    public static class SpokenNumberParser
    {
        private static readonly Dictionary<string, int> SmallNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"zero",0},{"one",1},{"first",1},{"two",2},{"second",2},{"three",3},{"third",3},{"four",4},{"fourth",4},
            {"five",5},{"fifth",5},{"six",6},{"sixth",6},{"seven",7},{"seventh",7},{"eight",8},{"eighth",8},
            {"nine",9},{"ninth",9},{"ten",10},{"tenth",10},{"eleven",11},{"eleventh",11},{"twelve",12},{"twelfth",12},
            {"thirteen",13},{"fourteen",14},{"fifteen",15},{"sixteen",16},{"seventeen",17},{"eighteen",18},{"nineteen",19}
        };

        private static readonly Dictionary<string, int> Tens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"twenty",20},{"thirty",30},{"forty",40},{"fifty",50},{"sixty",60},{"seventy",70},{"eighty",80},{"ninety",90}
        };

        public static string NormalizeNumbers(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            var tokens = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s-]", " ")
                .Replace("-", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var output = new List<string>();
            for (var i = 0; i < tokens.Count;)
            {
                int consumed;
                var number = TryReadNumber(tokens, i, out consumed);
                if (consumed > 0)
                {
                    output.Add(number.ToString());
                    i += consumed;
                }
                else
                {
                    output.Add(tokens[i]);
                    i++;
                }
            }

            return String.Join(" ", output);
        }

        private static int TryReadNumber(List<string> tokens, int index, out int consumed)
        {
            consumed = 0;
            if (index >= tokens.Count)
            {
                return 0;
            }

            var token = tokens[index];
            int digit;
            if (Int32.TryParse(token, out digit))
            {
                consumed = 1;
                return digit;
            }

            if (SmallNumbers.ContainsKey(token))
            {
                var value = SmallNumbers[token];
                consumed = 1;
                return value;
            }

            if (Tens.ContainsKey(token))
            {
                var value = Tens[token];
                consumed = 1;
                if (index + 1 < tokens.Count && SmallNumbers.ContainsKey(tokens[index + 1]))
                {
                    value += SmallNumbers[tokens[index + 1]];
                    consumed = 2;
                }

                return value;
            }

            if (token.Equals("hundred", StringComparison.OrdinalIgnoreCase) && index > 0)
            {
                consumed = 1;
                return 100;
            }

            return 0;
        }
    }
}
