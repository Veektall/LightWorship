using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LightWorship
{
    public class LocalWhisperListeningService : IDisposable
    {
        private readonly AppSettings _settings;
        private CancellationTokenSource _cancel;
        private bool _running;

        public event EventHandler<RecognizedPhraseEventArgs> PhraseRecognized;
        public event EventHandler<Exception> ListenFailed;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<float> AudioLevelAvailable;

        public bool IsRunning { get { return _running; } }

        public LocalWhisperListeningService(AppSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            try
            {
                ValidateSettings();
                _cancel = new CancellationTokenSource();
                _running = true;
                if (ConnectionStatusChanged != null)
                {
                    ConnectionStatusChanged(this, "Connected");
                }
                Task.Run(() => CaptureLoop(_cancel.Token));
            }
            catch (Exception ex)
            {
                _running = false;
                RaiseFailure(ex);
            }
        }

        public void Stop()
        {
            _running = false;
            if (_cancel != null)
            {
                try { _cancel.Cancel(); } catch { }
                _cancel = null;
            }

            if (ConnectionStatusChanged != null)
            {
                ConnectionStatusChanged(this, "Disconnected");
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

                    var text = TranscribeFile(wavPath, token);
                    if (!String.IsNullOrWhiteSpace(text) && PhraseRecognized != null)
                    {
                        PhraseRecognized(this, new RecognizedPhraseEventArgs(text, 0.95f));
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
            var seconds = Math.Max(2, Math.Min(20, _settings.WhisperChunkSeconds));
            var path = Path.Combine(Path.GetTempPath(), "lightworship-whisper-" + Guid.NewGuid().ToString("N") + ".wav");
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
                waveIn.BufferMilliseconds = 250;
                writer = new WaveFileWriter(path, waveIn.WaveFormat);
                waveIn.DataAvailable += (s, e) =>
                {
                    if (writer != null && e.BytesRecorded > 0)
                    {
                        float maxVal = 0f;
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            if (i + 1 < e.BytesRecorded)
                            {
                                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                                float val = (float)Math.Abs(sample) / 32768f;
                                if (val > maxVal) maxVal = val;
                            }
                        }

                        if (AudioLevelAvailable != null)
                        {
                            AudioLevelAvailable(this, maxVal);
                        }

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

        private string TranscribeFile(string wavPath, CancellationToken token)
        {
            var arguments = "-m \"" + _settings.WhisperModelPath + "\" -f \"" + wavPath + "\" -l en -nt -np";
            var info = new ProcessStartInfo
            {
                FileName = _settings.WhisperExecutablePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_settings.WhisperExecutablePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(info))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Could not start Whisper executable.");
                }

                var timeout = Math.Max(30000, _settings.WhisperChunkSeconds * 15000);
                var started = DateTime.UtcNow;
                while (!process.WaitForExit(250))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKill(process);
                        throw new OperationCanceledException();
                    }

                    if ((DateTime.UtcNow - started).TotalMilliseconds > timeout)
                    {
                        TryKill(process);
                        throw new TimeoutException("Whisper transcription timed out.");
                    }
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Whisper failed: " + error);
                }

                return WhisperTranscriptParser.Clean(output + "\n" + error);
            }
        }

        private void ValidateSettings()
        {
            if (String.IsNullOrWhiteSpace(_settings.WhisperExecutablePath) || !File.Exists(_settings.WhisperExecutablePath))
            {
                throw new FileNotFoundException("Set the Whisper executable path in Settings.");
            }

            if (String.IsNullOrWhiteSpace(_settings.WhisperModelPath) || !File.Exists(_settings.WhisperModelPath))
            {
                throw new FileNotFoundException("Set the Whisper model path in Settings.");
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

        private static void TryKill(Process process)
        {
            try { process.Kill(); } catch { }
        }
    }
}
