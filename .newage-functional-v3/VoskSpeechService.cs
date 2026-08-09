using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NAudio.Wave;
using Vosk;

namespace NewAgeWorship.Desktop
{
    public sealed class VoskSpeechService : IDisposable
    {
        private readonly string _modelPath;
        private Model _model;
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private ManualResetEventSlim _stopped;
        private string _recordingPath;
        public bool IsReady => Directory.Exists(_modelPath);
        public bool IsRecording => _waveIn!=null;

        public VoskSpeechService(string modelPath){_modelPath=modelPath;Vosk.Vosk.SetLogLevel(-1);}
        private Model ModelInstance(){if(_model==null){if(!IsReady)throw new DirectoryNotFoundException("Vosk model is missing: "+_modelPath);_model=new Model(_modelPath);}return _model;}

        public string TranscribeWaveFile(string path)
        {
            if(!File.Exists(path)) throw new FileNotFoundException("Audio file not found",path);
            using(var reader=new WaveFileReader(path))
            {
                if(reader.WaveFormat.SampleRate!=16000 || reader.WaveFormat.Channels!=1 || reader.WaveFormat.BitsPerSample!=16)
                    throw new InvalidDataException("Voice commands require 16 kHz mono 16-bit PCM WAV input.");
                using(var rec=new VoskRecognizer(ModelInstance(),16000.0f))
                {
                    var buffer=new byte[4096]; int n;
                    while((n=reader.Read(buffer,0,buffer.Length))>0) rec.AcceptWaveform(buffer,n);
                    var json=rec.FinalResult();
                    var m=Regex.Match(json,"\\\"text\\\"\\s*:\\s*\\\"(?<t>[^\\\"]*)\\\"");
                    return m.Success?m.Groups["t"].Value.Trim():"";
                }
            }
        }

        public void StartPushToTalk(string path)
        {
            if(_waveIn!=null) throw new InvalidOperationException("Recording is already active.");
            if(WaveIn.DeviceCount<1) throw new InvalidOperationException("No command microphone is available.");
            _recordingPath=path; Directory.CreateDirectory(Path.GetDirectoryName(path));
            _stopped=new ManualResetEventSlim(false);
            _waveIn=new WaveInEvent{WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=100};
            _writer=new WaveFileWriter(path,_waveIn.WaveFormat);
            _waveIn.DataAvailable+=(s,e)=>{if(_writer!=null)_writer.Write(e.Buffer,0,e.BytesRecorded);};
            _waveIn.RecordingStopped+=(s,e)=>{try{_writer?.Dispose();}finally{_writer=null;_stopped.Set();}};
            _waveIn.StartRecording();
        }

        public string StopPushToTalk()
        {
            if(_waveIn==null) return null;
            _waveIn.StopRecording(); _stopped.Wait(3000); _waveIn.Dispose(); _waveIn=null; _stopped.Dispose(); _stopped=null; return _recordingPath;
        }

        public void Dispose(){try{if(_waveIn!=null)StopPushToTalk();}catch{} _model?.Dispose();}
    }
}
