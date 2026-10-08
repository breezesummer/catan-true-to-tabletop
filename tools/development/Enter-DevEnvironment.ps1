# Dot-source this script: . .\tools\development\Enter-DevEnvironment.ps1
$ErrorActionPreference = 'Stop'
$toolchain = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$devRoot = $toolchain.developmentRoot
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectLocalRoot = Join-Path $projectRoot '.local\development'
$variables = @{
    DOTNET_ROOT = Join-Path $devRoot 'dotnet'
    DOTNET_ROOT_X64 = Join-Path $devRoot 'dotnet'
    DOTNET_CLI_HOME = Join-Path $projectLocalRoot 'caches\dotnet'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_MULTILEVEL_LOOKUP = '0'
    NUGET_PACKAGES = Join-Path $projectLocalRoot 'caches\NuGet\packages'
    NUGET_HTTP_CACHE_PATH = Join-Path $projectLocalRoot 'caches\NuGet\http'
    NUGET_PLUGINS_CACHE_PATH = Join-Path $projectLocalRoot 'caches\NuGet\plugins'
    UPM_CACHE_ROOT = Join-Path $projectLocalRoot 'caches\Unity\upm'
    ASSETSTORE_CACHE_PATH = Join-Path $projectLocalRoot 'caches\Unity\AssetStore'
    BLENDER_USER_RESOURCES = Join-Path $projectLocalRoot 'blender\4.5'
    TEMP = Join-Path $projectLocalRoot 'temp'
    TMP = Join-Path $projectLocalRoot 'temp'
}
foreach ($entry in $variables.GetEnumerator()) {
    if ($entry.Value.StartsWith($devRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $entry.Value.StartsWith($projectLocalRoot, [StringComparison]::OrdinalIgnoreCase)) {
        New-Item -ItemType Directory -Path $entry.Value -Force | Out-Null
    }
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}
$toolPaths = @((Split-Path $toolchain.dotnet), (Split-Path $toolchain.blenderExecutable),
    (Split-Path $toolchain.unity), (Split-Path $toolchain.code))
$otherPaths = $env:PATH -split ';' | Where-Object { $_ -and $_ -notin $toolPaths }
$env:PATH = ($toolPaths + $otherPaths) -join ';'
