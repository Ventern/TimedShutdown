using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.Notifications;
using System.Diagnostics;

namespace TimedShutdown;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private int _shutdownMinutes = 5;
    public static bool IsLinux = false;
    private bool _3MinWarn = true;
    private string _statusMessage = "No shutdown scheduled.";
    private DispatcherTimer? _countdownTimer;
    private int _remainingSeconds;

    public int ShutdownMinutes
    {
        get => _shutdownMinutes;
        set
        {
            if (_shutdownMinutes == value)
                return;

            _shutdownMinutes = value;
            OnPropertyChanged();
        }
    }

    public bool MinuteWarn
    {
        get => _3MinWarn;
        set
        {
            if (_3MinWarn == value)
                return;

            _3MinWarn = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsWindows())
        {
            IsLinux = false;
        }
        else if (OperatingSystem.IsLinux())
        {
            IsLinux = true;
        }
        DataContext = this;
    }

    private void ScheduleShutdown()
    {
        if (!IsLinux)
        {
            int seconds = _shutdownMinutes * 60;
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = $"/s /t {seconds}",
                UseShellExecute = false
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = $"-h +{_shutdownMinutes}",
                UseShellExecute = false
            });
        }
    }
    
    private void CancelShutdown()
    {
        if (!IsLinux)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/a",
                UseShellExecute = false
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "-c",
                UseShellExecute = false
            });
        }
    }

    private void ScheduleShutdown_Click(object? sender, RoutedEventArgs e)
    {
        int minutes = ShutdownMinutes;

        _remainingSeconds = minutes * 60;
        StatusMessage = $"Shutdown scheduled in {minutes}:00.";
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();

        ScheduleShutdown();
    }

    private void CancelShutdown_Click(object? sender, RoutedEventArgs e)
    {
        StatusMessage = "Shutdown cancelled.";
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
        }
        CancelShutdown();
    }
    
    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;

        int minutes = _remainingSeconds / 60;
        int seconds = _remainingSeconds % 60;

        StatusMessage = $"Shutdown scheduled in {minutes}:{seconds:D2}";
        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            StatusMessage = "Shutting down...";
        }
        if (_3MinWarn && _remainingSeconds == 180)
        {
            NotificationService.Show("Timed Shutdown", "Your computer will shutdown automatically in 3 minutes unless canceled via terminal/app");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public static class NotificationService
    {
        public static void Show(string title, string message)
        {
            if (!MainWindow.IsLinux)
            {
                ShowWindows(title, message);
            }
            else
            {
                ShowLinux(title, message);
            }
        }

        private static void ShowWindows(string title, string message)
        {
            string escapedTitle = title.Replace("'", "''");
            string escapedMessage = message.Replace("'", "''");

            string script = $"""
                             [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
                             [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]

                             $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                             $xml.LoadXml("<toast><visual><binding template='ToastGeneric'><text>{escapedTitle}</text><text>{escapedMessage}</text></binding></visual></toast>")

                             $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
                             [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("TimedShutdown").Show($toast)
                             """;

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static void ShowLinux(string title, string message)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments =
                    $"--app-name=\"Timed Shutdown\" " +
                    $"--hint=string:sound-name:message-new-instant " +
                    $"\"{title}\" \"{message}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
    }
}