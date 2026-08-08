using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace NewAgeWorship.Core.Services
{
    public sealed class StructuredAuditLogger
    {
        private readonly object _sync = new object(); private readonly string _path;
        public StructuredAuditLogger(string path){_path=path??throw new ArgumentNullException(nameof(path));var d=Path.GetDirectoryName(path);if(!string.IsNullOrWhiteSpace(d))Directory.CreateDirectory(d);}
        public void Write(string actor,string command,string result,string device="local",string service="")
        {var line=JsonConvert.SerializeObject(new{timestampUtc=DateTime.UtcNow,actor=actor??"",command=command??"",result=result??"",device=device??"",service=service??""},Formatting.None);lock(_sync)File.AppendAllText(_path,line+Environment.NewLine,new UTF8Encoding(false));}
    }

    public sealed class QuoteDetectionResult
    {public string ExactText{get;set;}=string.Empty;public double Confidence{get;set;}public IReadOnlyList<string> Reasons{get;set;}=Array.Empty<string>();}

    public sealed class QuoteCandidateDetector
    {
        private static readonly string[] Rhetorical={"remember this","the truth is","never forget","you cannot","you can never","the moment","when you","if you","until you","what you"};
        public QuoteDetectionResult Evaluate(string sentence,bool emphasized=false,bool repeated=false,double audienceResponse=0)
        {var text=(sentence??"").Trim();var reasons=new List<string>();if(text.Length<35||text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Length<7)return new QuoteDetectionResult{ExactText=text};double score=.35;if(text.Length<=220){score+=.12;reasons.Add("concise");}if(Rhetorical.Any(x=>text.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0)){score+=.15;reasons.Add("rhetorical structure");}if(emphasized){score+=.17;reasons.Add("speaker emphasis");}if(repeated){score+=.17;reasons.Add("repetition");}if(audienceResponse>=.6){score+=.10;reasons.Add("audience response");}if(text.EndsWith(".")||text.EndsWith("!")||text.EndsWith("?")){score+=.04;reasons.Add("sentence complete");}return new QuoteDetectionResult{ExactText=text,Confidence=Math.Min(.98,score),Reasons=reasons};}
    }

    public sealed class ServiceTimelineItem
    {public TimeSpan Timestamp{get;set;}public string Kind{get;set;}=string.Empty;public string Text{get;set;}=string.Empty;public double Confidence{get;set;}}
    public sealed class PostServiceReport
    {public List<ServiceTimelineItem> Timeline{get;}=new List<ServiceTimelineItem>();public List<string> Scriptures{get;}=new List<string>();public List<string> PrayerPoints{get;}=new List<string>();public List<QuoteDetectionResult> Quotes{get;}=new List<QuoteDetectionResult>();}

    /// <summary>Reflection is used only to decouple this orchestration layer from parser DTO names; source text stays exact.</summary>
    public sealed class PostServiceProcessor
    {
        private readonly object _scripture=new ScriptureReferenceParser();private readonly object _prayer=new PrayerPointExtractor();private readonly QuoteCandidateDetector _quotes=new QuoteCandidateDetector();
        public PostServiceReport Process(IEnumerable<Tuple<TimeSpan,string,double>> transcript)
        {var report=new PostServiceReport();foreach(var row in transcript??Enumerable.Empty<Tuple<TimeSpan,string,double>>()) {var text=row.Item2??"";var scr=InvokeBest(_scripture,"Parse",text,row.Item3);if(scr!=null){var reference=scr.ToString();if(!string.IsNullOrWhiteSpace(reference)){report.Scriptures.Add(reference);report.Timeline.Add(new ServiceTimelineItem{Timestamp=row.Item1,Kind="scripture",Text=reference,Confidence=row.Item3});}}var pr=InvokeBest(_prayer,"Extract",text,row.Item3);if(pr!=null){var exact=ReadString(pr,"ExactText","RawText","Text")??text;var cleaned=ReadString(pr,"CleanedText","CleanText","Text")??exact;var conf=ReadDouble(pr,"Confidence")??row.Item3;report.PrayerPoints.Add(cleaned);report.Timeline.Add(new ServiceTimelineItem{Timestamp=row.Item1,Kind="prayer",Text=exact,Confidence=conf});}var q=_quotes.Evaluate(text);if(q.Confidence>=.65){report.Quotes.Add(q);report.Timeline.Add(new ServiceTimelineItem{Timestamp=row.Item1,Kind="quote-candidate",Text=q.ExactText,Confidence=q.Confidence});}}return report;}
        private static object InvokeBest(object target,string name,string text,double confidence)
        {var methods=target.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public).Where(x=>x.Name==name).OrderByDescending(x=>x.GetParameters().Length);foreach(var m in methods){try{var p=m.GetParameters();if(p.Length==1&&p[0].ParameterType==typeof(string))return m.Invoke(target,new object[]{text});if(p.Length==2&&p[0].ParameterType==typeof(string)){object second=p[1].ParameterType==typeof(double)?(object)confidence:p[1].ParameterType==typeof(float)?(float)confidence:p[1].HasDefaultValue?p[1].DefaultValue:null;if(second!=null)return m.Invoke(target,new[]{(object)text,second});}}catch{}}return null;}
        private static string ReadString(object o,params string[] names){foreach(var n in names){var p=o.GetType().GetProperty(n);if(p!=null&&p.PropertyType==typeof(string))return(string)p.GetValue(o,null);}return null;}
        private static double? ReadDouble(object o,params string[] names){foreach(var n in names){var p=o.GetType().GetProperty(n);if(p==null)continue;var v=p.GetValue(o,null);if(v is double d)return d;if(v is float f)return f;}return null;}
    }

    public sealed class MediaTranscodeResult{public bool Success{get;set;}public string OutputPath{get;set;}=string.Empty;public string Error{get;set;}=string.Empty;}
    public sealed class LocalFfmpegTranscoder
    {
        private readonly string _ffmpeg;public LocalFfmpegTranscoder(string ffmpegPath){_ffmpeg=ffmpegPath??"";}
        public MediaTranscodeResult ConvertToSafeMp4(string inputPath,string outputPath)
        {if(!File.Exists(_ffmpeg))return new MediaTranscodeResult{Error="FFmpeg is not installed in the local application package."};if(!File.Exists(inputPath))return new MediaTranscodeResult{Error="Input media does not exist."};var d=Path.GetDirectoryName(outputPath);if(!string.IsNullOrWhiteSpace(d))Directory.CreateDirectory(d);var psi=new System.Diagnostics.ProcessStartInfo{FileName=_ffmpeg,Arguments="-hide_banner -nostdin -y -i \""+inputPath+"\" -map_metadata -1 -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -movflags +faststart \""+outputPath+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};try{using(var p=System.Diagnostics.Process.Start(psi)){var err=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0||!File.Exists(outputPath))return new MediaTranscodeResult{Error=err};}return new MediaTranscodeResult{Success=true,OutputPath=outputPath};}catch(Exception ex){return new MediaTranscodeResult{Error=ex.Message};}}
    }

    public sealed class PermissionedCommandGate
    {
        private static readonly HashSet<string> Nursery=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"nursery","alert","acknowledge"};
        private static readonly HashSet<string> ViewOnly=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool CanExecute(string role,string command,bool confirmed)
        {var cmd=(command??"").Trim().ToLowerInvariant();var high=cmd.Contains("blackout")||cmd.Contains("emergency")||cmd.Contains("delete")||cmd.Contains("finance");if(high&&!confirmed)return false;if(string.Equals(role,"View-only",StringComparison.OrdinalIgnoreCase))return false;if(string.Equals(role,"Nursery",StringComparison.OrdinalIgnoreCase))return Nursery.Any(cmd.Contains);return !string.IsNullOrWhiteSpace(role);}
    }
}
