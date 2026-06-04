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
    public class DeepgramLiveListeningService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cancel;
        private ClientWebSocket _socket;
        private WaveInEvent _waveIn;
        private bool _running;
        private string _lastPublished = "";

        public event EventHandler<RecognizedPhraseEventArgs> PhraseRecognized;
        public event EventHandler<Exception> ListenFailed;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<float> AudioLevelAvailable;

        public bool IsRunning { get { return _running; } }

        public DeepgramLiveListeningService(AppSettings settings)
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
                _socket.Options.SetRequestHeader("Authorization", "Token " + _settings.DeepgramApiKey);
                await _socket.ConnectAsync(new Uri(BuildUrl()), token);
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

        private void StartMicrophone(CancellationToken token)
        {
            _waveIn = new WaveInEvent();
            if (_settings.AudioInputDeviceNumber >= 0)
            {
                _waveIn.DeviceNumber = _settings.AudioInputDeviceNumber;
            }

            _waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            _waveIn.BufferMilliseconds = 80;
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
                foreach (var item in DeepgramTranscriptParser.Extract(json))
                {
                    if (String.IsNullOrWhiteSpace(item.Text))
                    {
                        continue;
                    }

                    if ((item.IsFinal || item.SpeechFinal) && !String.Equals(item.Text, _lastPublished, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastPublished = item.Text;
                        if (PhraseRecognized != null)
                        {
                            PhraseRecognized(this, new RecognizedPhraseEventArgs(item.Text, item.Confidence));
                        }
                    }
                }
            }
        }

        private async Task SendAudioAsync(byte[] audio, CancellationToken token)
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
            {
                return;
            }

            await _sendLock.WaitAsync(token);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(audio), WebSocketMessageType.Binary, true, token);
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
                    throw new WebSocketException("Deepgram socket closed.");
                }

                for (var i = 0; i < result.Count; i++)
                {
                    chunks.Add(buffer[i]);
                }
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(chunks.ToArray());
        }

        private string BuildUrl()
        {
            var model = String.IsNullOrWhiteSpace(_settings.DeepgramModel) ? "nova-3" : _settings.DeepgramModel;
            return "wss://api.deepgram.com/v1/listen" +
                "?model=" + Uri.EscapeDataString(model) +
                "&language=en-NG" +
                "&encoding=linear16&sample_rate=16000&channels=1" +
                "&interim_results=true&smart_format=true&punctuate=true";
        }

        private void ValidateSettings()
        {
            if (String.IsNullOrWhiteSpace(_settings.DeepgramApiKey))
            {
                throw new InvalidOperationException("Set the Deepgram API key in Settings.");
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
