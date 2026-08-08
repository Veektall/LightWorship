using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NewAgeWorship.Core.Models;

namespace NewAgeWorship.Core.Services
{
    public sealed class LyricPosition
    {
        public string SongId { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int LineIndex { get; set; } = -1;
        public string Line { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>Prepared-song tracker. It deliberately refuses unknown songs rather than projecting guessed lyrics.</summary>
    public sealed class PreparedSongTracker
    {
        public LyricPosition Match(string recognizedText, IEnumerable<Tuple<string,string,IReadOnlyList<string>>> preparedSongs, string activeSongId = "", int previousLine = -1)
        {
            var query = Normalize(recognizedText);
            if (query.Length < 5) return new LyricPosition();
            LyricPosition best = null;
            foreach (var song in preparedSongs ?? Enumerable.Empty<Tuple<string,string,IReadOnlyList<string>>>())
            {
                for (var i = 0; i < song.Item3.Count; i++)
                {
                    var line = song.Item3[i] ?? string.Empty;
                    var score = Similarity(query, Normalize(line));
                    if (song.Item1 == activeSongId)
                    {
                        if (i == previousLine || i == previousLine + 1) score += 0.09;
                        else if (Math.Abs(i - previousLine) <= 2) score += 0.04;
                    }
                    if (best == null || score > best.Confidence)
                        best = new LyricPosition { SongId = song.Item1, Section = song.Item2, LineIndex = i, Line = line, Confidence = Math.Min(0.99, score) };
                }
            }
            return best != null && best.Confidence >= 0.65 ? best : new LyricPosition();
        }

        private static string Normalize(string value)
        {
            var chars = (value ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray();
            return string.Join(" ", new string(chars).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
        private static double Similarity(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0;
            var aa = new HashSet<string>(a.Split(' ')); var bb = new HashSet<string>(b.Split(' '));
            var overlap = aa.Intersect(bb).Count(); var union = aa.Union(bb).Count();
            return union == 0 ? 0 : overlap / (double)union;
        }
    }

    public sealed class SemanticAssetHit
    {
        public AssetRecord Asset { get; set; }
        public double Score { get; set; }
    }

    /// <summary>Offline semantic-ish asset search using weighted token overlap over approved metadata.</summary>
    public sealed class SemanticAssetSearch
    {
        public IReadOnlyList<SemanticAssetHit> Search(string query, IEnumerable<AssetRecord> assets, int limit = 20)
        {
            var q = Tokens(query);
            if (q.Count == 0) return Array.Empty<SemanticAssetHit>();
            return (assets ?? Enumerable.Empty<AssetRecord>())
                .Select(a => new SemanticAssetHit { Asset = a, Score = Score(q, Tokens((a.Title ?? "") + " " + (a.Source ?? "") + " " + (a.FilePath ?? ""))) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score).ThenBy(x => x.Asset.Title)
                .Take(Math.Max(1, limit)).ToList();
        }
        private static HashSet<string> Tokens(string s) => new HashSet<string>((s ?? "").ToLowerInvariant().Split(new[] { ' ', '\t', '\r', '\n', '-', '_', '.', ',', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 1));
        private static double Score(HashSet<string> q, HashSet<string> d) => q.Count == 0 ? 0 : q.Count(x => d.Contains(x)) / (double)q.Count;
    }

    public enum AutomationTriggerKind { Time, State, Phrase, MediaEnd, Alert, Device, Silence }
    public sealed class AutomationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public AutomationTriggerKind TriggerKind { get; set; }
        public string TriggerValue { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public double MinimumConfidence { get; set; } = 0.92;
    }
    public sealed class AutomationRuleEngine
    {
        public IReadOnlyList<AutomationRule> Match(AutomationTriggerKind kind, string value, double confidence, IEnumerable<AutomationRule> rules)
        {
            return (rules ?? Enumerable.Empty<AutomationRule>()).Where(r => r.Enabled && r.TriggerKind == kind && confidence >= r.MinimumConfidence &&
                (string.IsNullOrWhiteSpace(r.TriggerValue) || (value ?? "").IndexOf(r.TriggerValue, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }
    }

    public sealed class LocalToolResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Capability-gated adapter for a user-installed local executable such as llama.cpp or a local image generator.
    /// It never downloads models, invokes a cloud API, or falls back to paid processing.
    /// </summary>
    public sealed class LocalToolAdapter
    {
        private readonly string _executable;
        public LocalToolAdapter(string executable) { _executable = executable ?? string.Empty; }
        public bool Available => File.Exists(_executable);

        public LocalToolResult Run(string arguments, string standardInput = "", int timeoutSeconds = 120)
        {
            if (!Available) return new LocalToolResult { Error = "Configured local tool is unavailable." };
            var psi = new ProcessStartInfo { FileName = _executable, Arguments = arguments ?? "", UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            try
            {
                using (var p = Process.Start(psi))
                {
                    if (!string.IsNullOrEmpty(standardInput)) { p.StandardInput.Write(standardInput); p.StandardInput.Close(); }
                    var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(Math.Max(1, timeoutSeconds) * 1000)) { try { p.Kill(); } catch { } return new LocalToolResult { Error = "Local tool timed out." }; }
                    return new LocalToolResult { Success = p.ExitCode == 0, Output = output, Error = error };
                }
            }
            catch (Exception ex) { return new LocalToolResult { Error = ex.Message }; }
        }
    }
}
