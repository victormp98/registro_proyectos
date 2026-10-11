param(
    [Parameter(Mandatory = $true)][string]$DatabasePath,
    [Parameter(Mandatory = $true)][string]$StoragePath,
    [Parameter(Mandatory = $true)][string]$DataProtectionKeysPath,
    [Parameter(Mandatory = $true)][string]$ApplicationDirectory,
    [Parameter(Mandatory = $true)][string]$BackupRoot
)

$ErrorActionPreference = 'Stop'
$database = (Resolve-Path -LiteralPath $DatabasePath).Path
$storage = (Resolve-Path -LiteralPath $StoragePath).Path
$keys = (Resolve-Path -LiteralPath $DataProtectionKeysPath).Path
$app = (Resolve-Path -LiteralPath $ApplicationDirectory).Path
$backupBase = [System.IO.Path]::GetFullPath($BackupRoot)
New-Item -ItemType Directory -Path $backupBase -Force | Out-Null

$expectedExecutable = [System.IO.Path]::Combine($app, 'PortalProyectos.Web.exe')
$running = Get-Process -Name 'PortalProyectos.Web' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and [System.IO.Path]::GetFullPath($_.Path) -eq $expectedExecutable }
if ($running) {
    throw 'Detén PortalProyectos.Web.exe antes de respaldar. No se copió ningún dato.'
}

$stamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$destination = Join-Path $backupBase $stamp
New-Item -ItemType Directory -Path $destination | Out-Null
try {
    Copy-Item -LiteralPath $database -Destination (Join-Path $destination ([System.IO.Path]::GetFileName($database)))
    foreach ($suffix in @('-wal', '-shm')) {
        $sidecar = "$database$suffix"
        if (Test-Path -LiteralPath $sidecar) {
            Copy-Item -LiteralPath $sidecar -Destination (Join-Path $destination ([System.IO.Path]::GetFileName($sidecar)))
        }
    }
    Copy-Item -LiteralPath $storage -Destination (Join-Path $destination 'storage') -Recurse
    Copy-Item -LiteralPath $keys -Destination (Join-Path $destination 'data-protection-keys') -Recurse
    $configDestination = Join-Path $destination 'configuration'
    New-Item -ItemType Directory -Path $configDestination | Out-Null
    Get-ChildItem -LiteralPath $app -Filter 'appsettings*.json' -File |
        Copy-Item -Destination $configDestination
    Get-ChildItem -LiteralPath $destination -Recurse -File |
        Get-FileHash -Algorithm SHA256 |
        Select-Object Hash, Path |
        Export-Csv -LiteralPath (Join-Path $destination 'SHA256.csv') -NoTypeInformation -Encoding UTF8
    Write-Output "Respaldo completo creado: $destination"
}
catch {
    Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue
    throw
}
