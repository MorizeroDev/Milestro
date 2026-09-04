[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityRoot,
    [ValidateRange(23, 99)]
    [int]$ApiLevel = 26,
    [ValidateSet('arm64-v8a', 'armeabi-v7a')]
    [string]$Abi = 'arm64-v8a',
    [switch]$GlesOnly,
    [switch]$ConfigureOnly,
    [switch]$RunContractTests,
    [switch]$PackageUnity,
    [ValidateRange(1, 128)]
    [int]$Jobs = 6
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$unity = (Resolve-Path -LiteralPath $UnityRoot).Path
$android = Join-Path $unity 'Editor/Data/PlaybackEngines/AndroidPlayer'
$sdk = Join-Path $android 'SDK'
$ndk = Join-Path $android 'NDK'
$jdk = Join-Path $android 'OpenJDK'
foreach ($file in @('NDK/build/cmake/android.toolchain.cmake', 'NDK/source.properties',
                    'SDK/platform-tools/adb.exe', 'OpenJDK/bin/java.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $android $file) -PathType Leaf)) {
        throw "Unity Android toolchain is incomplete: $android/$file"
    }
}
if (-not $GlesOnly -and $ApiLevel -lt 24) {
    throw 'Vulkan requires API 24+. Use -GlesOnly for API 23.'
}
if ($ConfigureOnly -and $PackageUnity) {
    throw '-PackageUnity cannot be combined with -ConfigureOnly.'
}
$revisionLine = Get-Content -LiteralPath (Join-Path $ndk 'source.properties') |
    Where-Object { $_ -match '^Pkg.Revision\s*=' } | Select-Object -First 1
if (-not $revisionLine) { throw 'NDK source.properties has no Pkg.Revision.' }
$revision = ($revisionLine -split '=', 2)[1].Trim()
if ($revision -notmatch '^[0-9A-Za-z._-]+$') { throw "Unexpected NDK revision: $revision" }
$backend = if ($GlesOnly) { 'gles' } else { 'vulkan' }
$build = Join-Path $repo "build/android-unity-$revision-$Abi-api$ApiLevel-$backend"

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    $command = Get-Command $Program -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell treats redirected native stderr as ErrorRecords.
        # Warnings must not terminate a successful CMake/Python/Java invocation.
        $ErrorActionPreference = 'Continue'
        & $command.Source @Arguments 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.Exception.Message }
            else { $_.ToString() }
        }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 0) { throw "$Program exited with code $exitCode" }
}

# Only change this process and its children; restore the caller's environment.
$variables = @('JAVA_HOME', 'ANDROID_HOME', 'ANDROID_SDK_ROOT', 'ANDROID_NDK_HOME', 'ANDROID_NDK', 'GRADLE_OPTS', 'PATH')
$saved = @{}
foreach ($name in $variables) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
Push-Location $repo
try {
    # A persistent Gradle daemon can inherit Ninja output handles on Windows.
    $env:GRADLE_OPTS = "$env:GRADLE_OPTS -Dorg.gradle.daemon=false"
    $env:JAVA_HOME = $jdk
    $env:ANDROID_HOME = $sdk
    $env:ANDROID_SDK_ROOT = $sdk
    $env:ANDROID_NDK_HOME = $ndk
    $env:ANDROID_NDK = $ndk
    $env:PATH = "$jdk/bin;$sdk/platform-tools;$env:PATH"
    Write-Host "Unity: $unity"
    Write-Host "SDK: $sdk"
    Write-Host "NDK: $ndk ($revision)"
    Write-Host "JDK: $jdk"
    Write-Host "Build: $build"
    Invoke-Checked (Join-Path $jdk 'bin/java.exe') @('--version')
    if ($RunContractTests) {
        Invoke-Checked 'python' @('tests/android_build/test_android_graphics_config.py', '--ndk', $ndk)
    }
    $vulkan = if ($GlesOnly) { 'OFF' } else { 'ON' }
    Invoke-Checked 'cmake' @(
        '-S', $repo, '-B', $build, '-G', 'Ninja',
        "-DCMAKE_TOOLCHAIN_FILE=$ndk/build/cmake/android.toolchain.cmake",
        "-DANDROID_ABI=$Abi", "-DANDROID_PLATFORM=android-$ApiLevel",
        '-DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON',
        "-DMILESTRO_ENABLE_ANDROID_VULKAN_RENDER=$vulkan",
        '-DMILESTRO_ENABLE_CLI=OFF', '-DMILESTRO_ENABLE_TESTS=OFF',
        '-DMILESTRO_BUILD_SHARED_LIBS=ON', '-DCMAKE_BUILD_TYPE=Release'
    )
    if (-not $ConfigureOnly) {
        Invoke-Checked 'cmake' @('--build', $build, '--target', 'Milestro', '--parallel', "$Jobs")
        Write-Host "Native library: $build/lib/libMilestro.so"
        if ($PackageUnity) {
            Invoke-Checked 'cmake' @('--build', $build, '--target', 'milestro_unity_plugin')
            # The Gradle target packages matching C# and ICU, but not native binaries.
            $destination = Join-Path $build "unity-plugin/Milestro/Plugins/Android/$Abi"
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            Copy-Item -LiteralPath (Join-Path $build 'lib/libMilestro.so') -Destination $destination
            Write-Host "Unity package: $build/unity-plugin"
        }
    }
}
finally {
    Pop-Location
    foreach ($name in $variables) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
    }
}
