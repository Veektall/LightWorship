using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NewAgeWorship.Desktop;

public static class CaptureFunctionalV4
{
    [STAThread]
    public static void Main()
    {
        Directory.CreateDirectory("functional-capture");
        var app=new Application();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var w=new MainWindow();
        w.Width=1440;w.Height=900;w.Left=8;w.Top=8;w.Show();
        Pump(1200); Shot(w,"functional-capture/01-ready.png");

        var command=(TextBox)w.FindName("CommandBox");
        command.Text="John 3:16";
        Invoke(w,"LookupBible_Click");
        Pump(500); Shot(w,"functional-capture/02-bible-lookup.png");

        Invoke(w,"ParseScripture_Click");
        Pump(700); Shot(w,"functional-capture/03-bible-preview.png");

        var prompt=(TextBox)w.FindName("AiPromptBox");
        prompt.Text="Give one concise operator note: John 3:16 is prepared in Preview and still needs human approval.";
        Invoke(w,"AiAssist_Click");
        PumpUntil(()=>((Button)w.FindName("AiAssistButton")).IsEnabled,60000);
        Shot(w,"functional-capture/04-local-ai.png");

        var audioMethod=typeof(MainWindow).GetMethod("ProcessAudioFileAsync",BindingFlags.NonPublic|BindingFlags.Instance);
        if(audioMethod==null)throw new Exception("ProcessAudioFileAsync missing");
        var task=(Task)audioMethod.Invoke(w,new object[]{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"john-three-sixteen.wav")});
        PumpUntil(()=>task.IsCompleted,60000);
        if(task.IsFaulted)throw task.Exception;
        var scroll=(ScrollViewer)w.FindName("AssistScroll");
        scroll.ScrollToEnd();
        Pump(700); Shot(w,"functional-capture/05-audio-voice.png");

        w.Close();app.Shutdown();
    }

    static void Invoke(MainWindow w,string name)
    {
        var m=typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance);
        if(m==null)throw new Exception("missing "+name);
        m.Invoke(w,new object[]{w,new RoutedEventArgs()});
    }
    static void Pump(int ms)
    {
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(ms)};
        timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    static void PumpUntil(Func<bool> done,int timeoutMs)
    {
        var start=DateTime.UtcNow;
        while(!done())
        {
            if((DateTime.UtcNow-start).TotalMilliseconds>timeoutMs)throw new TimeoutException("capture action timed out");
            Pump(100);
        }
    }
    static void Shot(Window w,string path)
    {
        w.UpdateLayout();
        int width=(int)Math.Ceiling(w.ActualWidth),height=(int)Math.Ceiling(w.ActualHeight);
        var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(w);
        var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));
        using(var fs=File.Create(path))enc.Save(fs);
    }
}
