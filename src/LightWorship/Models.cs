using System;
using System.Collections.Generic;

namespace LightWorship
{
    public class BibleVerse
    {
        public string Reference { get; set; }
        public string Book { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Reference + "  " + Text;
        }
    }

    public class Song
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Copyright { get; set; }
        public string CcliNumber { get; set; }
        public string Key { get; set; }
        public string TimeSignature { get; set; }
        public int Tempo { get; set; }
        public string Duration { get; set; }
        public string CustomSequence { get; set; }
        public List<SongSection> Sections { get; set; }

        public Song()
        {
            Id = Guid.NewGuid().ToString("N");
            Sections = new List<SongSection>();
            CustomSequence = "";
        }

        public override string ToString()
        {
            return Title;
        }
    }

    public class SongSection
    {
        public string Label { get; set; }
        public string Lyrics { get; set; }

        public override string ToString()
        {
            return Label;
        }
    }

    public class MediaItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Kind { get; set; }

        public MediaItem()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class ScheduleItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public Slide Slide { get; set; }
        public string Notes { get; set; }

        public ScheduleItem()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public override string ToString()
        {
            return Title;
        }
    }

    public class SlideTemplate
    {
        public string Name { get; set; }
        public string FontFamily { get; set; }
        public double FontSize { get; set; }
        public string TextColor { get; set; }
        public string BackgroundColor { get; set; }
        public string GradientStartColor { get; set; }
        public string GradientEndColor { get; set; }
        public string BackgroundPath { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class AppSettings
    {
        public const string DefaultGeminiModel = "gemini-2.5-flash-native-audio-latest";
        public const string DefaultGeminiApiKey = "";
        public const string DefaultDeepgramApiKey = "";

        public string DefaultBibleVersion { get; set; }
        public string LogoImagePath { get; set; }
        public string MediaLibraryPath { get; set; }
        public string FontFamily { get; set; }
        public double FontSize { get; set; }
        public string TextColor { get; set; }
        public int OutputScreenIndex { get; set; }
        public bool AutoPreviewLiveSuggestions { get; set; }
        public double ListeningConfidenceThreshold { get; set; }
        public string TranscriptionEngine { get; set; }
        public string WhisperExecutablePath { get; set; }
        public string WhisperModelPath { get; set; }
        public int WhisperChunkSeconds { get; set; }
        public string GeminiApiKey { get; set; }
        public string GeminiModel { get; set; }
        public string DeepgramApiKey { get; set; }
        public string DeepgramModel { get; set; }
        public int AudioInputDeviceNumber { get; set; }
        public bool LoadLastScheduleOnStartup { get; set; }
        public bool OpenProjectorOnStartup { get; set; }
        public bool StartListeningOnStartup { get; set; }
        public string SelectedTemplateName { get; set; }
        public bool ExternalScriptureIntegrationEnabled { get; set; }
        public string ExternalTargetWindowTitle { get; set; }
        public string ExternalScriptureInputHotkey { get; set; }
        public bool ExternalSendEnterAfterReference { get; set; }
        public int ExternalFocusDelayMs { get; set; }
        public string VoiceboxServerUrl { get; set; }
        public string VoiceboxModel { get; set; }
        public string VoiceboxLanguage { get; set; }
        public int VoiceboxChunkSeconds { get; set; }

        public AppSettings()
        {
            DefaultBibleVersion = "KJV";
            MediaLibraryPath = "";
            FontFamily = "Segoe UI";
            FontSize = 54;
            TextColor = "#FFFFFFFF";
            OutputScreenIndex = 0;
            AutoPreviewLiveSuggestions = true;
            ListeningConfidenceThreshold = 0.35;
            TranscriptionEngine = "Deepgram";
            WhisperExecutablePath = "";
            WhisperModelPath = "";
            WhisperChunkSeconds = 4;
            GeminiApiKey = DefaultGeminiApiKey;
            GeminiModel = DefaultGeminiModel;
            DeepgramApiKey = DefaultDeepgramApiKey;
            DeepgramModel = "nova-3";
            AudioInputDeviceNumber = -1;
            LoadLastScheduleOnStartup = true;
            OpenProjectorOnStartup = false;
            StartListeningOnStartup = false;
            SelectedTemplateName = "Obsidian Dark";
            ExternalScriptureIntegrationEnabled = false;
            ExternalTargetWindowTitle = "EasyWorship";
            ExternalScriptureInputHotkey = "";
            ExternalSendEnterAfterReference = false;
            ExternalFocusDelayMs = 350;
            VoiceboxServerUrl = "http://127.0.0.1:17493";
            VoiceboxModel = "turbo";
            VoiceboxLanguage = "en";
            VoiceboxChunkSeconds = 5;
        }
    }
}
