param([string]$Url = "http://localhost:5080/api/v1/equipment")

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(10)

try {
    $response = $client.GetAsync($Url).GetAwaiter().GetResult()
    "{0}  HTTP RESPONSE RECEIVED: {1} {2}" -f (Get-Date -Format o), [int]$response.StatusCode, $response.ReasonPhrase
    $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
}
catch {
    $inner = $_.Exception.GetBaseException()
    "{0}  NO HTTP RESPONSE: {1}: {2}" -f (Get-Date -Format o), $inner.GetType().Name, $inner.Message
}
finally {
    $client.Dispose()
}
