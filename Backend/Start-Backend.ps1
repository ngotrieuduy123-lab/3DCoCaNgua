$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'CoCaNgua.Server.csproj') --launch-profile CoCaNgua
