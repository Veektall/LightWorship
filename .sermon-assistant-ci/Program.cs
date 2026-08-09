using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SermonAssistantCompanion
{
    public sealed class ProjectionState
    {
        private readonly object gate = new object();
        private string mode = "CLEAR", title = "", body = "", backgroundMode = "";
        public event Action Changed;
        public string Mode { get { lock (gate) return mode; } }
        public string Title { get { lock (gate) return title; } }
        public string Body { get { lock (gate) return body; } }
        public string BackgroundMode { get { lock (gate) return backgroundMode; } }
        public void Set(string m, string t, string b)
        {
            lock (gate) { mode = m ?? "CLEAR"; title = t ?? ""; body = b ?? ""; }
            Fire();
        }
        public void SetSegment(string m, string t, string b)
        {
            lock (gate) { backgroundMode = m ?? ""; mode = m ?? "MEDIA"; title = t ?? ""; body = b ?? ""; }
            Fire();
        }
        public void SetMedia(string m, string t, string b)
        {
            lock (gate) { backgroundMode = m ?? "MEDIA"; mode = m ?? "MEDIA"; title = t ?? ""; body = b ?? ""; }
            Fire();
        }
        public void Clear()
        {
            lock (gate) { mode = "CLEAR"; title = ""; body = ""; backgroundMode = ""; }
            Fire();
        }
        private void Fire(){ Action h = Changed; if (h != null) h(); }
    }

    public sealed class ParsedCommand
    {
        public string Command;
        public Dictionary<string,string> Fields = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public string Get(string key) { string v; return Fields.TryGetValue(key, out v) ? v : ""; }
    }

    public static class WireProtocol
    {
        public static string Decode(string s) { try { return Uri.UnescapeDataString((s ?? "").Replace("+", "%20")); } catch { return s ?? ""; } }
        public static ParsedCommand Parse(string line)
        {
            if (line == null) throw new ArgumentNullException("line");
            string[] p = line.TrimEnd('\r','\n').Split('\t');
            ParsedCommand c = new ParsedCommand(); c.Command = p.Length == 0 ? "" : p[0].Trim().ToUpperInvariant();
            for (int i=1;i<p.Length;i++) { int eq=p[i].IndexOf('='); if(eq<=0) continue; c.Fields[p[i].Substring(0,eq)] = Decode(p[i].Substring(eq+1)); }
            return c;
        }
    }

    public sealed class ProjectorServer
    {
        private readonly ProjectionState state; private readonly int port; private readonly string logPath;
        private TcpListener tcp; private UdpClient udp; private Thread tcpThread, udpThread; private volatile bool running;
        public bool Running { get { return running; } }
        public string LastClient = "none";
        public event Action StatusChanged;
        public ProjectorServer(ProjectionState s, int p, string log) { state=s;port=p;logPath=log; }
        public void Start()
        {
            if (running) return; running=true;
            tcp = new TcpListener(IPAddress.Any, port); tcp.Start();
            tcpThread=new Thread(TcpLoop);tcpThread.IsBackground=true;tcpThread.Start();
            if (port == 9077) { udpThread=new Thread(UdpLoop);udpThread.IsBackground=true;udpThread.Start(); }
            Notify(); Log("SERVER|START|"+port);
        }
        public void Stop()
        {
            running=false; try{if(tcp!=null)tcp.Stop();}catch{} try{if(udp!=null)udp.Close();}catch{} Notify(); Log("SERVER|STOP");
        }
        private void TcpLoop()
        {
            while(running) {
                try { TcpClient client=tcp.AcceptTcpClient(); Thread t=new Thread(delegate(){HandleClient(client);});t.IsBackground=true;t.Start(); }
                catch { if(!running)break; Thread.Sleep(100); }
            }
        }
        private void HandleClient(TcpClient client)
        {
            try {
                client.NoDelay=true; IPEndPoint ep=client.Client.RemoteEndPoint as IPEndPoint; LastClient=ep==null?"client":ep.Address.ToString(); Notify();
                using(client) using(StreamReader r=new StreamReader(client.GetStream(),new UTF8Encoding(false))) using(StreamWriter w=new StreamWriter(client.GetStream(),new UTF8Encoding(false))) {
                    w.AutoFlush=true; string line;
                    while(running && (line=r.ReadLine())!=null) { if(line.Length==0)continue; ParsedCommand c=WireProtocol.Parse(line); Apply(c); w.WriteLine("OK"); if(c.Command=="QUIT")break; }
                }
            } catch(Exception ex) { Log("CLIENT|ERROR|"+Clean(ex.Message)); }
            finally { LastClient="none";Notify(); }
        }
        private void Apply(ParsedCommand c)
        {
            string cmd=c.Command;
            if(cmd=="HELLO") { Log("HELLO|"+Clean(c.Get("device"))); return; }
            if(cmd=="PING") return;
            if(cmd=="CLEAR") state.Clear();
            else if(cmd=="CAPTION") state.Set("CAPTION","",c.Get("text"));
            else if(cmd=="SCRIPTURE") { string ver=c.Get("version"); string ttl=c.Get("ref")+(ver.Length>0?" ("+ver+")":""); state.Set("SCRIPTURE",ttl,c.Get("text")); }
            else if(cmd=="PRAYER") state.Set("PRAYER","PRAYER POINT",c.Get("text"));
            else if(cmd=="SEGMENT") state.SetSegment(c.Get("name"),c.Get("title"),c.Get("body"));
            else if(cmd=="MEDIA") state.SetMedia(c.Get("style").Length>0?c.Get("style"):"MEDIA",c.Get("title"),c.Get("body"));
            else if(cmd=="QUIT") { Log("CONTROL|QUIT"); running=false; try{tcp.Stop();}catch{} }
            else { Log("UNKNOWN|"+Clean(cmd)); return; }
            Log("STATE|"+Clean(state.Mode)+"|"+Clean(state.Title)+"|"+Clean(state.Body));
        }
        private void UdpLoop()
        {
            try {
                udp=new UdpClient(9076); udp.EnableBroadcast=true; IPEndPoint any=new IPEndPoint(IPAddress.Any,0);
                while(running) {
                    byte[] data=udp.Receive(ref any); string q=Encoding.UTF8.GetString(data);
                    if(q.Trim()=="SERMON_ASSISTANT_DISCOVER_V1") { string resp="SERMON_ASSISTANT_COMPANION_V1|"+Environment.MachineName+"|9077"; byte[] b=Encoding.UTF8.GetBytes(resp);udp.Send(b,b.Length,any); }
                }
            } catch { }
        }
        private void Notify(){Action h=StatusChanged;if(h!=null)h();}
        private static string Clean(string s){return (s??"").Replace("\r"," ").Replace("\n"," ").Replace("|","/");}
        private void Log(string s)
        {
            if(string.IsNullOrEmpty(logPath))return;
            try{lock(this){File.AppendAllText(logPath,DateTime.UtcNow.ToString("o")+"|"+s+Environment.NewLine,Encoding.UTF8);}}catch{}
        }
    }

    public sealed class ProjectionForm : Form
    {
        private readonly ProjectionState state;
        private readonly string mediaDir;
        public ProjectionForm(ProjectionState s)
        {
            state=s; mediaDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media"); FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Black;DoubleBuffered=true;Cursor=Cursors.Default;
            state.Changed += OnStateChanged;
        }
        public void MoveToScreen(Screen screen){ if(screen==null)return; StartPosition=FormStartPosition.Manual; Bounds=screen.Bounds; WindowState=FormWindowState.Normal; Bounds=screen.Bounds; }
        private void OnStateChanged(){ if(IsDisposed)return; try{if(InvokeRequired)BeginInvoke((MethodInvoker)delegate{Invalidate();});else Invalidate();}catch{} }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            string mode=state.Mode,title=state.Title,body=state.Body,backgroundMode=state.BackgroundMode;
            string visualMode=(mode=="CAPTION" && !string.IsNullOrEmpty(backgroundMode))?backgroundMode:mode;
            Color a,b;Palette(visualMode,out a,out b); using(LinearGradientBrush bg=new LinearGradientBrush(ClientRectangle,a,b,35f))g.FillRectangle(bg,ClientRectangle);
            if(mode=="CLEAR")return;
            DrawMediaBackground(g,mode=="CAPTION"?backgroundMode:mode);
            int w=ClientSize.Width,h=ClientSize.Height; Rectangle content=new Rectangle((int)(w*0.07),(int)(h*0.08),(int)(w*0.86),(int)(h*0.84));
            if(mode=="CAPTION") {
                Rectangle band=new Rectangle((int)(w*0.04),(int)(h*0.70),(int)(w*0.92),(int)(h*0.22)); using(Brush overlay=new SolidBrush(Color.FromArgb(210,0,0,0)))g.FillRectangle(overlay,band);
                DrawFit(g,body,new Rectangle(band.X+30,band.Y+20,band.Width-60,band.Height-40),Math.Max(24,w/28),FontStyle.Bold,StringAlignment.Center);
                return;
            }
            if(title.Length>0) DrawFit(g,title,new Rectangle(content.X,content.Y,content.Width,(int)(content.Height*0.22)),Math.Max(30,w/22),FontStyle.Bold,StringAlignment.Center);
            Rectangle bodyRect=new Rectangle(content.X,content.Y+(int)(content.Height*0.24),content.Width,(int)(content.Height*0.70));
            DrawFit(g,body,bodyRect,Math.Max(28,w/30),FontStyle.Regular,StringAlignment.Center);
            using(Pen p=new Pen(Color.FromArgb(150,255,255,255),2))g.DrawLine(p,content.X+(int)(content.Width*.2),content.Y+(int)(content.Height*.225),content.Right-(int)(content.Width*.2),content.Y+(int)(content.Height*.225));
        }
        private void DrawMediaBackground(Graphics g,string mode)
        {
            try {
                string[] ext={".jpg",".jpeg",".png",".bmp"}; string file=null;
                for(int i=0;i<ext.Length;i++){string p=Path.Combine(mediaDir,(mode??"MEDIA").ToUpperInvariant()+ext[i]);if(File.Exists(p)){file=p;break;}}
                if(file==null)return;
                using(Image img=Image.FromFile(file)){
                    Rectangle dst=ClientRectangle; float scale=Math.Max(dst.Width/(float)img.Width,dst.Height/(float)img.Height);
                    int sw=(int)(dst.Width/scale), sh=(int)(dst.Height/scale); int sx=Math.Max(0,(img.Width-sw)/2), sy=Math.Max(0,(img.Height-sh)/2);
                    g.DrawImage(img,dst,new Rectangle(sx,sy,Math.Min(sw,img.Width),Math.Min(sh,img.Height)),GraphicsUnit.Pixel);
                }
                using(Brush shade=new SolidBrush(Color.FromArgb(115,0,0,0)))g.FillRectangle(shade,ClientRectangle);
            } catch { }
        }

        private static void DrawFit(Graphics g,string text,Rectangle r,int start,FontStyle style,StringAlignment align)
        {
            if(string.IsNullOrEmpty(text))return; int size=start; Font f=null; SizeF m;
            do { if(f!=null)f.Dispose(); f=new Font("Arial",size,style,GraphicsUnit.Pixel); m=g.MeasureString(text,f,r.Width); if(m.Height<=r.Height)break; size-=2; } while(size>=18);
            using(f) using(Brush br=new SolidBrush(Color.White)) { StringFormat sf=new StringFormat();sf.Alignment=align;sf.LineAlignment=StringAlignment.Center;sf.Trimming=StringTrimming.EllipsisWord;g.DrawString(text,f,br,r,sf);sf.Dispose(); }
        }
        private static void Palette(string mode,out Color a,out Color b)
        {
            string m=(mode??"").ToUpperInvariant();
            if(m=="SCRIPTURE"){a=Color.FromArgb(24,49,83);b=Color.FromArgb(48,90,130);}
            else if(m=="PRAYER"){a=Color.FromArgb(61,29,76);b=Color.FromArgb(112,55,126);}
            else if(m=="OFFERING"){a=Color.FromArgb(33,75,45);b=Color.FromArgb(75,119,70);}
            else if(m=="TESTIMONY"){a=Color.FromArgb(98,64,15);b=Color.FromArgb(173,119,28);}
            else if(m=="WORSHIP"||m=="PRAISE"){a=Color.FromArgb(49,25,95);b=Color.FromArgb(97,58,150);}
            else if(m=="ALTAR_CALL"){a=Color.FromArgb(87,26,26);b=Color.FromArgb(150,52,52);}
            else {a=Color.FromArgb(18,45,67);b=Color.FromArgb(32,75,103);}
        }
    }

    public sealed class ControlForm : Form
    {
        private readonly ProjectionState state=new ProjectionState(); private readonly ProjectorServer server; private ProjectionForm projection;
        private Label status; private ComboBox screens;
        public ControlForm()
        {
            Text="Sermon Assistant Companion";Width=600;Height=340;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;Font=new Font("Segoe UI",9f);
            server=new ProjectorServer(state,9077,null);server.StatusChanged+=UpdateStatus;
            BuildUi(); Load+=delegate{try{Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media"));}catch{}server.Start();OpenProjection();UpdateStatus();};FormClosing+=delegate{server.Stop();if(projection!=null)projection.Close();};
        }
        private void BuildUi()
        {
            Label title=new Label();title.Text="SERMON ASSISTANT — WINDOWS 7 PROJECTION COMPANION";title.Font=new Font("Segoe UI",14f,FontStyle.Bold);title.AutoSize=true;title.Left=18;title.Top=18;Controls.Add(title);
            Label info=new Label();info.Text="Connect this PC to the big screen by HDMI. Keep the Android phone and PC on the same LAN/Wi-Fi.";info.AutoSize=true;info.Left=20;info.Top=55;Controls.Add(info);
            status=new Label();status.Left=20;status.Top=88;status.Width=540;status.Height=45;status.Font=new Font("Segoe UI",10f,FontStyle.Bold);Controls.Add(status);
            Label sl=new Label();sl.Text="Projection display:";sl.Left=20;sl.Top=145;sl.AutoSize=true;Controls.Add(sl);
            screens=new ComboBox();screens.Left=135;screens.Top=140;screens.Width=300;screens.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(screens);RefreshScreens();screens.SelectedIndexChanged+=delegate{MoveProjection();};
            Button test=new Button();test.Text="Project test";test.Left=20;test.Top=190;test.Width=120;test.Height=38;test.Click+=delegate{state.Set("SCRIPTURE","John 3:16","For God so loved the world, that he gave his only begotten Son…");};Controls.Add(test);
            Button blank=new Button();blank.Text="Blank screen";blank.Left=150;blank.Top=190;blank.Width=120;blank.Height=38;blank.Click+=delegate{state.Clear();};Controls.Add(blank);
            Button refresh=new Button();refresh.Text="Refresh displays";refresh.Left=280;refresh.Top=190;refresh.Width=130;refresh.Height=38;refresh.Click+=delegate{RefreshScreens();MoveProjection();};Controls.Add(refresh);
            Label ports=new Label();ports.Text="LAN discovery UDP 9076 | Projection TCP 9077 | Optional media: put OFFERING.jpg, TESTIMONY.jpg, WORSHIP.jpg, etc. beside the EXE in the media folder.";ports.Left=20;ports.Top=245;ports.Width=540;ports.Height=50;Controls.Add(ports);
        }
        private void RefreshScreens(){int old=screens==null?-1:screens.SelectedIndex;if(screens==null)return;screens.Items.Clear();Screen[] all=Screen.AllScreens;for(int i=0;i<all.Length;i++)screens.Items.Add((i+1)+": "+all[i].DeviceName+(all[i].Primary?" (Primary)":""));int preferred=0;for(int i=0;i<all.Length;i++)if(!all[i].Primary){preferred=i;break;}screens.SelectedIndex=(old>=0&&old<all.Length)?old:preferred;}
        private void OpenProjection(){projection=new ProjectionForm(state);projection.Show();MoveProjection();}
        private void MoveProjection(){if(projection==null||screens.SelectedIndex<0)return;Screen[] all=Screen.AllScreens;if(screens.SelectedIndex<all.Length)projection.MoveToScreen(all[screens.SelectedIndex]);}
        private void UpdateStatus(){if(IsDisposed)return;try{if(InvokeRequired){BeginInvoke((MethodInvoker)UpdateStatus);return;}status.Text="Listening on TCP 9077 • Client: "+server.LastClient+"\r\nPC IPv4: "+LocalIps();status.ForeColor=server.Running?Color.DarkGreen:Color.DarkRed;}catch{}}
        private static string LocalIps(){List<string> ips=new List<string>();try{foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())if(ni.OperationalStatus==OperationalStatus.Up)foreach(UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)if(u.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(u.Address))ips.Add(u.Address.ToString());}catch{}return ips.Count==0?"unknown":string.Join(", ",ips.ToArray());}
    }

    public static class SelfTest
    {
        public static int Run(string log)
        {
            try {
                ParsedCommand p=WireProtocol.Parse("SCRIPTURE\tref=John%203%3A16\tversion=NIV\ttext=For%20God%20so%20loved");
                if(p.Command!="SCRIPTURE"||p.Get("ref")!="John 3:16"||p.Get("version")!="NIV"||p.Get("text")!="For God so loved")throw new Exception("protocol decode");
                ProjectionState s=new ProjectionState();s.SetSegment("OFFERING","OFFERING","Give");s.Set("CAPTION","","Giving now");if(s.Mode!="CAPTION"||s.BackgroundMode!="OFFERING")throw new Exception("layered state");s.Set("PRAYER","PRAYER POINT","Father, help us");if(s.Mode!="PRAYER"||s.Body.IndexOf("help")<0)throw new Exception("state update");
                if(!string.IsNullOrEmpty(log))File.WriteAllText(log,"SELF_TEST_OK",Encoding.UTF8);return 0;
            } catch(Exception ex){try{if(!string.IsNullOrEmpty(log))File.WriteAllText(log,"SELF_TEST_FAIL: "+ex.Message,Encoding.UTF8);}catch{}return 2;}
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string selfLog=Arg(args,"--self-test-log",null); if(Has(args,"--self-test")){Environment.Exit(SelfTest.Run(selfLog));return;}
            if(Has(args,"--headless")) { int port=IntArg(args,"--port",19077);string log=Arg(args,"--log","sermon-companion-headless.log");int exit=IntArg(args,"--exit-after",30);ProjectionState s=new ProjectionState();ProjectorServer srv=new ProjectorServer(s,port,log);srv.Start();DateTime end=DateTime.UtcNow.AddSeconds(exit);while(srv.Running&&DateTime.UtcNow<end)Thread.Sleep(100);srv.Stop();return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new ControlForm());
        }
        static bool Has(string[] a,string k){for(int i=0;i<a.Length;i++)if(string.Equals(a[i],k,StringComparison.OrdinalIgnoreCase))return true;return false;}
        static string Arg(string[] a,string k,string d){for(int i=0;i+1<a.Length;i++)if(string.Equals(a[i],k,StringComparison.OrdinalIgnoreCase))return a[i+1];return d;}
        static int IntArg(string[] a,string k,int d){int v;return int.TryParse(Arg(a,k,d.ToString()),out v)?v:d;}
    }
}
