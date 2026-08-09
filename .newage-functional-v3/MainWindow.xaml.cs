using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NewAgeWorship.Core.Models;
using NewAgeWorship.Core.Services;

namespace NewAgeWorship.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<SceneDefinition> _scenes=new ObservableCollection<SceneDefinition>();
        private readonly PresentationController _presentation;
        private readonly RecoveryStore _store;
        private readonly ScriptureReferenceParser _scripture=new ScriptureReferenceParser();
        private readonly BibleLibrary _bible;
        private readonly LocalAiAssistant _ai;
        private readonly VoskSpeechService _speech;
        private readonly WhisperSpeechService _whisper;
        private readonly MixerAudioService _mixer;
        private AudioHealthSnapshot _lastMixerHealth;
        private BiblePassage _lastBiblePassage;
        private Process _programHost;
        private readonly ProgramSnapshotPublisher _publisher;
        private LocalCompanionServer _phone;
        private readonly string _token;

        public MainWindow()
        {
            InitializeComponent();
            var data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NEWAGE WORSHIP");Directory.CreateDirectory(data);
            _store=new RecoveryStore(Path.Combine(data,"newage-worship.db"));
            var recovered=_store.Load();_presentation=new PresentationController(recovered);
            _publisher=new ProgramSnapshotPublisher(Path.Combine(data,"program.snapshot.json"));_publisher.Publish(_presentation.State.Program);
            _presentation.StateChanged+=Presentation_StateChanged;
            _token=CreateToken();PhoneToken.Text=_token;
            var baseDir=AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                _bible=new BibleLibrary(data,Path.Combine(baseDir,"Data","Bible_Versions.zip"));
                BibleVersionCombo.ItemsSource=_bible.Translations;
                var preferred=_bible.Translations.FirstOrDefault(x=>string.Equals(x.Code,"KJV",StringComparison.OrdinalIgnoreCase)) ?? _bible.Translations.FirstOrDefault();
                BibleVersionCombo.SelectedItem=preferred;
                UpdateBibleMeta();
            }
            catch(Exception ex){BibleMetaText.Text="Bible unavailable • "+ex.Message;}
            _ai=new LocalAiAssistant(baseDir);AiStatus.Text=_ai.IsReady?"LOCAL READY":"MODEL MISSING";
            _speech=new VoskSpeechService(Path.Combine(baseDir,"Models","vosk-model-small-en-us-0.15"));
            _whisper=new WhisperSpeechService(baseDir);
            AudioStatus.Text=_speech.IsReady?(_whisper.IsReady?"DUAL ASR READY":"VOSK READY"):"MODEL MISSING";
            _mixer=new MixerAudioService();
            MixerDeviceCombo.ItemsSource=_mixer.GetDevices();if(MixerDeviceCombo.Items.Count>0)MixerDeviceCombo.SelectedIndex=0;
            _mixer.HealthChanged+=(o,h)=>Dispatcher.BeginInvoke(new Action(()=>{_lastMixerHealth=h;MixerStatusText.Text=h.DeviceName+" • "+h.Summary;Refresh();}));
            if(MixerDeviceCombo.Items.Count==0)MixerStatusText.Text="No mixer/audio input device detected on this computer.";
            SeedScenes();SceneList.ItemsSource=_scenes;SceneList.SelectedIndex=0;Refresh();
            _store.Audit("system","startup","ready","desktop");
        }

        private void SeedScenes()
        {
            _scenes.Add(SceneFactory.SafeReady());
            var welcome=new SceneDefinition{Name="Welcome",Background="#FF0B1B2B"};
            welcome.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="WELCOME",X=.08,Y=.18,Width=.84,Height=.12,FontSize=24,Foreground="#FF79DCD0",ZIndex=1});
            welcome.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="We are glad you are here",X=.08,Y=.34,Width=.84,Height=.24,FontSize=58,ZIndex=2});
            welcome.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="NEWAGE WORSHIP",X=.08,Y=.68,Width=.84,Height=.08,FontSize=19,Foreground="#FF8FA8BD",ZIndex=3});_scenes.Add(welcome);
            var prayer=new SceneDefinition{Name="Prayer Point",Background="#FF151124"};
            prayer.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="PRAYER POINT",X=.08,Y=.16,Width=.84,Height=.10,FontSize=22,Foreground="#FFC4A9FF",ZIndex=1});
            prayer.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="Lord, lead us with wisdom and courage.",X=.08,Y=.31,Width=.84,Height=.36,FontSize=46,ZIndex=2});
            prayer.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="Pray with faith",X=.08,Y=.72,Width=.84,Height=.08,FontSize=18,Foreground="#FF9C8EBB",ZIndex=3});_scenes.Add(prayer);
        }

        private void SceneList_SelectionChanged(object sender,SelectionChangedEventArgs e){var s=SceneList.SelectedItem as SceneDefinition;if(s!=null)_presentation.SetPreview(s);}
        private void TakeLive_Click(object sender,RoutedEventArgs e){_presentation.TakeLive();_store.Audit("operator","take-live","ok","desktop");}
        private void Freeze_Click(object sender,RoutedEventArgs e){_presentation.Freeze(!_presentation.State.Frozen);_store.Audit("operator","freeze",_presentation.State.Frozen.ToString(),"desktop");}
        private void Blackout_Click(object sender,RoutedEventArgs e){_presentation.Blackout();_store.Audit("operator","blackout","ok","desktop");}
        private void Logo_Click(object sender,RoutedEventArgs e){_presentation.ShowLogo();_store.Audit("operator","logo","ok","desktop");}
        private void Emergency_Click(object sender,RoutedEventArgs e){_presentation.Emergency("PLEASE FOLLOW THE SERVICE TEAM'S INSTRUCTIONS");_store.Audit("operator","emergency","ok","desktop");}
        private void AddTextScene_Click(object sender,RoutedEventArgs e){var s=new SceneDefinition{Name="New Text Scene",Background="#FF172436"};s.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content="Design Studio text scene",X=.08,Y=.3,Width=.84,Height=.4,FontSize=42});_scenes.Add(s);SceneList.SelectedItem=s;}
        private void AddScriptureScene_Click(object sender,RoutedEventArgs e){CommandBox.Text="John 3:16";ParseScripture_Click(sender,e);}

        private void LookupBible_Click(object sender,RoutedEventArgs e)
        {
            if(_bible==null){CommandResult.Text="Bible library is unavailable. Nothing changed.";return;}
            ScriptureReference reference;BiblePassage passage;
            if(_scripture.TryParse(CommandBox.Text,out reference)||_scripture.TryParseSpoken(CommandBox.Text,out reference))
            {
                if(_bible.TryGetPassage(reference,ActiveBibleVersion(),out passage)){_lastBiblePassage=passage;CommandBox.Text=passage.Reference;CommandResult.Text=PassageSummary(passage);return;}
                CommandResult.Text="Reference exists syntactically but no verse was found in "+ActiveBibleVersion()+".";return;
            }
            var results=_bible.Search(CommandBox.Text,ActiveBibleVersion(),3);if(results.Count==0){CommandResult.Text="No local "+ActiveBibleVersion()+" Bible match found.";return;}
            _lastBiblePassage=results[0];CommandResult.Text=string.Join("\n",results.Select(x=>x.Reference+" • "+x.Translation+" — "+Short(x.Text,95)));
        }

        private void ParseScripture_Click(object sender,RoutedEventArgs e)
        {
            if(_bible==null){CommandResult.Text="Bible library is unavailable. Nothing projected.";return;}
            ScriptureReference r;if(!_scripture.TryParse(CommandBox.Text,out r)&&!_scripture.TryParseSpoken(CommandBox.Text,out r)){LookupBible_Click(sender,e);if(_lastBiblePassage==null)return;if(!_scripture.TryParse(_lastBiblePassage.Reference,out r))return;}
            BiblePassage p;if(!_bible.TryGetPassage(r,ActiveBibleVersion(),out p)){CommandResult.Text="Verse not found in "+ActiveBibleVersion()+".";return;}
            PrepareBiblePassage(p,"operator");
        }

        private void PrepareBiblePassage(BiblePassage p,string source)
        {
            _lastBiblePassage=p;var s=new SceneDefinition{Name=p.Reference+" • "+p.Translation,Background="#FF0B2430"};
            s.Layers.Add(new LayerDefinition{Kind=LayerKind.Scripture,Content=p.Text,X=.08,Y=.22,Width=.84,Height=.48,FontSize=p.Text.Length>220?34:(p.Text.Length>130?42:50),ZIndex=1});
            s.Layers.Add(new LayerDefinition{Kind=LayerKind.Text,Content=p.Reference+"  •  "+p.Translation,X=.10,Y=.75,Width=.80,Height=.10,FontSize=22,Foreground="#FF78D8CB",ZIndex=2});
            _scenes.Add(s);SceneList.SelectedItem=s;CommandBox.Text=p.Reference;CommandResult.Text=p.Reference+" • "+p.Translation+" • held in Preview\n"+p.Text;_presentation.State.ActiveScriptureReference=p.Reference;_store.Audit(source,"scripture-preview",p.Reference+" • "+p.Translation,"desktop");
        }

        private async void AiAssist_Click(object sender,RoutedEventArgs e)
        {
            AiAssistButton.IsEnabled=false;AiStatus.Text="THINKING";AiOutput.Text="Running locally. Program remains untouched.";
            try{AiOutput.Text=await _ai.CompleteAsync(AiPromptBox.Text);AiStatus.Text=_ai.IsReady?"LOCAL READY":"MODEL MISSING";_store.Audit("ai","suggestion","ok","local-model");}
            catch(Exception ex){AiOutput.Text="Local AI failed safely: "+ex.Message;AiStatus.Text="SAFE FAILURE";}
            finally{AiAssistButton.IsEnabled=true;}
        }

        private void MonitorMixer_Click(object sender,RoutedEventArgs e)
        {
            try
            {
                if(_mixer.IsMonitoring){_mixer.Stop();MonitorMixerButton.Content="MONITOR";MixerStatusText.Text="Mixer monitoring stopped.";Refresh();return;}
                if(MixerDeviceCombo.SelectedIndex<0)throw new InvalidOperationException("Select a mixer/audio input device first.");
                _mixer.Start(MixerDeviceCombo.SelectedIndex,48000);MonitorMixerButton.Content="STOP";MixerStatusText.Text="Monitoring the selected mixer feed at 48 kHz…";Refresh();
            }
            catch(Exception ex){MixerStatusText.Text=ex.Message;MonitorMixerButton.Content="MONITOR";}
        }

        private async void PushToTalk_Click(object sender,RoutedEventArgs e)
        {
            if(!_speech.IsReady){AudioStatus.Text="MODEL MISSING";AudioTranscript.Text="Offline speech model is not installed.";return;}
            try
            {
                if(!_speech.IsRecording)
                {
                    var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NEWAGE WORSHIP","Voice");Directory.CreateDirectory(dir);
                    _speech.StartPushToTalk(Path.Combine(dir,"command-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".wav"));PushToTalkButton.Content="RELEASE / PROCESS";AudioStatus.Text="LISTENING";AudioTranscript.Text="Command microphone is recording locally…";
                }
                else
                {
                    var path=_speech.StopPushToTalk();PushToTalkButton.Content="PUSH TO TALK";await ProcessAudioFileAsync(path);
                }
            }
            catch(Exception ex){PushToTalkButton.Content="PUSH TO TALK";AudioStatus.Text="MIC UNAVAILABLE";AudioTranscript.Text=ex.Message;}
        }

        private async void TranscribeFile_Click(object sender,RoutedEventArgs e)
        {
            var dlg=new OpenFileDialog{Filter="16 kHz mono PCM WAV|*.wav",Title="Transcribe command audio"};if(dlg.ShowDialog(this)==true)await ProcessAudioFileAsync(dlg.FileName);
        }

        private async Task ProcessAudioFileAsync(string path)
        {
            AudioStatus.Text="TRANSCRIBING";AudioTranscript.Text="Offline Vosk recognition is running…";
            try
            {
                var voskText=await Task.Run(()=>_speech.TranscribeWaveFile(path));
                var whisperText=_whisper.IsReady?await Task.Run(()=>_whisper.TranscribeWaveFile(path)):null;
                var text=!string.IsNullOrWhiteSpace(voskText)?voskText:whisperText;
                if(string.IsNullOrWhiteSpace(text)){AudioStatus.Text="NO SPEECH";AudioTranscript.Text="No command speech was recognised.";return;}
                AudioStatus.Text=_whisper.IsReady?"DUAL ASR READY":"VOSK READY";
                var transcript="Vosk: “"+voskText+"”"+(_whisper.IsReady?"\nWhisper: “"+(whisperText??"")+"”":"");AudioTranscript.Text=transcript;
                ScriptureReference r;var parsed=_scripture.TryParseSpoken(voskText,out r)||(!string.IsNullOrWhiteSpace(whisperText)&&_scripture.TryParseSpoken(whisperText,out r));
                if(parsed&&_bible!=null){BiblePassage p;if(_bible.TryGetPassage(r,ActiveBibleVersion(),out p)){PrepareBiblePassage(p,"voice");AudioTranscript.Text=transcript+"\nSuggested: "+p.Reference+" • "+p.Translation+" • held in Preview";}}
                _store.Audit("voice","transcript",transcript,_whisper.IsReady?"vosk+whisper":"vosk");
            }
            catch(Exception ex){AudioStatus.Text="SAFE FAILURE";AudioTranscript.Text=ex.Message;}
        }

        private void OpenProgram_Click(object sender,RoutedEventArgs e){try{if(_programHost!=null&&!_programHost.HasExited)return;var exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NewAgeWorship.ProgramHost.exe");if(!File.Exists(exe)){FooterStatus.Text="Program Host executable is missing; Program remains protected in operator preview only.";return;}_publisher.Publish(_presentation.State.Program);_programHost=Process.Start(new ProcessStartInfo(exe,"--screen 1"){UseShellExecute=false});_store.Audit("operator","program-host-start","ok","desktop");}catch(Exception){FooterStatus.Text="Program Host could not start. Last valid Program remains unchanged.";}}
        private void StartPhone_Click(object sender,RoutedEventArgs e){try{if(_phone==null){_phone=new LocalCompanionServer(_presentation,8765,_token);_phone.Start();PhoneStatus.Text="Running";}else{_phone.Dispose();_phone=null;PhoneStatus.Text="Stopped";}}catch(Exception){PhoneStatus.Text="Bind failed";}}
        private void Save_Click(object sender,RoutedEventArgs e){_store.Save(_presentation.State);FooterStatus.Text="Saved at "+DateTime.Now.ToLongTimeString();}
        private void Presentation_StateChanged(object sender,EventArgs e){Dispatcher.BeginInvoke(new Action(()=>{_store.Save(_presentation.State);_publisher.Publish(_presentation.State.Program);Refresh();}));}
        private void Refresh(){SceneRenderer.Render(_presentation.State.Preview,PreviewSurface);SceneRenderer.Render(_presentation.State.Program,ProgramSurface);ModeBadge.Text=_presentation.State.Mode.ToString().ToUpperInvariant();ProfileBadge.Text=_presentation.State.Profile.ToString().ToUpperInvariant();ProgramStatus.Text=_presentation.State.Frozen?"FROZEN":"LIVE";FreezeButton.Content=_presentation.State.Frozen?"UNFREEZE":"FREEZE";PreviewSceneName.Text=_presentation.State.Preview!=null?_presentation.State.Preview.Name:"No preview";PreviewFlowName.Text=_presentation.State.Preview!=null?_presentation.State.Preview.Name:"No preview";ProgramFlowName.Text=_presentation.State.Program!=null?_presentation.State.Program.Name:"No program";SceneCountText.Text=_scenes.Count+" scenes";SystemStatus.Text="PROGRAM  "+(_presentation.State.Program!=null?_presentation.State.Program.Name:"None")+"\nPREVIEW   "+(_presentation.State.Preview!=null?_presentation.State.Preview.Name:"None")+"\nBIBLE     "+(_bible!=null?ActiveBibleVersion()+" • "+_bible.Translations.Count+" versions":"Unavailable")+"\nAI        "+(_ai.IsReady?"Local model ready":"Unavailable")+"\nASR       "+(_speech.IsReady?(_whisper.IsReady?"Vosk + Whisper ready":"Vosk ready / Whisper missing"):"Unavailable")+"\nMIXER     "+(_mixer.IsMonitoring?(_lastMixerHealth!=null?_lastMixerHealth.Status:"Monitoring"):(MixerDeviceCombo.Items.Count>0?"Available / stopped":"No input device"));}

        private string ActiveBibleVersion()
        {
            var item=BibleVersionCombo!=null?BibleVersionCombo.SelectedItem as BibleTranslationInfo:null;
            return item!=null?item.Code:(_bible!=null&&_bible.Translations.Count>0?_bible.Translations[0].Code:"KJV");
        }

        private void BibleVersion_SelectionChanged(object sender,SelectionChangedEventArgs e)
        {
            UpdateBibleMeta();
            if(_bible!=null && CommandResult!=null) CommandResult.Text="Using "+ActiveBibleVersion()+" offline. Search by reference or phrase.";
            if(_ai!=null && _speech!=null) Refresh();
        }

        private void UpdateBibleMeta()
        {
            if(BibleMetaText==null)return;
            if(_bible==null){BibleMetaText.Text="Bible unavailable";return;}
            var v=ActiveBibleVersion();
            BibleMetaText.Text=v+" • 66 books • user-supplied offline pack • "+_bible.Translations.Count+" versions";
        }

        private static string PassageSummary(BiblePassage p)
        {
            var note=!string.Equals(p.Reference,p.RequestedReference,StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(p.RequestedReference)?"\nSource grouping: "+p.Reference+" for requested "+p.RequestedReference:"";
            return p.Reference+" • "+p.Translation+note+"\n"+p.Text;
        }

        protected override void OnClosed(EventArgs e){try{_store.Save(_presentation.State);}catch{}try{_phone?.Dispose();}catch{}try{_speech?.Dispose();}catch{}try{_mixer?.Dispose();}catch{}try{_bible?.Dispose();}catch{}_store.Dispose();base.OnClosed(e);}
        private static string Short(string s,int n)=>string.IsNullOrEmpty(s)?"":(s.Length<=n?s:s.Substring(0,n).Trim()+"…");
        private static string CreateToken(){var b=new byte[9];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return Convert.ToBase64String(b).Replace("+","").Replace("/","").Replace("=","");}
    }
}
