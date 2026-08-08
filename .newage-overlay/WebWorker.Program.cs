using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace NewAgeWorship.WebWorker
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var url = Arg(args, "--url"); var hosts = (Arg(args, "--allow-host") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            var cache = Arg(args, "--cache") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEWAGE WORSHIP", "web-cache", "fallback.html");
            var screenIndex = IntArg(args, "--screen", 0);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return 2;
            if (hosts.Length == 0 || !hosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase))) return 3;
            Directory.CreateDirectory(Path.GetDirectoryName(cache));
            try { using (var wc = new WebClient()) { wc.Headers[HttpRequestHeader.UserAgent] = "NEWAGE-WORSHIP/1.0"; File.WriteAllText(cache, wc.DownloadString(uri)); } } catch { }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SafeBrowserForm(uri, new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase), cache, screenIndex)); return 0;
        }
        private static string Arg(string[] a,string n){for(int i=0;i+1<a.Length;i++)if(string.Equals(a[i],n,StringComparison.OrdinalIgnoreCase))return a[i+1];return null;}
        private static int IntArg(string[] a,string n,int d){int x;return int.TryParse(Arg(a,n),out x)?x:d;}
    }

    internal sealed class SafeBrowserForm : Form
    {
        private readonly WebBrowser _browser = new WebBrowser(); private readonly HashSet<string> _hosts; private readonly string _cache; private bool _loaded;
        public SafeBrowserForm(Uri uri, HashSet<string> hosts, string cache, int screenIndex)
        {
            _hosts=hosts;_cache=cache;FormBorderStyle=FormBorderStyle.None;BackColor=Color.Black;TopMost=true;
            var screens=Screen.AllScreens;var s=screens[Math.Max(0,Math.Min(screenIndex,screens.Length-1))];StartPosition=FormStartPosition.Manual;Bounds=s.Bounds;
            _browser.Dock=DockStyle.Fill;_browser.ScriptErrorsSuppressed=true;_browser.AllowNavigation=true;_browser.IsWebBrowserContextMenuEnabled=false;_browser.WebBrowserShortcutsEnabled=false;
            _browser.Navigating+=Navigating;_browser.DocumentCompleted+=(a,b)=>_loaded=true;Controls.Add(_browser);
            Shown+=(a,b)=>{try{_browser.Navigate(uri);}catch{Fallback();}};
            var timer=new Timer{Interval=12000};timer.Tick+=(a,b)=>{timer.Stop();if(!_loaded)Fallback();};timer.Start();
        }
        private void Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if(e.Url==null)return;if(e.Url.Scheme=="about"||e.Url.Scheme=="file")return;if(!_hosts.Contains(e.Url.Host)){e.Cancel=true;}
        }
        private void Fallback()
        {
            try{if(File.Exists(_cache))_browser.Navigate(new Uri(_cache));else _browser.DocumentText="<html><body style='margin:0;background:#10182a;color:white;font:32px Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh'>Online source unavailable</body></html>";}catch{}
        }
    }
}
