using Avalonia.Threading;
using BigMission.WrlDynoCheck.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LogViewer.Core.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading;

namespace BigMission.WrlDynoCheck.ViewModels;

public partial class MainViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IRecipient<ChannelValue>
{
    private ILogger Logger { get; }
    public LogViewerControlViewModel LogViewer { get; }

    public ObservableCollection<DynoRunViewModel> Runs { get; } = new();
    private DynoRunViewModel? currentRun;
    private Timer? runTimeoutTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRun))]
    private DynoRunViewModel? selectedRun;

    public bool HasRun
    {
        get { return SelectedRun != null; }
    }

    /// <summary>
    /// Time to wait after last channel update to complete the run.
    /// </summary>
    private readonly TimeSpan runTimeout = TimeSpan.FromSeconds(3);


    public MainViewModel(ILoggerFactory loggerFactory, LogViewerControlViewModel logViewer)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        LogViewer = logViewer;

        var demoRun = new DynoRunViewModel
        {
            Name = "Demo Run"
        };
        Runs.Add(demoRun);
        SelectedRun = demoRun;
    }

    public async void Receive(ChannelValue message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => ProcessChannelValue(message));
    }

    private void ProcessChannelValue(ChannelValue message)
    {
        currentRun ??= new();

        if (message.ChannelType == ChannelType.RPM)
        {
            currentRun.Rpm.Add(message.Time, message);
        }
        else if (message.ChannelType == ChannelType.Power)
        {
            currentRun.Power.Add(message.Time, message);
        }

        RunTimeoutTimerReset();
    }

    private void RunTimeoutTimerReset()
    {
        if (runTimeoutTimer != null)
        {
            runTimeoutTimer.Change((int)runTimeout.TotalMilliseconds, Timeout.Infinite);
        }
        else
        {
            runTimeoutTimer = new(CompleteRun, null, (int)runTimeout.TotalMilliseconds, Timeout.Infinite);
        }
    }

    private async void CompleteRun(object? state)
    {
        try
        {
            runTimeoutTimer?.Dispose();
            runTimeoutTimer = null;
        }
        catch { }

        await Dispatcher.UIThread.InvokeAsync(() => 
        {
            Logger.LogInformation("Run completed");
            if (currentRun != null)
            {
                Runs.Add(currentRun);
                currentRun.Process();
                currentRun = null;
            }
            else
            {
                Logger.LogWarning("No run fount to complete");
            }
        });
    }
}
