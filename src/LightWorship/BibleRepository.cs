using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LightWorship
{
    public class BibleRepository
    {
        private readonly DataStore _store;
        private readonly List<BibleVerse> _verses = new List<BibleVerse>();
        private readonly Dictionary<string, BibleVerse> _byReference = new Dictionary<string, BibleVerse>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] BookNames =
        {
            "Genesis","Exodus","Leviticus","Numbers","Deuteronomy","Joshua","Judges","Ruth",
            "1 Samuel","2 Samuel","1 Kings","2 Kings","1 Chronicles","2 Chronicles","Ezra","Nehemiah","Esther","Job",
            "Psalms","Proverbs","Ecclesiastes","Song of Solomon","Isaiah","Jeremiah","Lamentations","Ezekiel","Daniel",
            "Hosea","Joel","Amos","Obadiah","Jonah","Micah","Nahum","Habakkuk","Zephaniah","Haggai","Zechariah","Malachi",
            "Matthew","Mark","Luke","John","Acts","Romans","1 Corinthians","2 Corinthians","Galatians","Ephesians",
            "Philippians","Colossians","1 Thessalonians","2 Thessalonians","1 Timothy","2 Timothy","Titus","Philemon",
            "Hebrews","James","1 Peter","2 Peter","1 John","2 John","3 John","Jude","Revelation"
        };

        private static readonly Dictionary<string, string> BookAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Gen","Genesis"},{"Ge","Genesis"},{"Ex","Exodus"},{"Exo","Exodus"},{"Lev","Leviticus"},{"Num","Numbers"},{"Deut","Deuteronomy"},
            {"Josh","Joshua"},{"Judg","Judges"},{"Ps","Psalms"},{"Psalm","Psalms"},{"Psa","Psalms"},{"Prov","Proverbs"},{"Eccl","Ecclesiastes"},
            {"Song","Song of Solomon"},{"Song Songs","Song of Solomon"},{"Isa","Isaiah"},{"Jer","Jeremiah"},{"Lam","Lamentations"},
            {"Ezek","Ezekiel"},{"Dan","Daniel"},{"Hos","Hosea"},{"Obad","Obadiah"},{"Jon","Jonah"},{"Mic","Micah"},{"Nah","Nahum"},
            {"Hab","Habakkuk"},{"Zeph","Zephaniah"},{"Hag","Haggai"},{"Zech","Zechariah"},{"Mal","Malachi"},{"Matt","Matthew"},
            {"Mk","Mark"},{"Lk","Luke"},{"Jn","John"},{"Rom","Romans"},{"1 Cor","1 Corinthians"},{"2 Cor","2 Corinthians"},
            {"Gal","Galatians"},{"Eph","Ephesians"},{"Phil","Philippians"},{"Col","Colossians"},{"1 Thess","1 Thessalonians"},
            {"2 Thess","2 Thessalonians"},{"1 Tim","1 Timothy"},{"2 Tim","2 Timothy"},{"Heb","Hebrews"},{"Jas","James"},
            {"1 Pet","1 Peter"},{"2 Pet","2 Peter"},{"Rev","Revelation"}
        };

        public BibleRepository(DataStore store)
        {
            _store = store;
        }

        public IReadOnlyList<BibleVerse> Verses { get { return _verses; } }
        public IReadOnlyList<string> Books { get { return BookNames; } }
        public string VersionName { get; private set; }

        public IReadOnlyList<int> GetChapters(string book)
        {
            return _verses
                .Where(v => v.Book.Equals(book, StringComparison.OrdinalIgnoreCase))
                .Select(v => v.Chapter)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public IReadOnlyList<BibleVerse> GetChapter(string book, int chapter)
        {
            return _verses
                .Where(v => v.Book.Equals(book, StringComparison.OrdinalIgnoreCase) && v.Chapter == chapter)
                .OrderBy(v => v.Verse)
                .ToList();
        }

        public void Load()
        {
            Load("KJV", _store.BiblePath);
        }

        public void Load(string versionName, string path)
        {
            _verses.Clear();
            _byReference.Clear();
            VersionName = String.IsNullOrWhiteSpace(versionName) ? "KJV" : versionName;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Bible data file not found.", path);
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            var raw = serializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            foreach (var pair in raw)
            {
                var verse = ParseVerse(pair.Key, CleanText(pair.Value));
                if (verse == null)
                {
                    continue;
                }

                _verses.Add(verse);
                _byReference[verse.Reference] = verse;
            }
        }

        public List<BibleVerse> Search(string query, int max)
        {
            if (String.IsNullOrWhiteSpace(query))
            {
                return new List<BibleVerse>();
            }

            var passage = GetPassage(query);
            if (passage.Count > 0)
            {
                return passage;
            }

            var terms = query.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return _verses
                .Where(v => terms.All(t => v.Text.ToLowerInvariant().Contains(t) || v.Reference.ToLowerInvariant().Contains(t)))
                .Take(max)
                .ToList();
        }

        public List<BibleVerse> GetPassage(string reference)
        {
            var parsed = ParseReference(reference);
            if (parsed == null)
            {
                return new List<BibleVerse>();
            }

            return _verses
                .Where(v => v.Book.Equals(parsed.Book, StringComparison.OrdinalIgnoreCase)
                    && v.Chapter == parsed.Chapter
                    && v.Verse >= parsed.StartVerse
                    && v.Verse <= parsed.EndVerse)
                .ToList();
        }

        public Slide CreateSlide(IEnumerable<BibleVerse> verses, AppSettings settings)
        {
            var list = verses.ToList();
            if (list.Count == 0)
            {
                return Slide.Text("No scripture selected", "");
            }

            var first = list.First();
            var last = list.Last();
            var title = first.Reference;
            if (list.Count > 1)
            {
                title = first.Book + " " + first.Chapter + ":" + first.Verse + "-" + last.Verse;
            }

            var slide = new Slide
            {
                Kind = SlideKind.Bible,
                Title = title,
                Body = String.Join(Environment.NewLine + Environment.NewLine, list.Select(v => v.Text)),
                Footer = VersionName
            };
            SlideDeckService.ApplyTemplate(slide, settings);
            return slide;
        }

        private static BibleVerse ParseVerse(string reference, string text)
        {
            var match = Regex.Match(reference, @"^(?<book>.+)\s(?<chapter>\d+):(?<verse>\d+)$");
            if (!match.Success)
            {
                return null;
            }

            return new BibleVerse
            {
                Reference = reference,
                Book = match.Groups["book"].Value,
                Chapter = Int32.Parse(match.Groups["chapter"].Value),
                Verse = Int32.Parse(match.Groups["verse"].Value),
                Text = text
            };
        }

        private static string CleanText(string text)
        {
            return (text ?? "").Replace("#", "").Replace("[", "").Replace("]", "").Trim();
        }

        private static ParsedReference ParseReference(string input)
        {
            var normalized = SpokenNumberParser.NormalizeNumbers(input ?? "");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            normalized = Regex.Replace(normalized, @"\bchapter\b", " ", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\bverse\b", ":", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\s*:\s*", ":");

            foreach (var alias in BookAliases.OrderByDescending(a => a.Key.Length))
            {
                var aliasIndex = normalized.IndexOf(alias.Key + " ", StringComparison.OrdinalIgnoreCase);
                if (aliasIndex >= 0)
                {
                    normalized = alias.Value + normalized.Substring(aliasIndex + alias.Key.Length);
                    break;
                }
            }

            var book = BookNames.OrderByDescending(b => b.Length)
                .FirstOrDefault(b => normalized.IndexOf(b + " ", StringComparison.OrdinalIgnoreCase) >= 0);
            if (book == null)
            {
                return null;
            }

            var bookIndex = normalized.IndexOf(book + " ", StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Substring(bookIndex).Trim();
            var remainder = normalized.Substring(book.Length).Trim();
            var match = Regex.Match(remainder, @"^(?<chapter>\d+)(?:(:|\s+)(?<start>\d+)(?:-|\s+)?(?<end>\d+)?)?$");
            if (!match.Success)
            {
                return null;
            }

            var chapter = Int32.Parse(match.Groups["chapter"].Value);
            var start = match.Groups["start"].Success ? Int32.Parse(match.Groups["start"].Value) : 1;
            var end = match.Groups["end"].Success ? Int32.Parse(match.Groups["end"].Value) : start;

            return new ParsedReference { Book = book, Chapter = chapter, StartVerse = start, EndVerse = end };
        }

        private class ParsedReference
        {
            public string Book { get; set; }
            public int Chapter { get; set; }
            public int StartVerse { get; set; }
            public int EndVerse { get; set; }
        }
    }
}
