using System;
using System.Collections.Generic;
using System.Linq;

namespace LightWorship
{
    public static class SlideDeckService
    {
        public static readonly List<SlideTemplate> Templates = new List<SlideTemplate>
        {
            new SlideTemplate
            {
                Name = "Obsidian Dark",
                FontFamily = "Segoe UI",
                FontSize = 54,
                TextColor = "#FFFFFFFF",
                BackgroundColor = "#0C0F14",
                GradientStartColor = "",
                GradientEndColor = ""
            },
            new SlideTemplate
            {
                Name = "Warm Cream",
                FontFamily = "Georgia",
                FontSize = 54,
                TextColor = "#232A34",
                BackgroundColor = "#F5EBE6",
                GradientStartColor = "",
                GradientEndColor = ""
            },
            new SlideTemplate
            {
                Name = "Glowing Mesh",
                FontFamily = "Segoe UI",
                FontSize = 54,
                TextColor = "#FFFFFFFF",
                BackgroundColor = "",
                GradientStartColor = "#1E0A3C",
                GradientEndColor = "#511E78"
            },
            new SlideTemplate
            {
                Name = "Emerald Hope",
                FontFamily = "Arial",
                FontSize = 54,
                TextColor = "#FFFFFFFF",
                BackgroundColor = "#0A3C1E",
                GradientStartColor = "",
                GradientEndColor = ""
            },
            new SlideTemplate
            {
                Name = "Majestic Royal",
                FontFamily = "Segoe UI",
                FontSize = 54,
                TextColor = "#FFFFFFFF",
                BackgroundColor = "",
                GradientStartColor = "#0A143C",
                GradientEndColor = "#1A3380"
            }
        };

        public static void ApplyTemplate(Slide slide, AppSettings settings)
        {
            if (slide == null || settings == null) return;

            var name = settings.SelectedTemplateName ?? "Obsidian Dark";
            var template = Templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (template == null) template = Templates[0];

            slide.FontFamily = template.FontFamily;
            slide.FontSize = template.FontSize;
            slide.TextColor = template.TextColor;
            slide.BackgroundColor = template.BackgroundColor;
            slide.GradientStartColor = template.GradientStartColor;
            slide.GradientEndColor = template.GradientEndColor;
            slide.BackgroundPath = template.BackgroundPath;
        }

        public static List<Slide> FromBible(IEnumerable<BibleVerse> verses, AppSettings settings, int versesPerSlide)
        {
            return FromBible(verses, settings, versesPerSlide, "KJV");
        }

        public static List<Slide> FromBible(IEnumerable<BibleVerse> verses, AppSettings settings, int versesPerSlide, string versionName)
        {
            var source = verses.ToList();
            var slides = new List<Slide>();
            if (source.Count == 0)
            {
                return slides;
            }

            versesPerSlide = Math.Max(1, versesPerSlide);
            for (var index = 0; index < source.Count; index += versesPerSlide)
            {
                var group = source.Skip(index).Take(versesPerSlide).ToList();
                var first = group.First();
                var last = group.Last();
                var title = first.Reference;
                if (group.Count > 1)
                {
                    title = first.Book + " " + first.Chapter + ":" + first.Verse + "-" + last.Verse;
                }

                var slide = new Slide
                {
                    Kind = SlideKind.Bible,
                    Title = title,
                    Body = String.Join(Environment.NewLine + Environment.NewLine, group.Select(v => v.Text)),
                    Footer = String.IsNullOrWhiteSpace(versionName) ? "KJV" : versionName
                };
                ApplyTemplate(slide, settings);
                slides.Add(slide);
            }

            return slides;
        }

        public static List<Slide> FromSong(Song song, AppSettings settings)
        {
            var slides = new List<Slide>();
            if (song == null) return slides;

            var sectionsToUse = new List<SongSection>();
            if (!String.IsNullOrWhiteSpace(song.CustomSequence))
            {
                var tokens = song.CustomSequence.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    var sec = FindSectionBySequenceToken(song, token);
                    if (sec != null)
                    {
                        sectionsToUse.Add(sec);
                    }
                }
            }

            if (sectionsToUse.Count == 0)
            {
                sectionsToUse = song.Sections ?? new List<SongSection>();
            }

            foreach (var sec in sectionsToUse)
            {
                var secSlides = FromSong(song, sec, settings);
                slides.AddRange(secSlides);
            }

            return slides;
        }

        public static SongSection FindSectionBySequenceToken(Song song, string token)
        {
            if (song == null || song.Sections == null) return null;
            token = token.Trim().ToLowerInvariant();
            foreach (var sec in song.Sections)
            {
                var label = (sec.Label ?? "").Trim().ToLowerInvariant();
                if (label == token) return sec;

                if (token == "v1" && (label == "verse 1" || label == "v1")) return sec;
                if (token == "v2" && (label == "verse 2" || label == "v2")) return sec;
                if (token == "v3" && (label == "verse 3" || label == "v3")) return sec;
                if (token == "v4" && (label == "verse 4" || label == "v4")) return sec;
                if (token == "c" && (label == "chorus" || label == "c")) return sec;
                if (token == "b" && (label == "bridge" || label == "b")) return sec;
                if (token == "e" && (label == "ending" || label == "e")) return sec;

                if (label.StartsWith(token)) return sec;
            }
            return null;
        }

        public static List<Slide> FromSong(Song song, SongSection section, AppSettings settings)
        {
            var chunks = SplitLines(section.Lyrics, 4);
            if (chunks.Count == 0)
            {
                chunks.Add("");
            }

            var slides = new List<Slide>();
            for (var i = 0; i < chunks.Count; i++)
            {
                var slide = new Slide
                {
                    Kind = SlideKind.Song,
                    Title = song.Title + " - " + section.Label + (chunks.Count > 1 ? " " + (i + 1) + "/" + chunks.Count : ""),
                    Body = chunks[i],
                    Footer = SongFooter(song)
                };
                ApplyTemplate(slide, settings);
                slides.Add(slide);
            }

            return slides;
        }

        public static List<string> SplitLines(string text, int maxLines)
        {
            var lines = (text ?? "")
                .Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.None)
                .Select(l => l.TrimEnd())
                .ToList();

            var chunks = new List<string>();
            var current = new List<string>();
            foreach (var line in lines)
            {
                if (current.Count >= maxLines && !String.IsNullOrWhiteSpace(line))
                {
                    chunks.Add(String.Join(Environment.NewLine, current));
                    current.Clear();
                }

                current.Add(line);
            }

            if (current.Count > 0)
            {
                chunks.Add(String.Join(Environment.NewLine, current));
            }

            return chunks.Where(c => !String.IsNullOrWhiteSpace(c)).ToList();
        }

        public static string SongFooter(Song song)
        {
            var parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(song.Author)) parts.Add(song.Author);
            if (!String.IsNullOrWhiteSpace(song.Copyright)) parts.Add(song.Copyright);
            if (!String.IsNullOrWhiteSpace(song.CcliNumber)) parts.Add("CCLI " + song.CcliNumber);
            return String.Join("  |  ", parts);
        }
    }
}
