using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure.Repositories;
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

        // Singletons: the lab uses in-memory storage, so the SAME instance
        // must be reused everywhere, or a new borrowing would vanish the
        // moment you switch views.
        services.AddSingleton<IStudentRepository, InMemoryStudentRepository>();
        services.AddSingleton<IEquipmentRepository, InMemoryEquipmentRepository>();
        services.AddSingleton<IBorrowingRepository, InMemoryBorrowingRepository>();

        // Transient: cheap to build, no shared state to protect.
        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();
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

            _ = mainWindowViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}