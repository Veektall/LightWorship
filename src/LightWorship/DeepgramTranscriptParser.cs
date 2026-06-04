using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LightWorship
{
    public static class DeepgramTranscriptParser
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static List<DeepgramTranscript> Extract(string json)
        {
            var results = new List<DeepgramTranscript>();
            if (String.IsNullOrWhiteSpace(json))
            {
                return results;
            }

            var transcript = MatchValue(json, @"""transcript""\s*:\s*""((?:\\.|[^""])*)""");
            if (String.IsNullOrWhiteSpace(transcript))
            {
                return results;
            }

            results.Add(new DeepgramTranscript
            {
                Text = transcript,
                IsFinal = MatchBool(json, @"""is_final""\s*:\s*(true|false)"),
                SpeechFinal = MatchBool(json, @"""speech_final""\s*:\s*(true|false)"),
                Confidence = MatchFloat(json, @"""confidence""\s*:\s*([0-9.]+)")
            });

            return results;
        }

        private static string MatchValue(string json, string pattern)
        {
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "";
            }

            try
            {
                return Serializer.Deserialize<string>("\"" + match.Groups[1].Value + "\"");
            }
            catch
            {
                return match.Groups[1].Value.Replace("\\n", " ").Replace("\\\"", "\"");
            }
        }

        private static bool MatchBool(string json, string pattern)
        {
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return match.Success && String.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static float MatchFloat(string json, string pattern)
        {
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            float value;
            return match.Success && Single.TryParse(match.Groups[1].Value, out value) ? value : 0.9f;
        }
    }

    public class DeepgramTranscript
    {
        public string Text { get; set; }
        public bool IsFinal { get; set; }
        public bool SpeechFinal { get; set; }
        public float Confidence { get; set; }
    }
}
