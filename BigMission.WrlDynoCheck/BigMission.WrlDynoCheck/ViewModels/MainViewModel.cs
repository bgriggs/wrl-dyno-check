using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BigMission.WrlDynoCheck.Models;
using BigMission.WrlDynoCheck.Services;
using BigMission.WrlDynoCheck.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DialogHostAvalonia;
using LogViewer.Core.ViewModels;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigMission.WrlDynoCheck.ViewModels;

public partial class MainViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IRecipient<ChannelValue>
{
    private ILogger Logger { get; }
    public LogViewerControlViewModel LogViewer { get; }

    public ObservableCollection<DynoRunViewModel> Runs { get; } = new();
    private DynoRunViewModel? currentRun;
    private Timer? runTimeoutTimer;
    private int messageCount;

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
    private readonly TimeSpan runTimeout = TimeSpan.FromSeconds(2);
    private readonly ISettingsProvider settings;

    public MainViewModel(ILoggerFactory loggerFactory, LogViewerControlViewModel logViewer, ISettingsProvider settings)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        LogViewer = logViewer;
        this.settings = settings;
        WeakReferenceMessenger.Default.RegisterAll(this);

        var demoRun = new DynoRunViewModel { Name = "Demo Run" };
        Runs.Add(demoRun);
        SelectedRun = demoRun;
    }

    public void Receive(ChannelValue message)
    {
        //await Dispatcher.UIThread.InvokeAsync(() => ProcessChannelValue(message));
        ProcessChannelValue(message);
    }

    private void ProcessChannelValue(ChannelValue message)
    {
        currentRun ??= new();
        messageCount++;

        if (message.ChannelType == ChannelType.RPM)
        {
            currentRun.Rpm.Add(message.Time, message);
        }
        else if (message.ChannelType == ChannelType.Power)
        {
            currentRun.Power.Add(message.Time, message);
        }

        if (messageCount % 10 == 0)
        {
            Logger.LogInformation($"Received {messageCount} channel updates.");
            RunTimeoutTimerReset();
        }
    }

    /// <summary>
    /// Reset the time each time a channel value is received. 
    /// When channel updates stop, the run will be completed after the timeout.
    /// </summary>
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
                SelectedRun = currentRun;
                currentRun.Process();
                currentRun = null;
            }
            else
            {
                Logger.LogWarning("No run fount to complete");
            }
        });
    }

    public async Task OpenFileAsync(object source)
    {
        var topLevel = TopLevel.GetTopLevel((Control)source);
        if (topLevel != null)
        {
            var dir = Directory.GetCurrentDirectory();
            var defaultPath = await topLevel.StorageProvider.TryGetFolderFromPathAsync(dir);
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false,
                SuggestedStartLocation = defaultPath,
            });

            if (files.Count > 0)
            {
                try
                {
                    await LoadCsv(files[0].Path.LocalPath, files[0].Name);
                }
                catch (Exception ex)
                {
                    var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
                    {
                        ButtonDefinitions = [new ButtonDefinition { Name = "OK", IsDefault = true }],
                        ContentTitle = "Load Error",
                        ContentMessage = "Failed to load selected file: " + ex.Message,
                        Icon = Icon.Error,
                        MaxWidth = 500,
                    });
                    await box.ShowAsync();
                }
            }
        }
    }

    public async Task LoadCsv(string file, string runName)
    {
        var csv = new CsvFile(file);
        await csv.Load();

        var rpmChannelName = settings.GetAppSetting("Dynojet:RpmChannel") ?? "(DWRT CPU) Engine RPM";
        var hpChannelName = settings.GetAppSetting("Dynojet:HpChannel") ?? "(DWRT CPU) Power";

        var rpmIndex = Array.FindIndex(csv.Headers, h => string.Compare(h.Trim(), rpmChannelName.Trim(), true) == 0);
        var powerIndex = Array.FindIndex(csv.Headers, h => string.Compare(h.Trim(), hpChannelName.Trim(), true) == 0);
        var timeIndex = Array.FindIndex(csv.Headers, h => string.Compare(h.Trim(), "Time", true) == 0);

        if (rpmIndex < 0 || powerIndex < 0 || timeIndex < 0)
        {
            Logger.LogError("RPM, Power, or Time channel not found in CSV file.");
            return;
        }

        var run = new DynoRunViewModel { Name = runName };

        foreach (var row in csv.Rows)
        {
            if (double.TryParse(row[timeIndex], out double timeOffset) && float.TryParse(row[rpmIndex], out float rpm) && float.TryParse(row[powerIndex], out float power))
            {
                var time = DateTime.Now.AddSeconds(timeOffset);
                run.Rpm.Add(time, new ChannelValue(ChannelType.RPM, rpm, time));
                run.Power.Add(time, new ChannelValue(ChannelType.Power, power, time));
            }
        }

        run.Process();

        Runs.Add(run);
        SelectedRun = run;
    }

    public async Task ShowAboutAsync()
    {
        var vm = new AboutViewModel();
        await DialogHost.Show(vm, "MainDialogHost");
    }
}
