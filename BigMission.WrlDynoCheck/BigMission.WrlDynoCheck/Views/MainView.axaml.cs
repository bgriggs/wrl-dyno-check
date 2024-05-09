using Avalonia.Controls;
using Avalonia.Interactivity;
using BigMission.WrlDynoCheck.ViewModels;

namespace BigMission.WrlDynoCheck.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is MainViewModel vm)
        {
            await vm.LoadCsv("DemoRuns\\134Power.csv", "Demo Run");
        }
    }
}
