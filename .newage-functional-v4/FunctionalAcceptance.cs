using System;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using NewAgeWorship.Core.Services;
using NewAgeWorship.Desktop;

public static class FunctionalAcceptanceV4
{
    static void Need(bool ok,string message){if(!ok)throw new Exception(message);}

    [STAThread]
    public static void Main()
    {
        var root=AppDomain.CurrentDomain.BaseDirectory;
        var report=Path.Combine(root,"functional-test-report.txt");
        File.WriteAllText(report,"NEWAGE WORSHIP functional acceptance V4\r\n");
        var temp=Path.Combine(Path.GetTempPath(),"newage-functional-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        using(var bible=new BibleLibrary(temp,Path.Combine(root,"Data","Bible_Versions.zip")))
        {
            Need(bible.Translations.Count==8,"Bible version count != 8");
            var parser=new ScriptureReferenceParser();
            ScriptureReference typed;
            Need(parser.TryParse("John 3:16",out typed),"Typed scripture parser failed");
            BiblePassage passage;
            Need(bible.TryGetPassage(typed,"KJV",out passage),"KJV John 3:16 lookup failed");
            Need(passage.Text.IndexOf("God so loved",StringComparison.OrdinalIgnoreCase)>=0,"KJV passage is not real Bible content");
            Need(bible.Search("only begotten Son","KJV",5).Count>0,"Bible phrase search failed");
            BiblePassage grouped;
            Need(bible.TryGetPassage(typed,"MSG",out grouped),"Grouped-source passage lookup failed");
            Need(grouped.Reference.EndsWith(":16-18",StringComparison.Ordinal),"Grouped source range was not preserved");
            ScriptureReference spoken;
            Need(parser.TryParseSpoken("john three sixteen",out spoken) && spoken.Chapter==3 && spoken.VerseStart==16,"Spoken scripture parser failed");
            File.AppendAllText(report,"PASS Bible: 8 versions, direct lookup, phrase search, grouped ranges, spoken reference\r\n");
        }

        var silent=new byte[32000];
        Need(AudioHealthAnalyzer.AnalyzePcm16(silent,silent.Length,48000,"fixture").Status=="SILENT","Mixer silence detection failed");
        var healthy=new byte[32000];
        for(int i=0;i<healthy.Length/2;i++){short s=(short)(10000*Math.Sin(2*Math.PI*440*i/48000.0));healthy[i*2]=(byte)s;healthy[i*2+1]=(byte)(s>>8);}
        Need(AudioHealthAnalyzer.AnalyzePcm16(healthy,healthy.Length,48000,"fixture").Status=="HEALTHY","Mixer healthy-level detection failed");
        var clipped=new byte[32000];
        for(int i=0;i<clipped.Length/2;i++){clipped[i*2]=255;clipped[i*2+1]=127;}
        Need(AudioHealthAnalyzer.AnalyzePcm16(clipped,clipped.Length,48000,"fixture").Status=="CLIPPING","Mixer clipping detection failed");
        File.AppendAllText(report,"PASS Mixer analyzer: silence / healthy / clipping\r\n");

        var ai=new LocalAiAssistant(root);
        Need(ai.IsReady,"Local AI runtime/model missing");
        var answer=ai.CompleteAsync("Give one concise operator note: John 3:16 is prepared in Preview and still needs human approval.").GetAwaiter().GetResult();
        Need(!String.IsNullOrWhiteSpace(answer),"Local AI returned empty output");
        Need(answer.IndexOf("Loading model",StringComparison.OrdinalIgnoreCase)<0,"Local AI leaked startup banner");
        Need(answer.IndexOf("failed safely",StringComparison.OrdinalIgnoreCase)<0 && answer.IndexOf("timed out",StringComparison.OrdinalIgnoreCase)<0,"Local AI failed: "+answer);
        Need(answer.IndexOf("John",StringComparison.OrdinalIgnoreCase)>=0 || answer.IndexOf("Preview",StringComparison.OrdinalIgnoreCase)>=0 || answer.IndexOf("approval",StringComparison.OrdinalIgnoreCase)>=0,"Local AI output is not relevant operator content: "+answer);
        File.AppendAllText(report,"PASS Local AI clean inference: "+answer+"\r\n");

        var wav=Path.Combine(root,"john-three-sixteen.wav");
        using(var synth=new SpeechSynthesizer())
        {
            synth.SetOutputToWaveFile(wav,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));
            synth.Speak("John three sixteen");
        }
        string voskText;
        using(var vosk=new VoskSpeechService(Path.Combine(root,"Models","vosk-model-small-en-us-0.15")))
        {
            Need(vosk.IsReady,"Vosk model missing");
            voskText=vosk.TranscribeWaveFile(wav);
            Need(!String.IsNullOrWhiteSpace(voskText),"Vosk returned empty transcript");
        }
        var whisper=new WhisperSpeechService(root);
        Need(whisper.IsReady,"Whisper runtime/model missing");
        var whisperText=whisper.TranscribeWaveFile(wav);
        Need(!String.IsNullOrWhiteSpace(whisperText),"Whisper returned empty transcript");
        File.AppendAllText(report,"PASS Vosk transcript: "+voskText+"\r\nPASS Whisper transcript: "+whisperText+"\r\n");

        var spokenParser=new ScriptureReferenceParser();
        ScriptureReference resolved;
        Need(spokenParser.TryParseSpoken(voskText,out resolved) || spokenParser.TryParseSpoken(whisperText,out resolved),"Neither ASR transcript resolved to a scripture reference");
        File.AppendAllText(report,"PASS Voice -> scripture reference integration\r\nALL FUNCTIONAL GATES PASSED\r\n");
        Console.WriteLine(File.ReadAllText(report));
    }
}
