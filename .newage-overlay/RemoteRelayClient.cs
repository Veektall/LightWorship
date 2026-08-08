using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace NewAgeWorship.Core.Services
{
    public sealed class RemoteRelayCommand
    {
        public string Id { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// Outbound-only HTTPS relay client. It never opens an inbound port and refuses non-HTTPS relay URLs.
    /// Loss of this client has no dependency relationship with local presentation.
    /// </summary>
    public sealed class RemoteRelayClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _baseUri;
        private readonly string _sessionId;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread _thread;

        public RemoteRelayClient(string relayUrl, string sessionId, string bearerToken, TimeSpan? timeout = null)
        {
            if (!Uri.TryCreate(relayUrl, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Remote relay must use HTTPS.", nameof(relayUrl));
            _baseUri = uri;
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            _http = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(20) };
            if (!string.IsNullOrWhiteSpace(bearerToken)) _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        public event Action<RemoteRelayCommand> CommandReceived;
        public string LastError { get; private set; } = string.Empty;
        public bool Running => _thread != null && _thread.IsAlive;

        public void Start()
        {
            if (Running) return;
            _thread = new Thread(PollLoop) { IsBackground = true, Name = "NEWAGE Remote Relay" };
            _thread.Start();
        }

        public void PublishSnapshot(object snapshot)
        {
            var json = JsonConvert.SerializeObject(snapshot, Formatting.None);
            var url = new Uri(_baseUri, "api/session/" + Uri.EscapeDataString(_sessionId) + "/snapshot");
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = _http.PostAsync(url, content).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
            }
        }

        private void PollLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var url = new Uri(_baseUri, "api/session/" + Uri.EscapeDataString(_sessionId) + "/commands?wait=15");
                    var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
                    var commands = JsonConvert.DeserializeObject<List<RemoteRelayCommand>>(json) ?? new List<RemoteRelayCommand>();
                    LastError = string.Empty;
                    foreach (var command in commands)
                    {
                        var handler = CommandReceived;
                        if (handler != null) handler(command);
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    if (_cts.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(3))) break;
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_thread != null && _thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(2));
            _http.Dispose();
            _cts.Dispose();
        }
    }
}
