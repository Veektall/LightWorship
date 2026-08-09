using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NewAgeWorship.Desktop
{
    public sealed class LocalAiAssistant
    {
        private readonly string _exe;
        private readonly string _model;
        public bool IsReady => File.Exists(_exe) && File.Exists(_model);
        public string RuntimePath => _exe;
        public string ModelPath => _model;

        public LocalAiAssistant(string baseDirectory)
        {
            _exe=Path.Combine(baseDirectory,"AI","llama","llama-cli.exe");
            _model=Path.Combine(baseDirectory,"AI","models","SmolLM2-360M-Instruct-Q4_K_M.gguf");
        }

        public async Task<string> CompleteAsync(string userText)
        {
            if(!IsReady) return "Local AI runtime or model is missing. Program output was not changed.";
            var tempRoot=Path.Combine(Path.GetTempPath(),"newage-worship-ai");Directory.CreateDirectory(tempRoot);
            var id=Guid.NewGuid().ToString("N");
            var systemPath=Path.Combine(tempRoot,"system-"+id+".txt");
            var promptPath=Path.Combine(tempRoot,"prompt-"+id+".txt");
            File.WriteAllText(systemPath,"You are NEWAGE WORSHIP's local operator assistant. Be concise, factual and operational. Never claim that you projected, changed Program, or performed an action. Suggestions remain private until a human approves them.",new UTF8Encoding(false));
            File.WriteAllText(promptPath,(userText??"").Trim(),new UTF8Encoding(false));
            try
            {
                var args="-m "+Q(_model)+" -sysf "+Q(systemPath)+" -f "+Q(promptPath)+" -n 96 --temp 0.2 --ctx-size 1024 --no-display-prompt --no-show-timings --simple-io --single-turn";
                var psi=new ProcessStartInfo(_exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Path.GetDirectoryName(_exe)};
                using(var p=new Process{StartInfo=psi})
                {
                    p.Start();
                    var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync();
                    var wait=Task.Run(()=>p.WaitForExit());
                    var completed=await Task.WhenAny(wait,Task.Delay(60000));
                    if(completed!=wait){try{p.Kill();}catch{} return "Local AI timed out. Nothing was sent to Program.";}
                    var output=(await stdout).Trim(); var error=(await stderr).Trim();
                    if(p.ExitCode!=0) return "Local AI failed safely: "+FirstLine(error);
                    if(string.IsNullOrWhiteSpace(output)) return "Local AI returned no suggestion. Nothing was sent to Program.";
                    return Clean(output);
                }
            }
            finally
            {
                TryDelete(systemPath);TryDelete(promptPath);
            }
        }

        private static string Q(string s)=>"\""+(s??"").Replace("\"","\\\"")+"\"";
        private static string FirstLine(string s){if(string.IsNullOrWhiteSpace(s))return "unknown error";var i=s.IndexOf('\n');return (i<0?s:s.Substring(0,i)).Trim();}
        private static string Clean(string output)
        {
            var s=output.Trim();
            foreach(var marker in new[]{"<|im_end|>","<|im_start|>","assistant","Assistant:"}) s=s.Replace(marker,"").Trim();
            var lines=s.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);if(lines.Length==0)return s;
            var joined=string.Join(" ",lines).Trim();return joined.Length>480?joined.Substring(0,480).Trim()+"…":joined;
        }
        private static void TryDelete(string path){try{if(File.Exists(path))File.Delete(path);}catch{}}
    }
}
