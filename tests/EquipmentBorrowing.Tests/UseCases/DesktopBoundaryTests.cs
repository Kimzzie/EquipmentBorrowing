using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// MA6-02 support: SOURCE INSPECTION showing the desktop is an HTTP-only client (Lab 5 architecture).
/// "Source inspection" evidence, as the handout allows: these read the desktop project's files; they do not
/// run the UI. No database is touched, so there is no fixture and no collection.
/// </summary>
public sealed class DesktopBoundaryTests
{
    private static string DesktopDirectory()
    {
        // ...\tests\EquipmentBorrowing.Tests\bin\Debug\net10.0\  ->  repository root
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var root = tfm.Parent!.Parent!.Parent!.Parent!.Parent!;
        var dir = Path.Combine(root.FullName, "src", "EquipmentBorrowing.Desktop");
        return Directory.Exists(dir) ? dir : throw new DirectoryNotFoundException(dir);
    }

    private static IEnumerable<string> DesktopSourceFiles() =>
        Directory.EnumerateFiles(DesktopDirectory(), "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".axaml"))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact, Trait("TC6", "MA6-02")]
    public void Desktop_project_has_no_project_references_and_no_database_packages()
    {
        // Parsed as XML on purpose: a comment mentioning "ProjectReference" must not count.
        var project = XDocument.Load(Path.Combine(DesktopDirectory(), "EquipmentBorrowing.Desktop.csproj"));

        Assert.Empty(project.Descendants("ProjectReference"));   // no Application, Infrastructure or Domain

        var packages = project.Descendants("PackageReference")
            .Select(p => (string?)p.Attribute("Include") ?? "")
            .ToList();

        Assert.NotEmpty(packages);   // the check really looked at something
        Assert.DoesNotContain(packages, p =>
            p.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Microsoft.Data", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("SqlClient", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("TC6", "MA6-02")]
    public void Desktop_source_contains_no_database_access_or_sql()
    {
        // Case-sensitive on purpose: LINQ .Select( and UI text such as "Please select" must not trigger it.
        var forbidden = new Regex(
            @"DbContext|Npgsql|EntityFramework|ConnectionString|appsettings|SELECT\s.+\sFROM\s|INSERT\s+INTO|DELETE\s+FROM|UPDATE\s+\S+\s+SET\s");

        var files = DesktopSourceFiles().ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(x => forbidden.IsMatch(x.Text))
            .Select(x => $"{x.File}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, "Database access found in the desktop project:\n" + string.Join("\n", offenders));
    }

    [Fact, Trait("TC6", "MA6-02")]
    public void Desktop_reaches_data_only_through_the_HTTP_client_abstraction()
    {
        // Positive control: the HTTP path really exists and is what the screens depend on.
        var source = DesktopSourceFiles().ToDictionary(f => f, File.ReadAllText);

        Assert.Contains(source, kv => kv.Key.EndsWith("EquipmentApiClient.cs") && kv.Value.Contains("HttpClient"));
        Assert.Contains(source, kv => kv.Key.EndsWith("App.axaml.cs") && kv.Value.Contains("HttpClient"));

        var viewModels = source.Where(kv => kv.Key.Contains($"{Path.DirectorySeparatorChar}ViewModels{Path.DirectorySeparatorChar}")).ToList();
        Assert.NotEmpty(viewModels);
        Assert.Contains(viewModels, kv => kv.Value.Contains("IEquipmentApi"));

        // ViewModels use the abstraction, never a raw HttpClient: transport stays in one place.
        Assert.DoesNotContain(viewModels, kv => Regex.IsMatch(kv.Value, @"\bHttpClient\b"));
    }
}
