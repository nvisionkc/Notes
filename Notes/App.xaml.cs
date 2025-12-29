using Notes.Services;
#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace Notes;

public partial class App : Application
{
    private Window? _mainWindow;
#if WINDOWS
    private ClipboardService? _clipboardService;
    private TrayService? _trayService;
#endif

    public App()
    {
        InitializeComponent();

#if WINDOWS
        // Register toast activation handler
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
#endif
    }

#if WINDOWS
    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Parse the arguments
        var args = ToastArguments.Parse(e.Argument);

        if (args.TryGetValue("action", out string? action) && action == "viewClipboard")
        {
            if (args.TryGetValue("clipboardId", out string? idStr) && int.TryParse(idStr, out int clipboardId))
            {
                // Dispatch to UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Show the window
                    _trayService?.ShowWindow();

                    // Notify clipboard service about the clicked item
                    _clipboardService?.RaiseNotificationClicked(clipboardId);
                });
            }
        }
    }
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _mainWindow = new Window(new MainPage()) { Title = "Notes (Build 2025.12.26.C)" };

#if WINDOWS
        _mainWindow.Created += (s, e) =>
        {
            _trayService = Handler?.MauiContext?.Services.GetService<TrayService>();
            var saveService = Handler?.MauiContext?.Services.GetService<SaveOnCloseService>();
            var windowSettings = Handler?.MauiContext?.Services.GetService<WindowSettingsService>();

            if (_trayService != null && _mainWindow.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                _trayService.Initialize(nativeWindow, windowSettings);

                // Initialize clipboard monitoring
                _clipboardService = Handler?.MauiContext?.Services.GetService<ClipboardService>();
                _clipboardService?.Initialize(nativeWindow);

                // Start minimized to tray
                _trayService.HideWindow();

                // When user clicks Exit in tray menu
                _trayService.ExitRequested += async () =>
                {
                    if (saveService != null)
                    {
                        await saveService.RequestSaveAsync();
                    }

                    _trayService.Dispose();
                    Application.Current?.Quit();
                };
            }
        };

        // Save when window loses focus (before hiding to tray)
        _mainWindow.Deactivated += async (s, e) =>
        {
            var saveService = Handler?.MauiContext?.Services.GetService<SaveOnCloseService>();
            if (saveService != null)
            {
                await saveService.RequestSaveAsync();
            }
        };
#endif

        return _mainWindow;
    }
}
