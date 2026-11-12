using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // The app's clock. Singleton because it has no state; tests can swap in a fake.
        services.AddSingleton(TimeProvider.System);

        // DbContext (Npgsql), repositories, unit of work, database initializer.
        // The Desktop project no longer knows which database is behind them.
        services.AddInfrastructure();

        services.AddScoped<BorrowEquipmentService>();
        services.AddScoped<ReturnEquipmentService>();

        // ViewModels live for the whole app session, so they hold a scope factory
        // and open a fresh scope (= fresh DbContext) for every operation.
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            // The window appears immediately; the database check runs asynchronously
            // and reports its own success or failure (see MainWindowViewModel).
            _ = mainWindowViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}