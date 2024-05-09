using System.Diagnostics;
using System.Net;
using JetdriveSharp;
using JetdriveSharp.Utils;

namespace BigMission.WrlDynoCheck.ReplayCsv
{
    internal class Program
    {
        private static JetdriveProvider? provider;
        private static volatile bool exit = false;
        private static readonly Dictionary<int, ushort> channelLookup = [];

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: ReplayCsv <filename.csv>");
                return;
            }

            var csv = new CsvFile(args[0]);
            csv.Load();

            if (csv.Headers.Length == 0)
            {
                Console.WriteLine("No headers found in CSV file.");
                return;
            }
            var timeIndex = Array.IndexOf(csv.Headers, "Time");
            if (timeIndex < 0)
            {
                Console.WriteLine("Time column not found in CSV file.");
                return;
            }

            var (result, mcastIfaceAddr) = await NetworkUtils.TryGetBestLocalIFAddrAsync();
            if (!result)
            {
                //Most people will probably be running apps on the same machine, especially if they're not connected to the internet.
                Console.WriteLine("Automatic IP address selection failed, reverting to loopback.");
                mcastIfaceAddr = IPAddress.Loopback;
            }

            Console.WriteLine($"Using IP address: {mcastIfaceAddr}");

            //Create our interface to the local network.
            using NetworkPort netPort = new();

            //Join our multicast group!
            netPort.Join(mcastIfaceAddr!);

            //Start listening for incoming traffic.
            netPort.StartListening();


            using (provider = new JetdriveProvider(true, netPort, $"Replay CSV Provider", new HighAccuracyTimer()))
            {
                //The first thing we need to do is discover other hosts and ensure that we randomly generate a host id that doesn't already exist.
                provider.NegotiateHostId();

                //Listen for stall events so we can react accordingly.
                provider.Stalled += Client1_Stalled;

                //Subscribe for incoming channels (that's what we're here for!)
                provider.ChannelPosted += Client1_ChannelPosted;

                //For logging and UI updates, you want to subscribe to this event - show the user that the provider channel list has been cleared or zero out gauges, etc.
                provider.ChannelsCleared += Provider_ChannelsCleared;

                //We're just starting up, so we should ask for channel info from all hosts... 
                provider.RequestChannelInfo(JetdriveNode.ALL_HOSTS);

                InitializeChannels(csv, provider);

                float lastTime = 0f;
                var stopwatch = Stopwatch.StartNew();
                foreach (var row in csv.Rows)
                {
                    if (float.TryParse(row[timeIndex], out float time))
                    {
                        var timeDiff = time - lastTime - stopwatch.Elapsed.TotalSeconds;
                        if (timeDiff < 0)
                        {
                            timeDiff = 0;
                        }
                        var sp = TimeSpan.FromSeconds(timeDiff);
                        await Task.Delay(sp);
                        lastTime = time;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid time value: {row[timeIndex]}");
                        continue;
                    }

                    stopwatch = Stopwatch.StartNew();

                    for (int i = 0; i < row.Length; i++)
                    {
                        if (float.TryParse(row[i], out float val))
                        {
                            provider.QueueSample(channelLookup[i], val, DateTime.Now);
                        }
                        Console.Write($"{row[i]},");
                    }
                    Console.WriteLine();

                    bool transmitAgain = false;
                    do
                    {
                        (_, transmitAgain) = provider.TransmitChannelValues();
                    } while (transmitAgain);

                    stopwatch.Stop();
                }
            }
        }

        private static void InitializeChannels(CsvFile data, JetdriveProvider provider)
        {
            provider.ClearChannels();

            foreach (var col in data.Headers)
            {
                JDChannelInfo info = new(col, JDUnit.NoUnit);
                var id = provider.RegisterChannel(info);
                channelLookup[channelLookup.Count] = id;
            }

            provider.TransmitChannelInfo();
        }

        private static void Provider_ChannelsCleared(object sender, ClearChannelsEventArgs e)
        {
            Console.WriteLine($"ClearChannels: {e.HostId:X4} (\"{e.HostName}\")");
        }

        private static void Client1_ChannelPosted(object sender, ChannelValuePostedEventArgs e)
        {
            if (e.ChannelInfo != null) // If channel info is null, the provider hasn't transmitted channel info yet.
            {
                Console.WriteLine($"{e.ProviderName}.{e.ChannelInfo.channelName}\t=\t{e.Value:F4} {e.ChannelInfo.unit}\t @ {e.Timestamp:HH:mm:ss:fff}\tFlags={e.Message.Flags}");
            }
            else
            {
                Console.WriteLine($"?? {e.Value} \t @ {e.Timestamp.ToShortTimeString()}");
            }
        }

        private static void Client1_Stalled(object? sender, EventArgs e)
        {
            if (sender is JetdriveNode node)
            {
                Console.WriteLine($"Stall: 0x{node.HostId:X4} {node.StallStatus}");

                //If we've collided with another host, renegotiate our host id.
                if (node.StallStatus == StallReason.AddressCollisionDetected)
                {
                    node.NegotiateHostId();
                }
                else if (node.StallStatus == StallReason.InvalidVersionDetected)
                {
                    Console.WriteLine("Error: Cannot communicate on this JETDRIVE network, at least one other node exists with an incompatible version.");
                }
            }
        }
    }

}
