# MA6-01 helper: a tiny forwarding proxy that DELAYS every response, so you can watch the desktop's
# loading state and check that the window stays responsive. It changes nothing in the API or the desktop.
#
#   Terminal 1 (API):      dotnet run --project src\EquipmentBorrowing.Api --urls http://localhost:5080
#   Terminal 2 (proxy):    .\docs\acceptance\Start-SlowProxy.ps1 -DelaySeconds 8
#   Terminal 3 (desktop):  $env:EQUIPMENT_BORROWING_API_URL = "http://localhost:5081"
#                          dotnet run --project src\EquipmentBorrowing.Desktop
#
# The desktop's own HttpClient timeout is 15 seconds. Use -DelaySeconds 8 to see a slow-but-successful
# load, and -DelaySeconds 20 to see the "did not respond in time" message.
# Stop the proxy with Ctrl+C. (It handles one request at a time, which is enough for this check.)

param(
    [int]$ListenPort = 5081,
    [string]$Target = "http://localhost:5080",
    [int]$DelaySeconds = 8
)

Add-Type -AssemblyName System.Net.Http

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$ListenPort/")
$listener.Start()
$client = [System.Net.Http.HttpClient]::new()

Write-Host "Slow proxy: http://localhost:$ListenPort  ->  $Target   (each response delayed $DelaySeconds s). Ctrl+C to stop."

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $received = Get-Date

        Start-Sleep -Seconds $DelaySeconds      # <-- the deliberate delay

        $message = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($request.HttpMethod), $Target + $request.Url.PathAndQuery)

        if ($request.HasEntityBody) {
            $reader = [System.IO.StreamReader]::new($request.InputStream, $request.ContentEncoding)
            $message.Content = [System.Net.Http.StringContent]::new($reader.ReadToEnd(), [System.Text.Encoding]::UTF8, "application/json")
        }

        try {
            $response = $client.SendAsync($message).GetAwaiter().GetResult()
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            $context.Response.StatusCode = [int]$response.StatusCode
            $context.Response.ContentType = "application/json; charset=utf-8"
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $status = [int]$response.StatusCode
        }
        catch {
            $context.Response.StatusCode = 502
            $status = 502
        }

        $context.Response.Close()
        "{0}  {1} {2} -> {3}  (held {4} s)" -f $received.ToString("HH:mm:ss"), $request.HttpMethod, $request.Url.PathAndQuery, $status, $DelaySeconds
    }
}
finally {
    $listener.Stop()
    $client.Dispose()
}
