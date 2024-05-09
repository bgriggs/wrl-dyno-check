using BigMission.WrlDynoCheck.Models;
using CommunityToolkit.Mvvm.Messaging;
using JetdriveSharp;
using JetdriveSharp.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BigMission.WrlDynoCheck.Services;

internal class DynojetComm : BackgroundService
{
    private ILogger Logger { get; }
    private JetdriveProvider? provider;
    private readonly ISettingsProvider settings;
    private readonly string rpmChannelName = "(DWRT CPU) Engine RPM";
    private readonly string hpChannelName = "(DWRT CPU) Power";

    public DynojetComm(ILoggerFactory loggerFactory, ISettingsProvider settings)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        this.settings = settings;

        rpmChannelName = settings.GetAppSetting("Dynojet:RpmChannel") ?? rpmChannelName;
        hpChannelName = settings.GetAppSetting("Dynojet:HpChannel") ?? hpChannelName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var overrideIpStr = settings.GetAppSetting("Dynojet:OverrideIp");

        if (!IPAddress.TryParse(overrideIpStr, out var bindAddress))
        {
            var (result, mcastIfaceAddr) = await NetworkUtils.TryGetBestLocalIFAddrAsync();
            if (!result)
            {
                //Most people will probably be running apps on the same machine, especially if they're not connected to the internet.
                Logger.LogWarning("Automatic IP address selection failed, reverting to loopback.");
                bindAddress = IPAddress.Loopback;
            }
            else
            {
                bindAddress = mcastIfaceAddr;
            }
        }

        Logger.LogInformation($"Using IP address: {bindAddress}");

        using NetworkPort netPort = new();
        netPort.Join(bindAddress!);

        // Start listening for incoming traffic.
        netPort.StartListening();
        var providerName = settings.GetAppSetting("Dynojet:ProviderName");
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "WRL Flat Curve Check";
        }

        using (provider = new JetdriveProvider(true, netPort, providerName, new HighAccuracyTimer()))
        {
            // The first thing we need to do is discover other hosts and ensure that we randomly generate a host id that doesn't already exist.
            provider.NegotiateHostId();

            // Listen for stall events so we can react accordingly.
            provider.Stalled += Client_Stalled;

            // Subscribe for incoming channels
            provider.ChannelPosted += Client_ChannelPosted;

            // For logging and UI updates, you want to subscribe to this event - show the user that the provider channel list has been cleared or zero out gauges, etc.
            provider.ChannelsCleared += Provider_ChannelsCleared;

            // We're just starting up, so we should ask for channel info from all hosts... 
            provider.RequestChannelInfo(JetdriveNode.ALL_HOSTS);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Do nothing, we're just waiting for the user to close the application.
                await Task.Delay(50, stoppingToken);
            }
        }
    }

    private void Client_Stalled(object? sender, EventArgs e)
    {
        if (sender is JetdriveNode node)
        {
            Logger.LogWarning($"Stall: 0x{node.HostId:X4} {node.StallStatus}");

            //If we've collided with another host, renegotiate our host id.
            if (node.StallStatus == StallReason.AddressCollisionDetected)
            {
                node.NegotiateHostId();
            }
            else if (node.StallStatus == StallReason.InvalidVersionDetected)
            {
                Logger.LogError("Error: Cannot communicate on this JETDRIVE network, at least one other node exists with an incompatible version.");
            }
        }
    }

    private void Client_ChannelPosted(object sender, ChannelValuePostedEventArgs e)
    {
        if (e.ChannelInfo != null)
        {
            Logger.LogTrace($"{e.ProviderName}.{e.ChannelInfo.channelName}={e.Value:F4} {e.ChannelInfo.unit} @ {e.Timestamp:HH:mm:ss:fff} Flags={e.Message.Flags}");

            var chName = e.ChannelInfo.channelName.Trim();

            // RPM
            if (string.Compare(chName, rpmChannelName, true) == 0)
            {
                var cv = new ChannelValue(ChannelType.RPM, e.Value, e.Timestamp);
                WeakReferenceMessenger.Default.Send(cv);
            }
            // Power
            else if (string.Compare(chName, hpChannelName, true) == 0)
            {
                var cv = new ChannelValue(ChannelType.Power, e.Value, e.Timestamp);
                WeakReferenceMessenger.Default.Send(cv);
            }
        }
        else // Provider hasn't transmitted channel info yet.
        {
            Logger.LogTrace($"?? {e.Value} @ {e.Timestamp.ToShortTimeString()}");
        }
    }

    private void Provider_ChannelsCleared(object sender, ClearChannelsEventArgs e)
    {
        Logger.LogInformation($"ClearChannels: {e.HostId:X4} (\"{e.HostName}\")");
    }
}
