using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace LightWorship
{
    public static class ExternalScriptureInjector
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;

        public static ExternalScriptureInjectionResult Send(string reference, AppSettings settings)
        {
            if (String.IsNullOrWhiteSpace(reference))
            {
                return ExternalScriptureInjectionResult.Fail("No scripture reference to send.");
            }

            if (settings == null || !settings.ExternalScriptureIntegrationEnabled)
            {
                return ExternalScriptureInjectionResult.Fail("External scripture integration is disabled.");
            }

            var window = FindWindowByTitle(settings.ExternalTargetWindowTitle);
            if (window == IntPtr.Zero)
            {
                return ExternalScriptureInjectionResult.Fail("Target window not found: " + settings.ExternalTargetWindowTitle);
            }

            try
            {
                ShowWindow(window, SwRestore);
                SetForegroundWindow(window);
                Thread.Sleep(Math.Max(100, Math.Min(2000, settings.ExternalFocusDelayMs)));

                if (!String.IsNullOrWhiteSpace(settings.ExternalScriptureInputHotkey))
                {
                    SendKeys.SendWait(settings.ExternalScriptureInputHotkey);
                    Thread.Sleep(120);
                }

                System.Windows.Clipboard.SetText(reference);
                SendKeys.SendWait("^v");
                if (settings.ExternalSendEnterAfterReference)
                {
                    Thread.Sleep(80);
                    SendKeys.SendWait("{ENTER}");
                }

                return ExternalScriptureInjectionResult.Ok("Sent " + reference + " to " + settings.ExternalTargetWindowTitle);
            }
            catch (Exception ex)
            {
                return ExternalScriptureInjectionResult.Fail(ex.Message);
            }
        }

        private static IntPtr FindWindowByTitle(string titlePart)
        {
            if (String.IsNullOrWhiteSpace(titlePart))
            {
                return IntPtr.Zero;
            }

            var found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                var title = new System.Text.StringBuilder(512);
                GetWindowText(hWnd, title, title.Capacity);
                if (title.ToString().IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }
    }

    public class ExternalScriptureInjectionResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        private ExternalScriptureInjectionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ExternalScriptureInjectionResult Ok(string message)
        {
            return new ExternalScriptureInjectionResult(true, message);
        }

        public static ExternalScriptureInjectionResult Fail(string message)
        {
            return new ExternalScriptureInjectionResult(false, message);
        }
    }
}
