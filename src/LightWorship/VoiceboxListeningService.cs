using NAudio.Wave;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LightWorship
{
    public class VoiceboxListeningService : IDisposable
    {
        private readonly AppSettings _settings;
        private CancellationTokenSource _cancel;
        private bool _running;

        public event EventHandler<RecognizedPhraseEventArgs> PhraseRecognized;
        public event EventHandler<Exception> ListenFailed;

        public bool IsRunning { get { return _running; } }

        public VoiceboxListeningService(AppSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _cancel = new CancellationTokenSource();
            _running = true;
            Task.Run(() => CaptureLoop(_cancel.Token));
        }

        public void Stop()
        {
            _running = false;
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void CaptureLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string wavPath = null;
                try
                {
                    wavPath = RecordChunk(token);
                    if (token.IsCancellationRequested || String.IsNullOrWhiteSpace(wavPath))
                    {
                        continue;
                    }

                    var text = TranscribeFileAsync(wavPath, token).Result;
                    if (!String.IsNullOrWhiteSpace(text) && PhraseRecognized != null)
                    {
                        PhraseRecognized(this, new RecognizedPhraseEventArgs(text, 0.96f));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RaiseFailure(ex);
                    Thread.Sleep(1000);
                }
                finally
                {
                    TryDelete(wavPath);
                }
            }

            _running = false;
        }

        private string RecordChunk(CancellationToken token)
        {
            var seconds = Math.Max(2, Math.Min(20, _settings.VoiceboxChunkSeconds));
            var path = Path.Combine(Path.GetTempPath(), "lightworship-voicebox-" + Guid.NewGuid().ToString("N") + ".wav");
            using (var done = new ManualResetEventSlim(false))
            using (var waveIn = new WaveInEvent())
            {
                WaveFileWriter writer = null;
                Exception stoppedError = null;
                if (_settings.AudioInputDeviceNumber >= 0)
                {
                    waveIn.DeviceNumber = _settings.AudioInputDeviceNumber;
                }

                waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
                waveIn.BufferMilliseconds = 160;
                writer = new WaveFileWriter(path, waveIn.WaveFormat);
                waveIn.DataAvailable += (s, e) =>
                {
                    if (writer != null && e.BytesRecorded > 0)
                    {
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                        writer.Flush();
                    }
                };
                waveIn.RecordingStopped += (s, e) =>
                {
                    stoppedError = e.Exception;
                    done.Set();
                };

                waveIn.StartRecording();
                if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(seconds)))
                {
                    waveIn.StopRecording();
                    done.Wait(TimeSpan.FromSeconds(2));
                }

                if (token.IsCancellationRequested)
                {
                    try { waveIn.StopRecording(); } catch { }
                }

                writer.Dispose();
                if (stoppedError != null)
                {
                    throw stoppedError;
                }
            }

            return path;
        }

        private async Task<string> TranscribeFileAsync(string wavPath, CancellationToken token)
        {
            var baseUrl = String.IsNullOrWhiteSpace(_settings.VoiceboxServerUrl)
                ? "http://127.0.0.1:17493"
                : _settings.VoiceboxServerUrl.Trim().TrimEnd('/');

            using (var client = new HttpClient())
            using (var form = new MultipartFormDataContent())
            using (var stream = File.OpenRead(wavPath))
            using (var audio = new StreamContent(stream))
            {
                client.Timeout = TimeSpan.FromSeconds(Math.Max(20, _settings.VoiceboxChunkSeconds * 8));
                form.Add(audio, "file", Path.GetFileName(wavPath));
                if (!String.IsNullOrWhiteSpace(_settings.VoiceboxLanguage))
                {
                    form.Add(new StringContent(_settings.VoiceboxLanguage), "language");
                }

                if (!String.IsNullOrWhiteSpace(_settings.VoiceboxModel))
                {
                    form.Add(new StringContent(_settings.VoiceboxModel), "model");
                }

                var response = await client.PostAsync(baseUrl + "/transcribe", form, token);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Voicebox transcribe failed: " + response.StatusCode + " " + json);
                }

                return VoiceboxTranscriptParser.ExtractText(json);
            }
        }

        private void RaiseFailure(Exception ex)
        {
            if (ListenFailed != null)
            {
                ListenFailed(this, ex);
            }
        }

        private static void TryDelete(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try { File.Delete(path); } catch { }
        }
    }
}
