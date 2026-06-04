using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LightWorship
{
    public static class GeminiTranscriptParser
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static List<string> ExtractInputTranscripts(string json)
        {
            var results = new List<string>();
            if (String.IsNullOrWhiteSpace(json))
            {
                return results;
            }

            AddMatches(results, json, @"""inputTranscription""\s*:\s*\{[^}]*""text""\s*:\s*""((?:\\.|[^""])*)""");
            AddMatches(results, json, @"""input_transcription""\s*:\s*\{[^}]*""text""\s*:\s*""((?:\\.|[^""])*)""");
            return results;
        }

        private static void AddMatches(List<string> results, string json, string pattern)
        {
            foreach (Match match in Regex.Matches(json, pattern, RegexOptions.IgnoreCase))
            {
                var text = Unescape(match.Groups[1].Value).Trim();
                if (!String.IsNullOrWhiteSpace(text) && !results.Contains(text))
                {
                    results.Add(text);
                }
            }
        }

        private static string Unescape(string value)
        {
            try
            {
                return Serializer.Deserialize<string>("\"" + value + "\"");
            }
            catch
            {
                return value.Replace("\\n", " ").Replace("\\\"", "\"");
            }
        }
    }
}
