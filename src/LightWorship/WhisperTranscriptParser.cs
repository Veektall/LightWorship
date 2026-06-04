using System;
using System.Text.RegularExpressions;

namespace LightWorship
{
    public static class WhisperTranscriptParser
    {
        public static string Clean(string output)
        {
            if (String.IsNullOrWhiteSpace(output))
            {
                return "";
            }

            var lines = output.Replace("\r", "").Split('\n');
            var cleaned = "";
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("whisper_", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("main:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("system_info:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                line = Regex.Replace(line, @"^\[[^\]]+\]\s*", "");
                line = Regex.Replace(line, @"\s+", " ").Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                cleaned += (cleaned.Length == 0 ? "" : " ") + line;
            }

            return cleaned.Trim();
        }
    }
}
