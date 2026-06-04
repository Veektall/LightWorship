using System.Windows;
using System;
using System.IO;

namespace LightWorship
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (sender, e) =>
            {
                LogCrash(e.Exception);
                MessageBox.Show("LightWorship hit an unexpected error. A crash log was written under ProgramData\\LightWorship\\Logs.", "LightWorship", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                LogCrash(e.ExceptionObject as Exception);
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        private static void LogCrash(Exception exception)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LightWorship", "Logs");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                File.WriteAllText(path, exception == null ? "Unknown error" : exception.ToString());
            }
            catch
            {
            }
        }
    }
}
