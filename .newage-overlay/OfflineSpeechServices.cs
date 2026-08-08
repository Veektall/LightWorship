using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using Vosk;

namespace NewAgeWorship.Core.Services
{
    public enum AudioChannelKind
    {
        Mixer,
        Command
    }

    public sealed class AudioCaptureStatus
    {
        public AudioChannelKind Channel { get; set; }
        public bool Running { get; set; }
        public int DeviceNumber { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public double Peak { get; set; }
        public bool Clipping { get; set; }
        public DateTime LastBufferUtc { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Legacy-safe 16 kHz PCM capture. The PRD's service feed and command feed are represented
    /// by distinct instances and cannot be confused by downstream consumers. WaveIn is used as
    /// a Windows 7-compatible fallback when WASAPI device routing is unavailable.
    /// </summary>
    public sealed class PcmAudioCapture : IDisposable
    {
        private readonly object _sync = new object();
        private readonly AudioChannelKind _channel;
        private WaveInEvent _waveIn;
        private bool _disposed;

        public PcmAudioCapture(AudioChannelKind channel)
        {
            _channel = channel;
            Status = new AudioCaptureStatus { Channel = channel, SampleRate = 16000, Channels = 1 };
        }

        public AudioCaptureStatus Status { get; private set; }
        public event Action<AudioChannelKind, byte[]> Pcm16Available;

        public static IReadOnlyList<string> EnumerateDevices()
        {
            var list = new List<string>();
            for (var i = 0; i < WaveIn.DeviceCount; i++)
            {
                var c = WaveIn.GetCapabilities(i);
                list.Add(i.ToString(CultureInfo.InvariantCulture) + ": " + c.ProductName);
            }
            return list;
        }

        public void Start(int deviceNumber)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                StopInternal();
                var input = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    BufferMilliseconds = 100,
                    NumberOfBuffers = 3,
                    WaveFormat = new WaveFormat(16000, 16, 1)
                };
                input.DataAvailable += OnDataAvailable;
                input.RecordingStopped += OnRecordingStopped;
                _waveIn = input;
                Status.DeviceNumber = deviceNumber;
                Status.Error = string.Empty;
                input.StartRecording();
                Status.Running = true;
            }
        }

        public void Stop()
        {
            lock (_sync) StopInternal();
        }

        private void StopInternal()
        {
            if (_waveIn == null) return;
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
            Status.Running = false;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            var copy = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
            double peak = 0;
            for (var i = 0; i + 1 < copy.Length; i += 2)
            {
                var sample = (short)(copy[i] | (copy[i + 1] << 8));
                var value = Math.Abs(sample / 32768.0);
                if (value > peak) peak = value;
            }
            Status.Peak = peak;
            Status.Clipping = peak >= 0.985;
            Status.LastBufferUtc = DateTime.UtcNow;
            var handler = Pcm16Available;
            if (handler != null) handler(_channel, copy);
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Status.Running = false;
            if (e.Exception != null) Status.Error = e.Exception.Message;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PcmAudioCapture));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_sync) StopInternal();
        }
    }

    public sealed class SpeechResult
    {
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool Final { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public AudioChannelKind Channel { get; set; }
    }

    /// <summary>
    /// Fully local Vosk recognizer. Model loading and inference never require network access.
    /// Each input channel owns an independent recognizer so operator commands cannot be treated
    /// as pulpit/service speech.
    /// </summary>
    public sealed class VoskOfflineRecognizer : IDisposable
    {
        private readonly Model _model;
        private readonly VoskRecognizer _mixer;
        private readonly VoskRecognizer _command;
        private bool _disposed;

        public VoskOfflineRecognizer(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath)) throw new ArgumentException("A local Vosk model path is required.", nameof(modelPath));
            if (!Directory.Exists(modelPath)) throw new DirectoryNotFoundException(modelPath);
            Vosk.Vosk.SetLogLevel(-1);
            _model = new Model(modelPath);
            _mixer = new VoskRecognizer(_model, 16000.0f);
            _command = new VoskRecognizer(_model, 16000.0f);
            _mixer.SetWords(true);
            _command.SetWords(true);
        }

        public SpeechResult Accept(AudioChannelKind channel, byte[] pcm16)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VoskOfflineRecognizer));
            if (pcm16 == null || pcm16.Length == 0) return new SpeechResult { Channel = channel };
            var recognizer = channel == AudioChannelKind.Command ? _command : _mixer;
            var final = recognizer.AcceptWaveform(pcm16, pcm16.Length);
            var json = final ? recognizer.Result() : recognizer.PartialResult();
            return Parse(json, final, channel);
        }

        public SpeechResult Flush(AudioChannelKind channel)
        {
            var recognizer = channel == AudioChannelKind.Command ? _command : _mixer;
            return Parse(recognizer.FinalResult(), true, channel);
        }

        private static SpeechResult Parse(string json, bool final, AudioChannelKind channel)
        {
            var result = new SpeechResult { Final = final, Channel = channel, TimestampUtc = DateTime.UtcNow };
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                var o = JObject.Parse(json);
                result.Text = ((string)(final ? o["text"] : o["partial"]) ?? string.Empty).Trim();
                if (final && o["result"] is JArray words && words.Count > 0)
                {
                    var confidences = words.Select(x => (double?)x["conf"]).Where(x => x.HasValue).Select(x => x.Value).ToArray();
                    if (confidences.Length > 0) result.Confidence = confidences.Average();
                }
            }
            catch
            {
                result.Text = string.Empty;
                result.Confidence = 0;
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _mixer.Dispose();
            _command.Dispose();
            _model.Dispose();
        }
    }

    public sealed class PushToTalkSession : IDisposable
    {
        private readonly MemoryStream _pcm = new MemoryStream();
        private readonly PcmAudioCapture _capture = new PcmAudioCapture(AudioChannelKind.Command);
        private bool _active;

        public PushToTalkSession()
        {
            _capture.Pcm16Available += OnPcm;
        }

        public AudioCaptureStatus Status => _capture.Status;

        public void Press(int deviceNumber)
        {
            _pcm.SetLength(0);
            _active = true;
            _capture.Start(deviceNumber);
        }

        public byte[] Release()
        {
            _capture.Stop();
            _active = false;
            return _pcm.ToArray();
        }

        private void OnPcm(AudioChannelKind channel, byte[] bytes)
        {
            if (!_active || channel != AudioChannelKind.Command) return;
            _pcm.Write(bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            _capture.Pcm16Available -= OnPcm;
            _capture.Dispose();
            _pcm.Dispose();
        }
    }
}
