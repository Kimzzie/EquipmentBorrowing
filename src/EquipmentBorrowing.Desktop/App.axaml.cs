using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    // The API location is configuration, not code: set EQUIPMENT_BORROWING_API_URL to point elsewhere.
    private const string ApiUrlVariable = "EQUIPMENT_BORROWING_API_URL";
    private const string DefaultApiUrl = "http://localhost:5080/";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiUrlVariable);
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultApiUrl;
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/"; // relative request paths only resolve correctly against a trailing slash

        var services = new ServiceCollection();

        // One HttpClient for the app's lifetime (creating one per call exhausts sockets).
        services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        });
        services.AddSingleton<IEquipmentApi, EquipmentApiClient>();

        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<EquipmentManagementViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<BorrowingSearchViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            _ = mainWindowViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
