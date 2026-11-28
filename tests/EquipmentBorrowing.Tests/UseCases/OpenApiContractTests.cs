using System.Net;
using System.Text.Json;
using EquipmentBorrowing.Tests.Infrastructure;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// OpenAPI consistency (Lab 6 R2 "OpenAPI consistency", supports MA6-02 and Lab 5 T01 step 4).
/// The machine-readable description served by the real API must describe API-01 to API-16: the right
/// methods, routes and published status codes, and nothing extra. Expected values are copied from
/// Section 2 of the Lab 5 reference, NOT generated from the code under test.
/// </summary>
[Collection("Api")]
public sealed class OpenApiContractTests : ApiTestBase
{
    public OpenApiContractTests(ApiFactory factory) : base(factory) { }

    // API id, method, route, status codes the Lab 5 contract publishes for it.
    private static readonly (string Api, string Method, string Route, string[] Codes)[] Contract =
    {
        ("API-01", "get",   "/api/v1/equipment",                       new[] { "200", "400" }),
        ("API-02", "get",   "/api/v1/students",                        new[] { "200", "400" }),
        ("API-03", "get",   "/api/v1/students/{id}",                   new[] { "200", "404" }),
        ("API-04", "post",  "/api/v1/students",                        new[] { "201", "400", "409" }),
        ("API-05", "put",   "/api/v1/students/{id}",                   new[] { "200", "400", "404", "409" }),
        ("API-06", "patch", "/api/v1/students/{id}/eligibility",       new[] { "200", "400", "404" }),
        ("API-07", "get",   "/api/v1/equipment/{id}",                  new[] { "200", "404" }),
        ("API-08", "post",  "/api/v1/equipment",                       new[] { "201", "400", "409" }),
        ("API-09", "put",   "/api/v1/equipment/{id}",                  new[] { "200", "400", "404", "409" }),
        ("API-10", "post",  "/api/v1/borrowings",                      new[] { "201", "400", "404", "409" }),
        ("API-11", "post",  "/api/v1/checkout-requests",               new[] { "201", "400", "404", "409" }),
        ("API-12", "post",  "/api/v1/borrowings/{id}/return",          new[] { "200", "404", "409" }),
        ("API-13", "get",   "/api/v1/borrowings/{id}",                 new[] { "200", "404" }),
        ("API-14", "get",   "/api/v1/borrowings",                      new[] { "200", "400" }),
        ("API-15", "get",   "/api/v1/students/{id}/borrowings",        new[] { "200", "404" }),
        ("API-16", "get",   "/api/v1/equipment/{id}/borrowings",       new[] { "200", "404" })
    };

    private static readonly string[] HttpMethods = { "get", "post", "put", "patch", "delete", "head", "options" };

    private async Task<JsonElement> LoadAsync()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);                  // the document is retrievable
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType ?? "");

        // JsonDocument is disposable; cloning detaches the element so it can be returned safely.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact, Trait("TC6", "MA6-02")]
    public async Task OpenApi_describes_exactly_the_sixteen_published_operations()
    {
        var root = await LoadAsync();

        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());

        var described = new SortedSet<string>();
        foreach (var path in root.GetProperty("paths").EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject().Where(o => HttpMethods.Contains(o.Name)))
                described.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");

        var expected = Contract.Select(c => $"{c.Method.ToUpperInvariant()} {c.Route}").ToHashSet();

        Assert.Equal(16, expected.Count);
        Assert.Equal(expected.OrderBy(x => x), described);          // all 16 present, none extra
    }

    [Fact, Trait("TC6", "MA6-02")]
    public async Task OpenApi_publishes_the_documented_status_codes_for_every_operation()
    {
        var root = await LoadAsync();

        foreach (var (api, method, route, codes) in Contract)
        {
            var operation = root.GetProperty("paths").GetProperty(route).GetProperty(method);
            var published = operation.GetProperty("responses").EnumerateObject().Select(r => r.Name).ToHashSet();

            foreach (var code in codes)
                Assert.True(published.Contains(code), $"{api} ({method.ToUpperInvariant()} {route}) does not document {code}.");
        }
    }

    [Fact, Trait("TC6", "MA6-02")]
    public async Task OpenApi_describes_request_bodies_for_writes_and_response_schemas_for_successes()
    {
        var root = await LoadAsync();

        foreach (var (api, method, route, codes) in Contract)
        {
            var operation = root.GetProperty("paths").GetProperty(route).GetProperty(method);

            // Writes that take a JSON body must describe it (the return endpoint takes an empty object).
            if (method is "post" or "put" or "patch" && !route.EndsWith("/return"))
                Assert.True(operation.TryGetProperty("requestBody", out _), $"{api} has no requestBody.");

            // Every success response must describe its JSON content.
            var success = codes.First(c => c is "200" or "201");
            var content = operation.GetProperty("responses").GetProperty(success);
            Assert.True(content.TryGetProperty("content", out _), $"{api} {success} has no response content.");
        }
    }
}
