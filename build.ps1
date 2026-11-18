param([string]$KspPath = 'D:\SteamLibrary\steamapps\common\Kerbal Space Program')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$managed = Join-Path $KspPath 'KSP_x64_Data\Managed'
$utilities = Join-Path $KspPath 'GameData\000_AT_Utils\Plugins'
$compilerRoot = Join-Path $build 'deps\compiler'
$compiler = Join-Path $compilerRoot 'tasks\net472\csc.exe'
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $build -Force | Out-Null
if (!(Test-Path -LiteralPath $compiler)) {
    $package = Join-Path $build 'deps\compiler.nupkg'
    New-Item -ItemType Directory -Path (Split-Path $package) -Force | Out-Null
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.8.0/microsoft.net.compilers.toolset.4.8.0.nupkg' -OutFile $package
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $compilerRoot)
}

$references = @('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'KSPAssets.dll',
    'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.AnimationModule.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.PhysicsModule.dll', 'UnityEngine.UI.dll', 'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.AssetBundleModule.dll', 'UnityEngine.AudioModule.dll') |
    ForEach-Object { Join-Path $managed $_ }
$references += @('000_AT_Utils.dll', '0_00_AT_Utils_UI.dll') |
    ForEach-Object { Join-Path $utilities $_ }

function Build-Tca([string]$source, [string]$output) {
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $arguments = @('/nologo', '/target:library', '/optimize+', '/langversion:latest',
        '/utf8output', '/codepage:65001', '/nowarn:0649')
    $arguments += $references | ForEach-Object { '/reference:' + $_ }
    [xml]$uiProject = Get-Content -LiteralPath (Join-Path $source 'TCA.UI\TCA.UI.csproj')
    $uiSources = $uiProject.Project.ItemGroup.Compile.Include | Where-Object { $_ } |
        ForEach-Object { Join-Path (Join-Path $source 'TCA.UI') $_ }
    & $compiler @arguments ('/out:' + (Join-Path $output 'TCA.UI.dll')) @uiSources
    if ($LASTEXITCODE -ne 0) { throw 'TCA.UI compilation failed.' }
    [xml]$project = Get-Content -LiteralPath (Join-Path $source 'ThrottleControlledAvionics.csproj')
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ } |
        ForEach-Object { Join-Path $source $_ })
    foreach ($extra in @('Loc.cs', 'GUI\HUD\HudLocalization.cs')) {
        $file = Join-Path $source $extra
        if ((Test-Path -LiteralPath $file) -and $file -notin $sources) { $sources += $file }
    }
    & $compiler @arguments ('/reference:' + (Join-Path $output 'TCA.UI.dll')) `
        ('/out:' + (Join-Path $output 'ThrottleControlledAvionics.dll')) @sources
    if ($LASTEXITCODE -ne 0) { throw 'TCA compilation failed.' }
}

$baseline = Join-Path $build 'baseline'
if (!(Test-Path -LiteralPath $baseline)) {
    $archive = Join-Path $build 'baseline.zip'
    git -C $root archive --format=zip ('--output=' + $archive) v3.8.0.1
    if ($LASTEXITCODE -ne 0) { throw 'Official v3.8.0.1 source is required for the baseline check.' }
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $baseline)
}
Build-Tca $baseline (Join-Path $build 'baseline-plugins')
Build-Tca $root (Join-Path $build 'Plugins')
Write-Output ('Built baseline and localized TCA against: ' + $KspPath)

$distribution = Join-Path $build 'package'
if (Test-Path -LiteralPath $distribution) {
    $staging = Get-Item -LiteralPath $distribution
    if ($staging.FullName -ne (Join-Path $root 'build\package') -or
        ($staging.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Package staging must be the regular build/package directory.'
    }
    Remove-Item -LiteralPath $staging.FullName -Recurse -Force
}
$mod = Join-Path $distribution 'GameData\ThrottleControlledAvionics'
New-Item -ItemType Directory -Path $mod -Force | Out-Null
$sourceMod = Join-Path $root 'GameData\ThrottleControlledAvionics'
foreach ($file in Get-ChildItem -LiteralPath $sourceMod -File -Recurse) {
    if ($file.Name -in @('config.xml', 'TCA.conf', 'TCA.macro')) { continue }
    $target = Join-Path $mod $file.FullName.Substring($sourceMod.Length + 1)
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
# Git stores this bundle as a cross-repository symlink; use the installed official asset.
$bundle = Join-Path $KspPath 'GameData\ThrottleControlledAvionics\tca_ui.bundle'
if ((Get-Item -LiteralPath $bundle).Length -lt 1024) { throw 'The installed official tca_ui.bundle is required.' }
Copy-Item -LiteralPath $bundle -Destination (Join-Path $mod 'tca_ui.bundle') -Force
Copy-Item -Path (Join-Path $build 'Plugins\*.dll') -Destination (Join-Path $mod 'Plugins') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE') -Destination $distribution -Force
$zip = Join-Path $build 'TCA-3.8.0.1-zh-CN.1.zip'
Compress-Archive -Path (Join-Path $distribution '*') -DestinationPath $zip -Force
Write-Output ('Packaged: ' + $zip)
