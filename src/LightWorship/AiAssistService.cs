using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LightWorship
{
    public class AiAssistService
    {
        private readonly BibleRepository _bible;
        private readonly Func<IEnumerable<Song>> _songs;
        private readonly AppSettings _settings;

        public AiAssistService(BibleRepository bible, Func<IEnumerable<Song>> songs, AppSettings settings)
        {
            _bible = bible;
            _songs = songs;
            _settings = settings;
        }

        public List<Slide> Analyze(string heardPhrase, int maxResults)
        {
            var results = new List<ScoredSlide>();
            heardPhrase = SpokenNumberParser.NormalizeNumbers(heardPhrase);
            var normalized = Normalize(heardPhrase);
            if (String.IsNullOrWhiteSpace(normalized))
            {
                return new List<Slide>();
            }

            foreach (var verse in _bible.Search(heardPhrase, 4))
            {
                results.Add(new ScoredSlide(100, _bible.CreateSlide(new[] { verse }, _settings)));
            }

            foreach (var verse in RankVerses(normalized).Take(6))
            {
                results.Add(new ScoredSlide(verse.Score, _bible.CreateSlide(new[] { verse.Verse }, _settings)));
            }

            foreach (var match in RankSongs(normalized).Take(6))
            {
                results.Add(new ScoredSlide(match.Score, new Slide
                {
                    Kind = SlideKind.Song,
                    Title = "Song match: " + match.Song.Title + " - " + match.Section.Label,
                    Body = match.Section.Lyrics,
                    Footer = SongFooter(match.Song),
                    FontFamily = _settings.FontFamily,
                    FontSize = _settings.FontSize,
                    TextColor = _settings.TextColor
                }));
            }

            return results
                .GroupBy(r => r.Slide.Kind + "|" + r.Slide.Title + "|" + r.Slide.Body)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .Select(r => r.Slide)
                .ToList();
        }

        public static string Normalize(string text)
        {
            text = (text ?? "").ToLowerInvariant();
            text = Regex.Replace(text, @"[^a-z0-9\s]", " ");
            text = Regex.Replace(text, @"\b(chapter|verse|please|open|read|turn|to|from|the|a|an|and|let|us|start|by|with|me|church)\b", " ");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private IEnumerable<VerseScore> RankVerses(string normalized)
        {
            var heardTokens = TokenSet(normalized);
            if (heardTokens.Count == 0)
            {
                yield break;
            }

            foreach (var verse in _bible.Verses)
            {
                var verseTokens = TokenSet(Normalize(verse.Reference + " " + verse.Text));
                var overlap = heardTokens.Intersect(verseTokens).Count();
                if (overlap == 0)
                {
                    continue;
                }

                var score = (int)Math.Round(100.0 * overlap / Math.Max(heardTokens.Count, 1));
                if (score >= 35)
                {
                    yield return new VerseScore(verse, score);
                }
            }
        }

        private IEnumerable<SongScore> RankSongs(string normalized)
        {
            var heardTokens = TokenSet(normalized);
            foreach (var song in _songs())
            {
                foreach (var section in song.Sections)
                {
                    var songTokens = TokenSet(Normalize(song.Title + " " + song.Author + " " + section.Label + " " + section.Lyrics));
                    var overlap = heardTokens.Intersect(songTokens).Count();
                    var score = heardTokens.Count == 0 ? 0 : (int)Math.Round(100.0 * overlap / heardTokens.Count);
                    if (score >= 35)
                    {
                        yield return new SongScore(song, section, score);
                    }
                }
            }
        }

        private static HashSet<string> TokenSet(string text)
        {
            return new HashSet<string>(
                (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length > 1),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string SongFooter(Song song)
        {
            var parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(song.Author)) parts.Add(song.Author);
            if (!String.IsNullOrWhiteSpace(song.Copyright)) parts.Add(song.Copyright);
            if (!String.IsNullOrWhiteSpace(song.CcliNumber)) parts.Add("CCLI " + song.CcliNumber);
            return String.Join("  |  ", parts);
        }

        private class ScoredSlide
        {
            public int Score { get; private set; }
            public Slide Slide { get; private set; }

            public ScoredSlide(int score, Slide slide)
            {
                Score = score;
                Slide = slide;
            }
        }

        private class VerseScore
        {
            public BibleVerse Verse { get; private set; }
            public int Score { get; private set; }

            public VerseScore(BibleVerse verse, int score)
            {
                Verse = verse;
                Score = score;
            }
        }

        private class SongScore
        {
            public Song Song { get; private set; }
            public SongSection Section { get; private set; }
            public int Score { get; private set; }

            public SongScore(Song song, SongSection section, int score)
            {
                Song = song;
                Section = section;
                Score = score;
            }
        }
    }
}
