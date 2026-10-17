param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$assets = Join-Path $project 'Assets'
$backup = Join-Path $project 'MilestroAtlasBackup'
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt'))) {
    throw 'Expected an existing Unity project.'
}
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Close Unity before synchronizing validation assets.'
}
$roots = @('Milestro', 'Milestro.Editor', 'Milestro.Experimental', 'Milestro.InputSystem', 'Milestro.Tests', 'Milestro.InputSystem.Tests')
foreach ($root in $roots) {
    $source = Join-Path $repository "apps\unity-plugins\$root"
    $destination = Join-Path $assets $root
    $saved = Join-Path $backup $root
    if (-not (Test-Path -LiteralPath $saved) -and (Test-Path -LiteralPath $destination)) {
        $resolved = (Resolve-Path -LiteralPath $destination).Path
        if (-not $resolved.StartsWith($assets + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected backup target: $resolved"
        }
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Move-Item -LiteralPath $resolved -Destination $saved
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $destination -Recurse -Force
}
$nativeDestination = Join-Path $assets 'Milestro\Plugins\Win64'
New-Item -ItemType Directory -Path $nativeDestination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'cmake-build-nocli-relwithdebinfo\bin\libMilestro.dll') -Destination $nativeDestination -Force
$icuDestination = Join-Path $assets 'Resources\Milestro'
New-Item -ItemType Directory -Path $icuDestination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'ext\icu-cmake\common\icudtl.dat') -Destination (Join-Path $icuDestination 'icudtl.dat.bytes') -Force
Write-Output "Synchronized Milestro validation assets. Original directories are recoverable at $backup."
