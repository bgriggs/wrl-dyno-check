using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BigMission.WrlDynoCheck.ViewModels;

internal class AboutViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public void OpenUrl(object urlObj)
    {
        var url = urlObj as string;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //https://stackoverflow.com/a/2796367/241446
            using var proc = new Process { StartInfo = { UseShellExecute = true, FileName = url } };
            proc.Start();
        }
    }
}
