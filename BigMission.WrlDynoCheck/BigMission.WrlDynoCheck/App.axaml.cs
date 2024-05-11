using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using BigMission.Avalonia.LogViewer.Extensions;
using BigMission.WrlDynoCheck.ViewModels;
using BigMission.WrlDynoCheck.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MsLogger.Core;
using System.Threading;
using CommunityToolkit.Extensions.DependencyInjection;
using LogViewer.Core.ViewModels;
using BigMission.WrlDynoCheck.Services;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace BigMission.WrlDynoCheck;

public partial class App : Application
{
    private IHost? _host;
    private CancellationTokenSource? _cancellationTokenSource;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddLogViewer().Logging.AddDefaultDataStoreLogger();
        
        var services = builder.Services;
        services.AddHostedService<DynojetComm>();
        ConfigureServices(services);
        ConfigureViewModels(services);
        ConfigureViews(services);

        services.AddSingleton(service => new MainWindow
        {
            DataContext = service.GetRequiredService<MainViewModel>()
        });
        //builder.Logging.SetMinimumLevel(LogLevel.Trace);
        _host = builder.Build();
        _cancellationTokenSource = new();

        // Dependency injection: https://github.com/stevemonaco/AvaloniaViewModelFirstDemos
        // NuGet source: https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json
        var locator = new ViewLocator(_host.Services);
        DataTemplates.Add(locator);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _host.Services.GetRequiredService<MainWindow>();
            desktop.ShutdownRequested += OnShutdownRequested;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            //singleViewPlatform.MainView = new MainView
            //{
            //    DataContext = new MainViewModel()
            //};
        }

        // Startup background services
        _ = _host.StartAsync(_cancellationTokenSource.Token);

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        => _ = _host!.StopAsync(_cancellationTokenSource!.Token);

    [Singleton(typeof(WindowsSettingsProvider), typeof(ISettingsProvider))]
    internal static partial void ConfigureServices(IServiceCollection services);

    [Singleton(typeof(MainViewModel))]
    [Singleton(typeof(LogViewerControlViewModel))]
    [Singleton(typeof(AboutViewModel))]
    internal static partial void ConfigureViewModels(IServiceCollection services);

    [Singleton(typeof(MainView))]
    [Singleton(typeof(About))]
    internal static partial void ConfigureViews(IServiceCollection services);
}
