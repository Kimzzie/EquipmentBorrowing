using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
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

        // Scoped: one DbContext per operation (see the ViewModels' scope usage).
        services.AddDbContext<EquipmentBorrowingDbContext>(options =>
            options.UseSqlite(DatabasePathProvider.GetConnectionString()));

        services.AddScoped<IStudentRepository, EfStudentRepository>();
        services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
        services.AddScoped<IBorrowingRepository, EfBorrowingRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<BorrowEquipmentService>();
        services.AddScoped<ReturnEquipmentService>();

        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        using (var startupScope = provider.CreateScope())
        {
            var context = startupScope.ServiceProvider.GetRequiredService<EquipmentBorrowingDbContext>();
            context.Database.Migrate();
            DbSeeder.SeedAsync(context).GetAwaiter().GetResult();
        }

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