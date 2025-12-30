using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace Notes.WinUI;

public static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Notes", "startup.log");

    private static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"{DateTime.Now}: {message}\n");
        }
        catch { }
    }

    [STAThread]
    static int Main(string[] args)
    {
        Log($"Main started, PID={Environment.ProcessId}");
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("ComWrappers initialized");

            bool isRedirect = DecideRedirection();
            Log($"isRedirect={isRedirect}");

            if (!isRedirect)
            {
                Log("Starting application...");
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    Log("Inside Application.Start callback");
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    Log("Creating App instance");
                    new App();
                    Log("App instance created");
                });
                Log("Application.Start completed");
            }
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION: {ex}");
            throw;
        }

        Log($"Main exiting, PID={Environment.ProcessId}");
        return 0;
    }

    private static bool DecideRedirection()
    {
        bool isRedirect = false;
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("NotesApp");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
        }

        return isRedirect;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        IntPtr hWnd = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            var length = GetWindowTextLength(hwnd);
            if (length > 0)
            {
                var sb = new System.Text.StringBuilder(length + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                if (sb.ToString().StartsWith("Notes"))
                {
                    hWnd = hwnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        if (hWnd != IntPtr.Zero)
        {
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);

            // Flash the window to get user attention if SetForegroundWindow fails
            var flashInfo = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hWnd,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref flashInfo);
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int SW_RESTORE = 9;
    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
