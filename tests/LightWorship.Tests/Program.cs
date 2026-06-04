using LightWorship;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightWorship.Tests
{
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            var store = new DataStore();
            var bible = new BibleRepository(store);
            bible.Load();

            Check("KJV loads all verses", bible.Verses.Count == 31102);
            Check("John 3:16 reference search", bible.Search("John 3:16", 10).Any(v => v.Reference == "John 3:16"));
            Check("Jn 3 16 compact reference search", bible.Search("Jn 3 16", 10).Any(v => v.Reference == "John 3:16"));
            Check("Genesis chapter phrase search", bible.Search("Genesis chapter 1 verse 3", 10).Any(v => v.Reference == "Genesis 1:3"));
            Check("Spoken John three sixteen search", bible.Search("John three sixteen", 10).Any(v => v.Reference == "John 3:16"));
            Check("Spoken Genesis one three search", bible.Search("Genesis one three", 10).Any(v => v.Reference == "Genesis 1:3"));
            Check("Book/chapter browse", bible.GetChapter("Genesis", 1).Count == 31);
            Check("Phrase search finds Genesis 1:3", bible.Search("let there be light", 10).Any(v => v.Reference == "Genesis 1:3"));

            var songs = new List<Song>
            {
                new Song
                {
                    Title = "Amazing Grace",
                    Author = "John Newton",
                    Sections = new List<SongSection>
                    {
                        new SongSection { Label = "Verse 1", Lyrics = "Amazing grace how sweet the sound that saved a wretch like me" }
                    }
                }
            };
            var settings = new AppSettings();
            var assist = new AiAssistService(bible, () => songs, settings);
            Check("AI Assist detects Genesis 1:3 phrase", assist.Analyze("and God said let there be light", 5).Any(s => s.Title == "Genesis 1:3"));
            Check("AI Assist detects direct reference", assist.Analyze("please open Genesis chapter 1 verse 3", 5).Any(s => s.Title == "Genesis 1:3"));
            Check("AI Assist detects spoken direct reference", assist.Analyze("please open John three sixteen", 5).Any(s => s.Title == "John 3:16"));
            Check("AI Assist detects local song lyric", assist.Analyze("amazing grace sweet sound", 5).Any(s => s.Kind == SlideKind.Song && s.Title.Contains("Amazing Grace")));
            Check("AI Assist normalization keeps useful terms", AiAssistService.Normalize("Please open Genesis chapter 1 verse 3").Contains("genesis 1 3"));
            Check("Spoken number parser converts words", SpokenNumberParser.NormalizeNumbers("John three sixteen").Contains("john 3 16"));
            Check("Whisper parser removes timestamps", WhisperTranscriptParser.Clean("[00:00:00.000 --> 00:00:02.000] Genesis one three").Equals("Genesis one three"));
            Check("Gemini parser extracts input transcript", GeminiTranscriptParser.ExtractInputTranscripts("{\"serverContent\":{\"inputTranscription\":{\"text\":\"Genesis one three\"}}}").FirstOrDefault() == "Genesis one three");
            Check("Deepgram parser extracts final transcript", DeepgramTranscriptParser.Extract("{\"is_final\":true,\"speech_final\":true,\"channel\":{\"alternatives\":[{\"transcript\":\"Genesis 1:3\",\"confidence\":0.99}]}}").FirstOrDefault().Text == "Genesis 1:3");
            Check("Voicebox parser extracts transcript", VoiceboxTranscriptParser.ExtractText("{\"text\":\"Genesis one three\"}") == "Genesis one three");
            Check("Bible deck splits long passage", SlideDeckService.FromBible(bible.GetPassage("Genesis 1:1-5"), settings, 2).Count == 3);
            Check("Song deck splits long lyrics", SlideDeckService.FromSong(songs[0], songs[0].Sections[0], settings).Count >= 1);

            Console.WriteLine(_failures == 0 ? "All tests passed." : _failures + " test(s) failed.");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                Console.WriteLine("[PASS] " + name);
                return;
            }

            _failures++;
            Console.WriteLine("[FAIL] " + name);
        }
    }
}
