using System;

namespace LightWorship
{
    public class TranscriptItem
    {
        public DateTime Time { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        public int SuggestionCount { get; set; }

        public override string ToString()
        {
            return Time.ToString("HH:mm:ss") + "  " + Confidence.ToString("0.00") + "  " + Text + "  [" + SuggestionCount + "]";
        }
    }
}
