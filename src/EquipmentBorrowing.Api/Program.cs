using System.Text.Json.Serialization;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Api.Support;
using EquipmentBorrowing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Clock: real by default, frozen only when the test variable is set ----
TimeProvider timeProvider = TimeProvider.System;
var fixedDateText = builder.Configuration["EQUIPMENT_BORROWING_FIXED_DATE"];
if (!string.IsNullOrWhiteSpace(fixedDateText))
{
    if (!DateOnly.TryParseExact(fixedDateText, "yyyy-MM-dd", out var fixedDate))
        throw new InvalidOperationException("EQUIPMENT_BORROWING_FIXED_DATE must look like 2026-11-10.");

    timeProvider = new FixedDateTimeProvider(fixedDate);
}
builder.Services.AddSingleton(timeProvider);

// ---- Persistence + use cases (DbContext is scoped = one per HTTP request) ----
builder.Services.AddInfrastructure();
builder.Services.AddApplicationServices();

// ---- HTTP layer ----
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // PUT /equipment/{id} with "isAvailable" must be a 400 (T03), not silently ignored.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ApiResults.FromModelState;
    });

builder.Services.AddOpenApi();          // serves /openapi/v1.json
builder.Services.AddProblemDetails();   // required by UseExceptionHandler() below
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (timeProvider is FixedDateTimeProvider)
    app.Logger.LogWarning("TEST CLOCK ACTIVE: the API believes today is {Date}.", fixedDateText);

app.UseExceptionHandler();

// Unknown route -> 404, wrong verb -> 405 and non-JSON body -> 415 all arrive with an
// EMPTY body by default. This gives them our { code, message } body. (Responses that
// already have a body, such as a missing student, are left alone.)
app.UseStatusCodePages(async context =>
{
    var error = context.HttpContext.Response.StatusCode switch
    {
        404 => new ApiError(ApiErrorCodes.RouteNotFound, "No endpoint exists at this address."),
        405 => new ApiError(ApiErrorCodes.MethodNotAllowed, "This endpoint does not support that HTTP method."),
        415 => new ApiError(ApiErrorCodes.UnsupportedMediaType, "Send the request body as JSON with Content-Type: application/json."),
        _ => new ApiError("http_error", "The request could not be completed.")
    };

    await context.HttpContext.Response.WriteAsJsonAsync(error);
});

app.MapOpenApi();
app.MapControllers();

app.Run();
