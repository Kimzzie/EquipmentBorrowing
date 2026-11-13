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

        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure();

        // Application services: scoped, so each one shares the DbContext of its scope.
        services.AddScoped<CheckoutProcessor>();
        services.AddScoped<BorrowEquipmentService>();
        services.AddScoped<CheckOutMultipleEquipmentService>();
        services.AddScoped<ReturnEquipmentService>();
        services.AddScoped<FindStudentsService>();
        services.AddScoped<RegisterStudentService>();
        services.AddScoped<UpdateStudentService>();
        services.AddScoped<ChangeStudentEligibilityService>();
        services.AddScoped<FindEquipmentService>();
        services.AddScoped<RegisterEquipmentService>();
        services.AddScoped<UpdateEquipmentService>();
        services.AddScoped<ViewStudentHistoryService>();
        services.AddScoped<ViewEquipmentHistoryService>();
        services.AddScoped<SearchBorrowingsService>();

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