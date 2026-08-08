using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AForge.Video;
using AForge.Video.DirectShow;

namespace NewAgeWorship.Core.Services
{
    public sealed class CameraDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Moniker { get; set; } = string.Empty;
    }

    public sealed class CameraFrameEventArgs : EventArgs
    {
        public CameraFrameEventArgs(Bitmap frame, DateTime timestampUtc)
        {
            Frame = frame;
            TimestampUtc = timestampUtc;
        }
        public Bitmap Frame { get; }
        public DateTime TimestampUtc { get; }
    }

    /// <summary>
    /// DirectShow camera/capture-card input for the Legacy profile. USB webcams and capture cards
    /// exposed as DirectShow video devices share this path. Frames are cloned before publication so
    /// the capture worker owns no UI object and can fail independently of Program rendering.
    /// </summary>
    public sealed class CameraCaptureService : IDisposable
    {
        private readonly object _sync = new object();
        private VideoCaptureDevice _device;
        private bool _disposed;

        public event EventHandler<CameraFrameEventArgs> FrameAvailable;
        public string ActiveDeviceName { get; private set; } = string.Empty;
        public bool Running => _device != null && _device.IsRunning;
        public string LastError { get; private set; } = string.Empty;

        public static IReadOnlyList<CameraDeviceInfo> Enumerate()
        {
            var collection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            return collection.Cast<FilterInfo>()
                .Select(x => new CameraDeviceInfo { Name = x.Name, Moniker = x.MonikerString })
                .ToList();
        }

        public void Start(string moniker, int preferredWidth = 1280, int preferredHeight = 720, int preferredFps = 30)
        {
            if (string.IsNullOrWhiteSpace(moniker)) throw new ArgumentException("Camera moniker is required.", nameof(moniker));
            lock (_sync)
            {
                ThrowIfDisposed();
                StopInternal();
                var d = new VideoCaptureDevice(moniker);
                var cap = d.VideoCapabilities
                    .OrderBy(x => Math.Abs(x.FrameSize.Width - preferredWidth) + Math.Abs(x.FrameSize.Height - preferredHeight) + Math.Abs(x.AverageFrameRate - preferredFps) * 10)
                    .FirstOrDefault();
                if (cap != null) d.VideoResolution = cap;
                d.NewFrame += OnNewFrame;
                d.VideoSourceError += OnVideoSourceError;
                _device = d;
                ActiveDeviceName = d.Source;
                LastError = string.Empty;
                d.Start();
            }
        }

        public void Stop()
        {
            lock (_sync) StopInternal();
        }

        private void StopInternal()
        {
            if (_device == null) return;
            _device.NewFrame -= OnNewFrame;
            _device.VideoSourceError -= OnVideoSourceError;
            try
            {
                if (_device.IsRunning)
                {
                    _device.SignalToStop();
                    if (!_device.WaitForStop(TimeSpan.FromSeconds(2))) _device.Stop();
                }
            }
            catch { try { _device.Stop(); } catch { } }
            _device = null;
            ActiveDeviceName = string.Empty;
        }

        private void OnNewFrame(object sender, NewFrameEventArgs e)
        {
            Bitmap clone = null;
            try
            {
                clone = (Bitmap)e.Frame.Clone();
                var handler = FrameAvailable;
                if (handler != null) handler(this, new CameraFrameEventArgs(clone, DateTime.UtcNow));
                else clone.Dispose();
            }
            catch
            {
                if (clone != null) clone.Dispose();
            }
        }

        private void OnVideoSourceError(object sender, VideoSourceErrorEventArgs e)
        {
            LastError = e.Description ?? "Camera source error.";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CameraCaptureService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_sync) StopInternal();
        }
    }

    internal static class AForgeWaitExtensions
    {
        public static bool WaitForStop(this VideoCaptureDevice device, TimeSpan timeout)
        {
            var until = DateTime.UtcNow + timeout;
            while (device.IsRunning && DateTime.UtcNow < until) System.Threading.Thread.Sleep(20);
            return !device.IsRunning;
        }
    }
}
