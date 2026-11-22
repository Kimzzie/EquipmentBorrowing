using EquipmentBorrowing.Application.Services;

namespace EquipmentBorrowing.Api.Support;

public static class ApplicationServices
{
    /// <summary>
    /// Registers the Lab 4 use-case services. Scoped: each one shares the DbContext of
    /// the current HTTP request, so one request = one unit of work.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CheckoutProcessor>();
        services.AddScoped<BorrowEquipmentService>();
        services.AddScoped<CheckOutMultipleEquipmentService>();
        services.AddScoped<ReturnEquipmentService>();
        services.AddScoped<ViewActiveBorrowingsService>();
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

        // New in Lab 5: single-record reads for API-03, API-07 and API-13.
        services.AddScoped<GetStudentService>();
        services.AddScoped<GetEquipmentService>();
        services.AddScoped<GetBorrowingService>();

        return services;
    }
}
