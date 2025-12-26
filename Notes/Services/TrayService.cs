#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace Notes.Services;

public class TrayService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_HOTKEY = 0x0312;

    // Hotkey modifiers
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN = 0x0008;
    private const int MOD_NOREPEAT = 0x4000;

    // Hotkey ID
    private const int HOTKEY_ID = 1;
    private const int NIM_ADD = 0x00;
    private const int NIM_DELETE = 0x02;
    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;

    private const int MF_STRING = 0x00;
    private const int MF_SEPARATOR = 0x800;
    private const int TPM_RIGHTBUTTON = 0x02;
    private const int TPM_RETURNCMD = 0x100;

    private const int ID_SHOW = 1;
    private const int ID_EXIT = 2;

    private WinUIWindow? _window;
    private AppWindow? _appWindow;
    private IntPtr _hWnd;
    private IntPtr _hIcon;
    private NOTIFYICONDATA _nid;
    private bool _iconAdded;
    private NativeWindowProc? _wndProcDelegate;
    private IntPtr _oldWndProc;

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public bool IsExiting { get; private set; }

    public void Initialize(WinUIWindow window)
    {
        _window = window;
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // Get AppWindow for minimize/hide functionality
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Intercept close to minimize to tray
        _appWindow.Closing += (s, e) =>
        {
            if (!IsExiting)
            {
                e.Cancel = true;
                _appWindow.Hide();
            }
        };

        // Subclass the window to handle tray messages
        _wndProcDelegate = new NativeWindowProc(WndProc);
        _oldWndProc = SetWindowLongPtr(_hWnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // Load icon
        _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION - default app icon

        // Try to load the app's icon
        var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
        if (File.Exists(iconPath))
        {
            var loadedIcon = LoadImage(IntPtr.Zero, iconPath, 1, 16, 16, 0x10);
            if (loadedIcon != IntPtr.Zero)
            {
                _hIcon = loadedIcon;
            }
        }

        // Create tray icon
        _nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Notes"
        };

        _iconAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);

        // Register global hotkey: Win+Shift+N
        RegisterHotKey(_hWnd, HOTKEY_ID, MOD_WIN | MOD_SHIFT | MOD_NOREPEAT, 0x4E); // 0x4E = 'N'
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // Win+Shift+N pressed - show/focus the window
            ShowWindow();
            return IntPtr.Zero;
        }

        if (msg == WM_TRAYICON)
        {
            int eventType = lParam.ToInt32() & 0xFFFF;

            if (eventType == WM_LBUTTONDBLCLK)
            {
                ShowWindow();
            }
            else if (eventType == WM_RBUTTONUP)
            {
                ShowContextMenu();
            }

            return IntPtr.Zero;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        // Get cursor position
        GetCursorPos(out POINT pt);

        // Create popup menu
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, MF_STRING, ID_SHOW, "Show Notes");
        AppendMenu(hMenu, MF_SEPARATOR, 0, null);
        AppendMenu(hMenu, MF_STRING, ID_EXIT, "Exit");

        // Need to set foreground window for menu to work properly
        SetForegroundWindow(_hWnd);

        // Show menu and get selection
        int cmd = TrackPopupMenu(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, 0, _hWnd, IntPtr.Zero);

        DestroyMenu(hMenu);

        if (cmd == ID_SHOW)
        {
            ShowWindow();
        }
        else if (cmd == ID_EXIT)
        {
            IsExiting = true;
            ExitRequested?.Invoke();
        }
    }

    public void ShowWindow()
    {
        _appWindow?.Show(true);
        SetForegroundWindow(_hWnd);
        ShowWindowRequested?.Invoke();
    }

    public void Dispose()
    {
        // Unregister global hotkey
        UnregisterHotKey(_hWnd, HOTKEY_ID);

        if (_iconAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _iconAdded = false;
        }
    }

    // P/Invoke declarations
    private delegate IntPtr NativeWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, int uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }
}
#endif
