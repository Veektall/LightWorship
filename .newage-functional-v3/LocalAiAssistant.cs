using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
                var args="-m "+Q(_model)+" -sysf "+Q(systemPath)+" -f "+Q(promptPath)+" -n 96 --temp 0.2 --ctx-size 1024 --log-disable --no-display-prompt --no-show-timings --simple-io --single-turn";
                var psi=new ProcessStartInfo(_exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Path.GetDirectoryName(_exe)};
                using(var p=new Process{StartInfo=psi})
                {
                    p.Start();
                    var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync();
                    var wait=Task.Run(()=>p.WaitForExit());
                    var completed=await Task.WhenAny(wait,Task.Delay(60000)).ConfigureAwait(false);
                    if(completed!=wait){try{p.Kill();}catch{} return "Local AI timed out. Nothing was sent to Program.";}
                    var output=(await stdout.ConfigureAwait(false)).Trim(); var error=(await stderr.ConfigureAwait(false)).Trim();
                    if(p.ExitCode!=0) return "Local AI failed safely: "+FirstLine(error);
                    var cleaned=Clean(output);
                    if(string.IsNullOrWhiteSpace(cleaned)) return "Local AI returned no suggestion. Nothing was sent to Program.";
                    return cleaned;
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
            if(string.IsNullOrWhiteSpace(output)) return "";
            var s=Regex.Replace(output,"\\x1B\\[[0-9;?]*[ -/]*[@-~]","").Trim();
            foreach(var marker in new[]{"<|im_end|>","<|im_start|>","<|assistant|>","Assistant:"}) s=s.Replace(marker,"").Trim();
            var lines=s.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)
                       .SelectLineText();
            return lines.Length>480?lines.Substring(0,480).Trim()+"…":lines;
        }
        private static void TryDelete(string path){try{if(File.Exists(path))File.Delete(path);}catch{}}
    }

    internal static class LocalAiOutputExtensions
    {
        public static string SelectLineText(this string[] lines)
        {
            if(lines==null||lines.Length==0)return "";
            var filtered=new System.Collections.Generic.List<string>();
            foreach(var raw in lines)
            {
                var line=(raw??"").Trim(); if(line.Length==0)continue;
                if(line.StartsWith("Loading model",StringComparison.OrdinalIgnoreCase) || line.StartsWith("build",StringComparison.OrdinalIgnoreCase) || line.StartsWith("model",StringComparison.OrdinalIgnoreCase) || line.StartsWith("ftype",StringComparison.OrdinalIgnoreCase) || line.StartsWith("modalities",StringComparison.OrdinalIgnoreCase))continue;
                if(line.IndexOf("██",StringComparison.Ordinal)>=0 || line.IndexOf("▄▄",StringComparison.Ordinal)>=0 || line.IndexOf("▀▀",StringComparison.Ordinal)>=0)continue;
                filtered.Add(line);
            }
            return string.Join(" ",filtered).Trim();
        }
    }
}
