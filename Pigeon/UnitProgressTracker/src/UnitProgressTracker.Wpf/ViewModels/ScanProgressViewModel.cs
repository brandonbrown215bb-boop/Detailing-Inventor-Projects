using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UnitProgressTracker.Wpf.ViewModels;

public class ScanProgressViewModel : INotifyPropertyChanged
{
    private bool _isScanRunning;
    private double _scanProgress;
    private string _statusText = "Ready";
    private CancellationTokenSource? _cts;

    public bool IsScanRunning
    {
        get => _isScanRunning;
        set { _isScanRunning = value; OnPropertyChanged(); }
    }

    public double ScanProgress
    {
        get => _scanProgress;
        set { _scanProgress = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public CancellationTokenSource? CurrentCts => _cts;

    public CancellationToken StartNewScan()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsScanRunning = true;
        ScanProgress = 0;
        StatusText = "Scanning geometry...";
        return _cts.Token;
    }

    public void ReportProgress(double percent, string message)
    {
        ScanProgress = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusText = message;
        }
    }

    public void CompleteScan(int surfaceCount)
    {
        IsScanRunning = false;
        ScanProgress = 100;
        StatusText = $"Scan complete. Discovered {surfaceCount} surface(s).";
    }

    public void CancelScan()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            StatusText = "Cancelling scan...";
        }
    }

    public void FailScan(string errorMessage)
    {
        IsScanRunning = false;
        StatusText = $"Scan failed: {errorMessage}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
