using System;
using System.Speech.Recognition;

namespace LightWorship
{
    public class LiveListeningService : IDisposable
    {
        private SpeechRecognitionEngine _engine;
        private bool _running;

        public event EventHandler<RecognizedPhraseEventArgs> PhraseRecognized;
        public event EventHandler<Exception> ListenFailed;

        public bool IsRunning { get { return _running; } }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            try
            {
                _engine = new SpeechRecognitionEngine();
                _engine.LoadGrammar(new DictationGrammar());
                _engine.SetInputToDefaultAudioDevice();
                _engine.SpeechRecognized += OnSpeechRecognized;
                _engine.RecognizeAsync(RecognizeMode.Multiple);
                _running = true;
            }
            catch (Exception ex)
            {
                _running = false;
                Cleanup();
                if (ListenFailed != null)
                {
                    ListenFailed(this, ex);
                }
            }
        }

        public void Stop()
        {
            _running = false;
            Cleanup();
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e == null || e.Result == null || String.IsNullOrWhiteSpace(e.Result.Text))
            {
                return;
            }

            if (PhraseRecognized != null)
            {
                PhraseRecognized(this, new RecognizedPhraseEventArgs(e.Result.Text, e.Result.Confidence));
            }
        }

        private void Cleanup()
        {
            if (_engine == null)
            {
                return;
            }

            try
            {
                _engine.RecognizeAsyncCancel();
                _engine.RecognizeAsyncStop();
            }
            catch
            {
            }

            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.Dispose();
            _engine = null;
        }
    }

    public class RecognizedPhraseEventArgs : EventArgs
    {
        public string Text { get; private set; }
        public float Confidence { get; private set; }

        public RecognizedPhraseEventArgs(string text, float confidence)
        {
            Text = text;
            Confidence = confidence;
        }
    }
}
