param([string]$KspPath = 'D:\SteamLibrary\steamapps\common\Kerbal Space Program')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$locales = @{
    'en-us' = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    'zh-cn' = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
}
foreach ($file in Get-ChildItem (Join-Path $root 'GameData\ThrottleControlledAvionics\Localization') -Filter '*.cfg') {
    $locale = $null
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        if ($line.Trim() -in @('en-us', 'zh-cn')) { $locale = $line.Trim() }
        if ($line -match '^\s*(#LOC_TCA_\w+)\s*=\s*(.*)$') {
            $key = $Matches[1]
            if ($locales[$locale].ContainsKey($key)) { throw "Duplicate $locale key: $key" }
            $locales[$locale][$key] = $Matches[2]
        }
    }
}
$difference = Compare-Object @($locales['en-us'].Keys) @($locales['zh-cn'].Keys) -CaseSensitive
if ($difference) { throw ('Language keys differ: ' + ($difference.InputObject -join ', ')) }
foreach ($key in $locales['en-us'].Keys) {
    $signatures = foreach ($locale in @('en-us', 'zh-cn')) {
        $tokens = [regex]::Matches($locales[$locale][$key], '\{\d+(?:,-?\d+)?(?::[^}]+)?\}|<<\d+>>') |
            ForEach-Object { $_.Value } | Sort-Object
        $tokens -join '|'
    }
    if ($signatures[0] -ne $signatures[1]) { throw "Format placeholders differ: $key ($($signatures -join ' / '))" }
}
$sources = Get-ChildItem $root -Filter '*.cs' -Recurse | Where-Object {
    $_.FullName -notmatch '\\build\\|\\Tests\\|\\Testing\\'
}
$used = [Collections.Generic.HashSet[string]]::new()
foreach ($file in $sources) {
    $source = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($source, 'Loc\.(?:T|F|Resolve)\(\s*"(\w+)"\s*(?=,|\))')) {
        [void]$used.Add('#LOC_TCA_' + $match.Groups[1].Value)
    }
    foreach ($match in [regex]::Matches($source, '"(#LOC_TCA_\w+)"')) { [void]$used.Add($match.Groups[1].Value) }
    foreach ($match in [regex]::Matches($source, 'Loc\.Content\(\s*"(\w+)"\s*,\s*"(?:\\.|[^"\\])*"\s*,\s*"(\w+)"')) {
        [void]$used.Add('#LOC_TCA_' + $match.Groups[1].Value)
        [void]$used.Add('#LOC_TCA_' + $match.Groups[2].Value)
    }
}
foreach ($key in $used) {
    if (!$locales['zh-cn'].ContainsKey($key)) { throw "Missing translation: $key" }
}
$config = Join-Path $root 'GameData\ThrottleControlledAvionics\TCATechTree.cfg'
$original = git -C $root show 'v3.8.0.1:GameData/ThrottleControlledAvionics/TCATechTree.cfg'
$current = Get-Content -LiteralPath $config
$oldFields = ($original | Where-Object { $_ -notmatch '^\s*(title|description|manufacturer)\s*=' -and $_.Trim() } | ForEach-Object { $_.Trim() }) -join "`n"
$newFields = ($current | Where-Object { $_ -notmatch '^\s*(title|description|manufacturer)\s*=' -and $_.Trim() } | ForEach-Object { $_.Trim() }) -join "`n"
if ($oldFields -ne $newFields) { throw 'Non-display research module configuration changed.' }
$managed = Join-Path $KspPath 'KSP_x64_Data\Managed'
$compilerFolder = Join-Path $root 'build\deps\compiler\tasks\net472'
Copy-Item -LiteralPath (Join-Path $managed 'Mono.Cecil.dll') -Destination $compilerFolder -Force
$exe = Join-Path $compilerFolder 'LocalizationChecks.exe'
& (Join-Path $compilerFolder 'csc.exe') /nologo /target:exe /optimize+ `
    ('/reference:' + (Join-Path $managed 'Mono.Cecil.dll')) ('/out:' + $exe) `
    (Join-Path $root 'Tests\LocalizationChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Localization verifier compilation failed.' }
& $exe $root
if ($LASTEXITCODE -ne 0) { throw 'Baseline behavior comparison failed.' }
Write-Output ('PASS: ' + $locales['zh-cn'].Count + ' bilingual keys; matching format placeholders; ' + $used.Count + ' explicit source keys resolved.')
Write-Output 'PASS: research part IDs, costs, technology assignments and other non-display fields unchanged.'
