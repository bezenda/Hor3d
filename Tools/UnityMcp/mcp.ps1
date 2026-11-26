param([string]$Tool, [string]$ArgsJsonFile)
# Minimal MCP streamable-HTTP client for the Unity MCP server on 8080.
$base = "http://127.0.0.1:8080/mcp"
$h = @{ "Accept" = "application/json, text/event-stream"; "Content-Type" = "application/json" }
$init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"cc","version":"1"}}}'
$r = Invoke-WebRequest -Uri $base -Method Post -Headers $h -Body $init -UseBasicParsing
$sid = $r.Headers["Mcp-Session-Id"]
if ($sid) { $h["Mcp-Session-Id"] = $sid }
Invoke-WebRequest -Uri $base -Method Post -Headers $h -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' -UseBasicParsing | Out-Null
if ($Tool -eq "list") {
  $body = '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
} else {
  $a = Get-Content $ArgsJsonFile -Raw
  $body = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"' + $Tool + '","arguments":' + $a + '}}'
}
$r = Invoke-WebRequest -Uri $base -Method Post -Headers $h -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -UseBasicParsing -TimeoutSec 600
$text = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
foreach ($line in $text -split "`n") {
  if ($line.StartsWith("data:")) { $line = $line.Substring(5) }
  $line = $line.Trim()
  if ($line.StartsWith("{")) {
    try { $j = $line | ConvertFrom-Json } catch { continue }
    if ($j.result.content) { foreach ($c in $j.result.content) { $c.text } }
    elseif ($j.result.tools) { $j.result.tools | ForEach-Object { $_.name } }
    elseif ($j.error) { "ERROR: " + ($j.error | ConvertTo-Json -Compress) }
  }
}
