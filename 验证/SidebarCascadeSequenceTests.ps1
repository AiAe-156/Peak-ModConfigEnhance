$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot\SidebarCascadeTests\SidebarCascadeTests.csproj" --configuration Release
if ($LASTEXITCODE -ne 0) { throw "SidebarCascadeTests failed with exit code $LASTEXITCODE" }
exit 0

function Assert-Equal([object] $actual, [object] $expected, [string] $message) {
    $a = @($actual) -join ','
    $e = @($expected) -join ','
    if ($a -ne $e) { throw "$message`n实际: $a`n期望: $e" }
}

# 与 SidebarCascade.BuildModWaves 相同的序列规则：首波为表头+选中项，
# 后续每波从选中项向两侧各扩一行；无选中时逐行从顶向下。
function Build-ModWaves([string[]] $rows, [int] $selectedIndex) {
    if ($rows.Count -eq 0) { return @() }
    $firstModIndex = if ($rows[0] -eq 'HEADER') { 1 } else { 0 }
    if ($selectedIndex -lt $firstModIndex -or $selectedIndex -ge $rows.Count) {
        return @($rows | ForEach-Object { ,@($_) })
    }

    $waves = [System.Collections.Generic.List[object]]::new()
    $first = [System.Collections.Generic.List[string]]::new()
    if ($firstModIndex -eq 1) { [void]$first.Add('HEADER') }
    [void]$first.Add($rows[$selectedIndex])
    [void]$waves.Add($first.ToArray())
    for ($distance = 1; $selectedIndex - $distance -ge $firstModIndex -or $selectedIndex + $distance -lt $rows.Count; $distance++) {
        $wave = [System.Collections.Generic.List[string]]::new()
        if ($selectedIndex - $distance -ge $firstModIndex) { [void]$wave.Add($rows[$selectedIndex - $distance]) }
        if ($selectedIndex + $distance -lt $rows.Count) { [void]$wave.Add($rows[$selectedIndex + $distance]) }
        [void]$waves.Add($wave.ToArray())
    }
    return $waves.ToArray()
}

function Flatten($waves) { @($waves | ForEach-Object { $_ }) }

$rows = @('HEADER', 'A', 'B', 'C', 'D', 'E')
Assert-Equal (Flatten (Build-ModWaves $rows 1)) @('HEADER','A','B','C','D','E') '顶部选中顺序错误'
Assert-Equal (Flatten (Build-ModWaves $rows 3)) @('HEADER','C','B','D','A','E') '中间选中顺序错误'
Assert-Equal (Flatten (Build-ModWaves $rows 5)) @('HEADER','E','D','C','B','A') '底部选中顺序错误'
Assert-Equal (Flatten (Build-ModWaves $rows -1)) @('HEADER','A','B','C','D','E') '无选中顺序错误'

# 每个选中序列必须完整且不重复，专门覆盖表头不能在向上扩展时再次进入序列。
foreach ($selected in 1..5) {
    $flat = Flatten (Build-ModWaves $rows $selected)
    Assert-Equal (($flat | Sort-Object -Unique).Count) $rows.Count "选中索引 $selected 存在重复或缺行"
}

# 模拟两套协程所有权：重启模组波不能清理仍在运行的分区波，关闭时两者都恢复。
$mod = @{ A = 0.2; B = 0.0 }
$section = @{ S1 = 0.1 }
$mod['A'] = 1.0; $mod['B'] = 1.0 # Restart(mod) 只恢复自己的 prepared/fading
if ($section['S1'] -ne 0.1) { throw '重启模组波错误地清理了分区波' }
@($mod.Keys) | ForEach-Object { $mod[$_] = 1.0 }
@($section.Keys) | ForEach-Object { $section[$_] = 1.0 }
if (($mod.Values + $section.Values | Where-Object { $_ -ne 1.0 }).Count -ne 0) { throw '关闭后仍有残留透明行' }

Write-Output 'SidebarCascadeSequenceTests: PASS (4 sequences, duplicate-free coverage, ownership cleanup)'
