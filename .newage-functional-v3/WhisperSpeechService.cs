using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NewAgeWorship.Desktop
{
    public sealed class WhisperSpeechService
    {
        private readonly string _exe;
        private readonly string _model;
        public bool IsReady => File.Exists(_exe)&&File.Exists(_model);
        public WhisperSpeechService(string baseDirectory)
        {
            _exe=Path.Combine(baseDirectory,"AI","whisper","whisper-cli.exe");
            _model=Path.Combine(baseDirectory,"Models","whisper","ggml-tiny.en.bin");
        }
        public string TranscribeWaveFile(string wavPath)
        {
            if(!IsReady) return null;
            if(!File.Exists(wavPath)) throw new FileNotFoundException("Audio file not found",wavPath);
            var prefix=Path.Combine(Path.GetTempPath(),"newage-whisper-"+Guid.NewGuid().ToString("N"));
            var args="-m "+Q(_model)+" -f "+Q(wavPath)+" -l en -otxt -of "+Q(prefix)+" -nt -np";
            var psi=new ProcessStartInfo(_exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Path.GetDirectoryName(_exe)};
            using(var p=Process.Start(psi))
            {
                if(!p.WaitForExit(60000)){try{p.Kill();}catch{}throw new TimeoutException("Whisper validation timed out.");}
                var err=p.StandardError.ReadToEnd();if(p.ExitCode!=0)throw new InvalidOperationException("Whisper failed: "+FirstLine(err));
            }
            var txt=prefix+".txt";try{return File.Exists(txt)?File.ReadAllText(txt).Trim():"";}finally{try{if(File.Exists(txt))File.Delete(txt);}catch{}}
        }
        private static string Q(string s)=>"\""+(s??"").Replace("\"","\\\"")+"\"";
        private static string FirstLine(string s){if(string.IsNullOrWhiteSpace(s))return "unknown error";var i=s.IndexOf('\n');return (i<0?s:s.Substring(0,i)).Trim();}
    }
}
