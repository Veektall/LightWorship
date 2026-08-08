using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NewAgeWorship.Core.Services;

namespace NewAgeWorship.IntelligenceWorker
{
    internal static class Program
    {
        private static readonly object Sync = new object();
        private static StreamWriter _writer;
        private static VoskOfflineRecognizer _recognizer;

        private static int Main(string[] args)
        {
            try
            {
                var model = Arg(args, "--model") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "vosk-model-small-en-us-0.15");
                var mixerDevice = IntArg(args, "--mixer-device", 0);
                var output = Arg(args, "--events") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEWAGE WORSHIP", "intelligence.events.jsonl");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                _writer = new StreamWriter(new FileStream(output, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
                _recognizer = new VoskOfflineRecognizer(model);
                using (var mixer = new PcmAudioCapture(AudioChannelKind.Mixer))
                {
                    mixer.Pcm16Available += OnPcm;
                    mixer.Start(mixerDevice);
                    Write(new { type = "health", module = "intelligence-worker", state = "ready", utc = DateTime.UtcNow, device = mixerDevice });
                    Console.CancelKeyPress += (s, e) => { e.Cancel = true; _stop = true; };
                    while (!_stop) Thread.Sleep(250);
                    mixer.Stop();
                }
                return 0;
            }
            catch (Exception ex)
            {
                try { Write(new { type = "health", module = "intelligence-worker", state = "failed", utc = DateTime.UtcNow, error = ex.Message }); } catch { }
                return 2;
            }
            finally
            {
                if (_recognizer != null) _recognizer.Dispose();
                if (_writer != null) _writer.Dispose();
            }
        }

        private static volatile bool _stop;

        private static void OnPcm(AudioChannelKind channel, byte[] bytes)
        {
            try
            {
                var r = _recognizer.Accept(channel, bytes);
                if (!r.Final || string.IsNullOrWhiteSpace(r.Text)) return;
                Write(new { type = "transcript", source = channel.ToString().ToLowerInvariant(), utc = r.TimestampUtc, text = r.Text, confidence = r.Confidence, engine = "vosk" });
            }
            catch (Exception ex)
            {
                Write(new { type = "health", module = "vosk", state = "degraded", utc = DateTime.UtcNow, error = ex.Message });
            }
        }

        private static void Write(object value)
        {
            if (_writer == null) return;
            lock (Sync) _writer.WriteLine(JsonConvert.SerializeObject(value, Formatting.None));
        }

        private static string Arg(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            int n;
            var v = Arg(args, name);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }
    }
}
