using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LightWorship
{
    public class GeminiLiveListeningService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cancel;
        private ClientWebSocket _socket;
        private WaveInEvent _waveIn;
        private bool _running;
        private string _lastTranscript = "";

        public event EventHandler<RecognizedPhraseEventArgs> PhraseRecognized;
        public event EventHandler<Exception> ListenFailed;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<float> AudioLevelAvailable;

        public bool IsRunning { get { return _running; } }

        public GeminiLiveListeningService(AppSettings settings)
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
            Task.Run(() => RunAsync(_cancel.Token));
        }

        public void Stop()
        {
            _running = false;
            if (_cancel != null)
            {
                try { _cancel.Cancel(); } catch { }
                _cancel = null;
            }

            if (_waveIn != null)
            {
                try { _waveIn.StopRecording(); } catch { }
                try { _waveIn.Dispose(); } catch { }
                _waveIn = null;
            }

            Cleanup();
            if (ConnectionStatusChanged != null)
            {
                ConnectionStatusChanged(this, "Disconnected");
            }
        }

        public void Dispose()
        {
            Stop();
            _sendLock.Dispose();
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                ValidateSettings();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                if (ConnectionStatusChanged != null)
                {
                    ConnectionStatusChanged(this, "Connecting");
                }

                _socket = new ClientWebSocket();
                var url = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" +
                    Uri.EscapeDataString(_settings.GeminiApiKey);
                await _socket.ConnectAsync(new Uri(url), token);
                await SendTextAsync(BuildSetupJson(), token);
                await WaitForSetupAsync(token);
                StartMicrophone(token);
                _running = true;

                if (ConnectionStatusChanged != null)
                {
                    ConnectionStatusChanged(this, "Connected");
                }

                await ReceiveLoopAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                RaiseFailure(ex);
            }
            finally
            {
                _running = false;
                Cleanup();
                if (ConnectionStatusChanged != null)
                {
                    ConnectionStatusChanged(this, "Disconnected");
                }
            }
        }

        private async Task WaitForSetupAsync(CancellationToken token)
        {
            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalSeconds < 20)
            {
                var json = await ReceiveTextAsync(token);
                if (json.IndexOf("setupComplete", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                var transcripts = GeminiTranscriptParser.ExtractInputTranscripts(json);
                PublishTranscripts(transcripts);
            }

            throw new TimeoutException("Gemini Live setup timed out.");
        }

        private void StartMicrophone(CancellationToken token)
        {
            _waveIn = new WaveInEvent();
            if (_settings.AudioInputDeviceNumber >= 0)
            {
                _waveIn.DeviceNumber = _settings.AudioInputDeviceNumber;
            }

            _waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            _waveIn.BufferMilliseconds = 120;
            _waveIn.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded <= 0 || token.IsCancellationRequested)
                {
                    return;
                }

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

                var copy = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
                Task.Run(() => SendAudioAsync(copy, token));
            };
            _waveIn.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null) RaiseFailure(e.Exception);
            };
            _waveIn.StartRecording();
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
            {
                var json = await ReceiveTextAsync(token);
                PublishTranscripts(GeminiTranscriptParser.ExtractInputTranscripts(json));
            }
        }

        private void PublishTranscripts(IEnumerable<string> transcripts)
        {
            foreach (var text in transcripts)
            {
                if (String.IsNullOrWhiteSpace(text) || text == _lastTranscript)
                {
                    continue;
                }

                _lastTranscript = text;
                if (PhraseRecognized != null)
                {
                    PhraseRecognized(this, new RecognizedPhraseEventArgs(text, 0.98f));
                }
            }
        }

        private async Task SendAudioAsync(byte[] audio, CancellationToken token)
        {
            var base64 = Convert.ToBase64String(audio);
            var json = "{\"realtimeInput\":{\"audio\":{\"mimeType\":\"audio/pcm;rate=16000\",\"data\":\"" + base64 + "\"}}}";
            await SendTextAsync(json, token);
        }

        private async Task SendTextAsync(string text, CancellationToken token)
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync(token);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task<string> ReceiveTextAsync(CancellationToken token)
        {
            var buffer = new byte[32768];
            var chunks = new List<byte>();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("Gemini Live socket closed.");
                }

                for (var i = 0; i < result.Count; i++)
                {
                    chunks.Add(buffer[i]);
                }
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(chunks.ToArray());
        }

        private string BuildSetupJson()
        {
            var model = _settings.GeminiModel;
            if (String.IsNullOrWhiteSpace(model))
            {
                model = AppSettings.DefaultGeminiModel;
            }

            if (!model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            {
                model = "models/" + model;
            }

            var instruction = "Transcribe worship service speech accurately. The speaker has a Nigerian English accent; transcribe their words into clear standard English. Pay special attention to biblical names, book names, chapters, verses, and quoted scripture. Always format spoken scripture references in the standard format (e.g. John eleven thirty five -> John 11:35). Do not output any explanation or extra text, only the transcription.";
            return "{\"setup\":{\"model\":\"" + Escape(model) + "\"," +
                "\"generationConfig\":{\"responseModalities\":[\"AUDIO\"],\"temperature\":0.1,\"maxOutputTokens\":64}," +
                "\"systemInstruction\":{\"role\":\"user\",\"parts\":[{\"text\":\"" + Escape(instruction) + "\"}]}," +
                "\"inputAudioTranscription\":{\"model\":\"models/speech-to-text\"}," +
                "\"realtimeInputConfig\":{\"automaticActivityDetection\":{\"disabled\":false,\"endOfSpeechSensitivity\":\"END_SENSITIVITY_LOW\",\"silenceDurationMs\":900}}}}";
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void ValidateSettings()
        {
            if (String.IsNullOrWhiteSpace(_settings.GeminiApiKey))
            {
                throw new InvalidOperationException("Set the Gemini API key in Settings.");
            }
        }

        private void Cleanup()
        {
            if (_waveIn != null)
            {
                try { _waveIn.StopRecording(); } catch { }
                try { _waveIn.Dispose(); } catch { }
                _waveIn = null;
            }

            if (_socket != null)
            {
                var socketToClose = _socket;
                _socket = null;
                Task.Run(async () =>
                {
                    try
                    {
                        if (socketToClose.State == WebSocketState.Open)
                        {
                            await socketToClose.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Stopped", CancellationToken.None);
                        }
                    }
                    catch { }
                    finally
                    {
                        try { socketToClose.Dispose(); } catch { }
                    }
                });
            }
        }

        private void RaiseFailure(Exception ex)
        {
            if (ListenFailed != null)
            {
                ListenFailed(this, ex);
            }
        }
    }
}
