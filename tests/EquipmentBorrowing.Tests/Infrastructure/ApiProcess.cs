using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EquipmentBorrowing.Tests.Infrastructure;

/// <summary>
/// INTEGRATION HARNESS for TC6-E08: runs the ACTUAL API as a separate operating-system process
/// (`dotnet EquipmentBorrowing.Api.dll`) against the same PostgreSQL test database, so the test can
/// really stop it and start it again. The in-process WebApplicationFactory used elsewhere cannot do that.
/// </summary>
public sealed class ApiProcess : IDisposable
{
    private readonly StringBuilder _log = new();
    private Process? _process;

    public ApiProcess()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{FreePort()}/");
    }

    public Uri BaseAddress { get; }

    public string Log { get { lock (_log) return _log.ToString(); } }

    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>...\tests\EquipmentBorrowing.Tests\bin\Debug\net10.0\  =>  ...\src\EquipmentBorrowing.Api\bin\Debug\net10.0\</summary>
    public static string ApiDllPath()
    {
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = tfm.Parent!.Name;
        var testProject = tfm.Parent!.Parent!.Parent!;
        var repositoryRoot = testProject.Parent!.Parent!;

        var dll = Path.Combine(repositoryRoot.FullName, "src", "EquipmentBorrowing.Api",
            "bin", configuration, tfm.Name, "EquipmentBorrowing.Api.dll");

        return File.Exists(dll)
            ? dll
            : throw new FileNotFoundException($"The API has not been built: {dll}");
    }

    public async Task StartAsync()
    {
        var dll = ApiDllPath();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(dll);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseAddress.ToString().TrimEnd('/'));

        // The child inherits EQUIPMENT_BORROWING_CONNECTION (the *_test database) from this process.
        startInfo.Environment["EQUIPMENT_BORROWING_FIXED_DATE"] = TestDatabase.D.ToString("yyyy-MM-dd");
        startInfo.Environment.Remove(FaultScope.VariableName);

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("The API process did not start.");
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // Wait until a real request is answered with 200.
        using var probe = new HttpClient { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"The API exited during startup (code {_process.ExitCode}).\n{Log}");

            try
            {
                var response = await probe.GetAsync("api/v1/equipment");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not listening yet */ }
            catch (TaskCanceledException) { /* too slow yet */ }

            await Task.Delay(300);
        }

        throw new TimeoutException($"The API did not answer within 45 seconds.\n{Log}");
    }

    /// <summary>Kills the process tree: the equivalent of stopping the service.</summary>
    public void Stop()
    {
        if (_process is null) return;

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(10_000);
        }

        _process.Dispose();
        _process = null;
    }

    public void Dispose() => Stop();

    private void Append(string? line)
    {
        if (line is null) return;
        lock (_log) _log.AppendLine(line);
    }
}
