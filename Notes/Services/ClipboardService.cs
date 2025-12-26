#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
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
            // Skip if our window is the foreground window (copy came from within the app)
            if (GetForegroundWindow() == _hWnd)
            {
                return;
            }

            if (OpenClipboard(_hWnd))
            {
                try
                {
                    // Check for image first
                    if (IsClipboardFormatAvailable(CF_DIB))
                    {
                        var imageData = GetClipboardImageAsBase64();
                        if (!string.IsNullOrEmpty(imageData) && imageData != _lastClipboardText)
                        {
                            _lastClipboardText = imageData;
                            AddImageToHistory(imageData);
                            return;
                        }
                    }

                    // Try to get text from clipboard
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

    private string? GetClipboardImageAsBase64()
    {
        try
        {
            IntPtr hDib = GetClipboardData(CF_DIB);
            if (hDib == IntPtr.Zero) return null;

            IntPtr pDib = GlobalLock(hDib);
            if (pDib == IntPtr.Zero) return null;

            try
            {
                // Get DIB size
                int size = (int)GlobalSize(hDib);
                byte[] dibData = new byte[size];
                Marshal.Copy(pDib, dibData, 0, size);

                // Convert DIB to BMP by adding file header
                using var bmpStream = new MemoryStream();
                using var bw = new BinaryWriter(bmpStream);

                // BMP file header (14 bytes)
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(14 + size); // File size
                bw.Write((short)0); // Reserved
                bw.Write((short)0); // Reserved

                // Calculate offset to pixel data (header + DIB header size)
                int dibHeaderSize = BitConverter.ToInt32(dibData, 0);
                int colorTableSize = 0;
                if (dibHeaderSize >= 40)
                {
                    int bitCount = BitConverter.ToInt16(dibData, 14);
                    if (bitCount <= 8)
                    {
                        int colorsUsed = BitConverter.ToInt32(dibData, 32);
                        colorTableSize = (colorsUsed == 0 ? (1 << bitCount) : colorsUsed) * 4;
                    }
                }
                bw.Write(14 + dibHeaderSize + colorTableSize); // Offset to pixel data

                // Write DIB data
                bw.Write(dibData);
                bw.Flush();

                // Convert BMP to PNG for better browser compatibility
                bmpStream.Position = 0;
                using var bitmap = new Bitmap(bmpStream);
                using var pngStream = new MemoryStream();
                bitmap.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(pngStream.ToArray());
            }
            finally
            {
                GlobalUnlock(hDib);
            }
        }
        catch
        {
            return null;
        }
    }

    private void AddImageToHistory(string base64Image)
    {
        // Check for duplicate (same image)
        var existing = _clipboardHistory.FirstOrDefault(x => x.IsImage && x.Content == base64Image);
        if (existing != null)
        {
            _clipboardHistory.Remove(existing);
            existing.CopiedAt = DateTime.Now;
            _clipboardHistory.Insert(0, existing);
        }
        else
        {
            var item = new ClipboardItem
            {
                Id = _nextId++,
                Content = base64Image,
                Preview = "Image",
                CopiedAt = DateTime.Now,
                IsImage = true
            };

            _clipboardHistory.Insert(0, item);

            while (_clipboardHistory.Count > MaxItems)
            {
                _clipboardHistory.RemoveAt(_clipboardHistory.Count - 1);
            }

            ShowClipboardNotification(item.Id, "Image copied to clipboard");
        }

        ClipboardChanged?.Invoke();
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
    private const uint CF_DIB = 8;
    private const uint CF_BITMAP = 2;

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
#endif
