using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Diagnostics;
using System.IO;

namespace TimedShutdown;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private int _shutdownMinutes = 5;
    private static bool _isLinux = false;
    private static bool _3MinWarn = true;
    private string _statusMessage = "No shutdown scheduled.";
    private DispatcherTimer? _countdownTimer;
    private int _remainingSeconds;
    private readonly string _settingsFile =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TimedShutdown",
            "lastUsedMinutes.txt");

    public int ShutdownMinutes
    {
        get => _shutdownMinutes;
        set
        {
            if (_shutdownMinutes == value)
                return;

            _shutdownMinutes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedShutdownTime));
            SaveMinutes();
        }
    }

    public string FormattedShutdownTime
    {
        get
        {
            TimeSpan time = TimeSpan.FromMinutes(ShutdownMinutes);

            if (time.TotalDays >= 1)
            {
                return $"{time.Days} day{(time.Days == 1 ? "" : "s")}, " +
                       $"{time.Hours} hour{(time.Hours == 1 ? "" : "s")}, " +
                       $"{time.Minutes} minute{(time.Minutes == 1 ? "" : "s")}";
            }

            if (time.TotalHours >= 1)
            {
                return $"{time.Hours} hour{(time.Hours == 1 ? "" : "s")}, " +
                       $"{time.Minutes} minute{(time.Minutes == 1 ? "" : "s")}";
            }

            return $"{time.Minutes} minute{(time.Minutes == 1 ? "" : "s")}";
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
            _isLinux = false;
        }
        else if (OperatingSystem.IsLinux())
        {
            _isLinux = true;
        }
        LoadMinutes();
        DataContext = this;
    }

    private void ScheduleShutdown()
    {
        if (!_isLinux)
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
        if (!_isLinux)
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
        StatusMessage = $"Shutdown scheduled in {FormatRemainingTime(_remainingSeconds)}";
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
        StatusMessage = $"Shutdown scheduled in {FormatRemainingTime(_remainingSeconds)}";
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
    
    private string FormatRemainingTime(int totalSeconds)
    {
        if (totalSeconds <= 0)
            return "0 seconds";

        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);

        if (time.TotalDays >= 1)
        {
            if (time.Hours == 0 && time.Minutes == 0)
                return $"{time.Days} day{(time.Days == 1 ? "" : "s")}";

            if (time.Minutes == 0)
                return $"{time.Days} day{(time.Days == 1 ? "" : "s")} {time.Hours} hour{(time.Hours == 1 ? "" : "s")}";

            return $"{time.Days} day{(time.Days == 1 ? "" : "s")} {time.Hours} hour{(time.Hours == 1 ? "" : "s")} {time.Minutes} minute{(time.Minutes == 1 ? "" : "s")}";
        }

        if (time.TotalHours >= 1)
        {
            if (time.Minutes == 0)
                return $"{time.Hours} hour{(time.Hours == 1 ? "" : "s")}";

            return $"{time.Hours} hour{(time.Hours == 1 ? "" : "s")} {time.Minutes} minute{(time.Minutes == 1 ? "" : "s")}";
        }

        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}:{time.Seconds:D2}";

        return $"{time.Seconds} second{(time.Seconds == 1 ? "" : "s")}";
    }

    private void SaveMinutes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
        File.WriteAllText(_settingsFile, ShutdownMinutes.ToString());
    }

    private void LoadMinutes()
    {
        if (File.Exists(_settingsFile) &&
            int.TryParse(File.ReadAllText(_settingsFile), out int minutes))
        {
            _shutdownMinutes = minutes;
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
            if (!MainWindow._isLinux)
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