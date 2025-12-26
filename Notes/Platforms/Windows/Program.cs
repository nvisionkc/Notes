using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace Notes.WinUI;

public static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        bool isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

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
