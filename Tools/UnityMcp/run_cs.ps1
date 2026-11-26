param([string]$CsFile)
$dir = Split-Path $MyInvocation.MyCommand.Path
$code = [System.IO.File]::ReadAllText($CsFile)
$json = @{ action = "execute"; code = $code; safety_checks = $false } | ConvertTo-Json -Compress
$tmp = Join-Path $env:TEMP "unity_mcp_args.json"
[System.IO.File]::WriteAllText($tmp, $json)
& (Join-Path $dir "mcp.ps1") execute_code $tmp
