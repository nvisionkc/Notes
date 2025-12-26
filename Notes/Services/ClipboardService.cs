#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace Notes.Services;

public class ClipboardItem
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime CopiedAt { get; set; } = DateTime.Now;
    public bool IsImage { get; set; }
}

public class ClipboardService : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int MaxItems = 20;

    private IntPtr _hWnd;
    private NativeWindowProc? _wndProcDelegate;
    private IntPtr _oldWndProc;
    private bool _isListening;
    private string _lastClipboardText = string.Empty;

    private readonly List<ClipboardItem> _clipboardHistory = new();
    private int _nextId = 1;

    public event Action? ClipboardChanged;

    public IReadOnlyList<ClipboardItem> History => _clipboardHistory.AsReadOnly();

    public void Initialize(WinUIWindow window)
    {
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // Subclass window to receive clipboard messages
        _wndProcDelegate = new NativeWindowProc(WndProc);
        _oldWndProc = SetWindowLongPtr(_hWnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // Register for clipboard notifications
        _isListening = AddClipboardFormatListener(_hWnd);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardUpdate();
            return IntPtr.Zero;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnClipboardUpdate()
    {
        try
        {
            // Try to get text from clipboard
            if (OpenClipboard(_hWnd))
            {
                try
                {
                    IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                    if (hData != IntPtr.Zero)
                    {
                        IntPtr pData = GlobalLock(hData);
                        if (pData != IntPtr.Zero)
                        {
                            try
                            {
                                string? text = Marshal.PtrToStringUni(pData);
                                if (!string.IsNullOrEmpty(text) && text != _lastClipboardText)
                                {
                                    _lastClipboardText = text;
                                    AddToHistory(text);
                                }
                            }
                            finally
                            {
                                GlobalUnlock(hData);
                            }
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private void AddToHistory(string text)
    {
        // Don't add duplicates (check if same content exists)
        var existing = _clipboardHistory.FirstOrDefault(x => x.Content == text);
        if (existing != null)
        {
            // Move to top
            _clipboardHistory.Remove(existing);
            existing.CopiedAt = DateTime.Now;
            _clipboardHistory.Insert(0, existing);
        }
        else
        {
            // Add new item
            var item = new ClipboardItem
            {
                Id = _nextId++,
                Content = text,
                Preview = text.Length > 100 ? text[..100] + "..." : text,
                CopiedAt = DateTime.Now
            };

            _clipboardHistory.Insert(0, item);

            // Keep only last N items
            while (_clipboardHistory.Count > MaxItems)
            {
                _clipboardHistory.RemoveAt(_clipboardHistory.Count - 1);
            }

            // Show toast notification for new items
            ShowClipboardNotification(item.Id, item.Preview);
        }

        ClipboardChanged?.Invoke();
    }

    private void ShowClipboardNotification(int itemId, string preview)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewClipboard")
                .AddArgument("clipboardId", itemId.ToString())
                .AddText("Clipboard Captured")
                .AddText(preview)
                .Show();
        }
        catch
        {
            // Ignore notification errors
        }
    }

    public ClipboardItem? GetItemById(int id)
    {
        return _clipboardHistory.FirstOrDefault(x => x.Id == id);
    }

    public event Action<int>? NotificationClicked;

    public void RaiseNotificationClicked(int clipboardId)
    {
        NotificationClicked?.Invoke(clipboardId);
    }

    public void CopyToClipboard(string text)
    {
        // Temporarily stop listening to avoid re-adding
        _lastClipboardText = text;

        if (OpenClipboard(_hWnd))
        {
            try
            {
                EmptyClipboard();

                IntPtr hGlobal = Marshal.StringToHGlobalUni(text);
                SetClipboardData(CF_UNICODETEXT, hGlobal);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    public void RemoveFromHistory(int id)
    {
        var item = _clipboardHistory.FirstOrDefault(x => x.Id == id);
        if (item != null)
        {
            _clipboardHistory.Remove(item);
            ClipboardChanged?.Invoke();
        }
    }

    public void ClearHistory()
    {
        _clipboardHistory.Clear();
        ClipboardChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_isListening)
        {
            RemoveClipboardFormatListener(_hWnd);
            _isListening = false;
        }
    }

    // P/Invoke
    private const uint CF_UNICODETEXT = 13;

    private delegate IntPtr NativeWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
#endif
