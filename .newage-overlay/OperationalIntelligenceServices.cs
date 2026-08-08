using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using NewAgeWorship.Core.Models;

namespace NewAgeWorship.Core.Services
{
    public sealed class StructuredAuditLogger
    {
        private readonly object _sync = new object();
        private readonly string _path;
        public StructuredAuditLogger(string path) { _path = path ?? throw new ArgumentNullException(nameof(path)); Directory.CreateDirectory(Path.GetDirectoryName(path)); }
        public void Write(string actor, string command, string result, string device = "local", string service = "")
        {
            var row = new { timestampUtc = DateTime.UtcNow, actor = actor ?? "", command = command ?? "", result = result ?? "", device = device ?? "", service = service ?? "" };
            var line = JsonConvert.SerializeObject(row, Formatting.None);
            lock (_sync) File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public sealed class QuoteDetectionResult
    {
        public string ExactText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
    }

    /// <summary>Conservative local quote-candidate detector. It never publishes; it only proposes exact transcript spans.</summary>
    public sealed class QuoteCandidateDetector
    {
        private static readonly string[] Rhetorical = { "remember this", "the truth is", "never forget", "you cannot", "you can never", "the moment", "when you", "if you", "until you", "what you" };
        public QuoteDetectionResult Evaluate(string sentence, bool emphasized = false, bool repeated = false, double audienceResponse = 0)
        {
            var text = (sentence ?? string.Empty).Trim();
            var reasons = new List<string>();
            if (text.Length < 35 || text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length < 7)
                return new QuoteDetectionResult { ExactText = text, Confidence = 0, Reasons = reasons };
            double score = 0.35;
            if (text.Length <= 220) { score += 0.12; reasons.Add("concise"); }
            if (Rhetorical.Any(x => text.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) { score += 0.15; reasons.Add("rhetorical structure"); }
            if (emphasized) { score += 0.17; reasons.Add("speaker emphasis"); }
            if (repeated) { score += 0.17; reasons.Add("repetition"); }
            if (audienceResponse >= 0.6) { score += 0.10; reasons.Add("audience response"); }
            if (text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?")) { score += 0.04; reasons.Add("sentence complete"); }
            return new QuoteDetectionResult { ExactText = text, Confidence = Math.Min(0.98, score), Reasons = reasons };
        }
    }

    public sealed class ServiceTimelineItem
    {
        public TimeSpan Timestamp { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    public sealed class PostServiceReport
    {
        public List<ServiceTimelineItem> Timeline { get; } = new List<ServiceTimelineItem>();
        public List<string> Scriptures { get; } = new List<string>();
        public List<string> PrayerPoints { get; } = new List<string>();
        public List<QuoteDetectionResult> Quotes { get; } = new List<QuoteDetectionResult>();
    }

    /// <summary>Deterministic post-service pass linking extracted items to source timestamps.</summary>
    public sealed class PostServiceProcessor
    {
        private readonly ScriptureReferenceParser _scripture = new ScriptureReferenceParser();
        private readonly PrayerPointExtractor _prayer = new PrayerPointExtractor();
        private readonly QuoteCandidateDetector _quotes = new QuoteCandidateDetector();

        public PostServiceReport Process(IEnumerable<Tuple<TimeSpan, string, double>> transcript)
        {
            var report = new PostServiceReport();
            foreach (var row in transcript ?? Enumerable.Empty<Tuple<TimeSpan, string, double>>())
            {
                var text = row.Item2 ?? string.Empty;
                var scripture = _scripture.Parse(text);
                if (scripture != null)
                {
                    var reference = scripture.ToString();
                    report.Scriptures.Add(reference);
                    report.Timeline.Add(new ServiceTimelineItem { Timestamp = row.Item1, Kind = "scripture", Text = reference, Confidence = row.Item3 });
                }
                var prayer = _prayer.Extract(text, row.Item3);
                if (prayer != null)
                {
                    report.PrayerPoints.Add(prayer.CleanedText);
                    report.Timeline.Add(new ServiceTimelineItem { Timestamp = row.Item1, Kind = "prayer", Text = prayer.ExactText, Confidence = prayer.Confidence });
                }
                var quote = _quotes.Evaluate(text);
                if (quote.Confidence >= 0.65)
                {
                    report.Quotes.Add(quote);
                    report.Timeline.Add(new ServiceTimelineItem { Timestamp = row.Item1, Kind = "quote-candidate", Text = quote.ExactText, Confidence = quote.Confidence });
                }
            }
            return report;
        }
    }

    public sealed class MediaTranscodeResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>Invokes only a locally bundled FFmpeg executable. No network or provider fallback exists.</summary>
    public sealed class LocalFfmpegTranscoder
    {
        private readonly string _ffmpeg;
        public LocalFfmpegTranscoder(string ffmpegPath) { _ffmpeg = ffmpegPath ?? string.Empty; }
        public MediaTranscodeResult ConvertToSafeMp4(string inputPath, string outputPath)
        {
            if (!File.Exists(_ffmpeg)) return new MediaTranscodeResult { Error = "FFmpeg is not installed in the local application package." };
            if (!File.Exists(inputPath)) return new MediaTranscodeResult { Error = "Input media does not exist." };
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpeg,
                Arguments = "-hide_banner -nostdin -y -i \"" + inputPath + "\" -map_metadata -1 -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -movflags +faststart \"" + outputPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            try
            {
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    var error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0 || !File.Exists(outputPath)) return new MediaTranscodeResult { Error = error };
                }
                return new MediaTranscodeResult { Success = true, OutputPath = outputPath };
            }
            catch (Exception ex) { return new MediaTranscodeResult { Error = ex.Message }; }
        }
    }

    public sealed class PermissionedCommandGate
    {
        private readonly RolePermissionService _roles = new RolePermissionService();
        public bool CanExecute(UserRole role, string command, bool confirmed)
        {
            var cmd = (command ?? string.Empty).Trim().ToLowerInvariant();
            var highRisk = cmd.Contains("blackout") || cmd.Contains("emergency") || cmd.Contains("delete") || cmd.Contains("finance");
            if (highRisk && !confirmed) return false;
            return _roles.IsAllowed(role, cmd);
        }
    }
}
