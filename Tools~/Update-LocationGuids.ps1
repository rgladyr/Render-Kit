<#
.SYNOPSIS
Выдаёт файлам папки локации новые GUID и обновляет ссылки на них во всём проекте.

.DESCRIPTION
Нужен, когда локацию собирают из копии проекта игры: без смены GUID пакет при подключении к игре
конфликтует с исходными файлами, которые в игре ещё лежат в Assets.

Скрипт меняет GUID в .meta каждого файла и подпапки внутри -Folder и заменяет старые GUID на новые
во всех текстовых файлах проекта (Assets, Packages, ProjectSettings): префабах, материалах, сценах,
.meta моделей и т. д. Ссылки внутри локации и из превью-сцены продолжают работать.

Соответствие старых и новых GUID сохраняется в CSV. С -MapPath без -Folder скрипт применяет готовое
соответствие к другому проекту, например к игре, когда она переходит с исходных файлов на пакет.

Запускать при закрытом Unity. После запуска удалите папку Library проекта: в ней лежат старые GUID.

.PARAMETER ProjectPath
Корень Unity-проекта, папка с Assets и ProjectSettings.

.PARAMETER Folder
Папка локации относительно проекта, например Assets/Childhood. Её файлы получат новые GUID.

.PARAMETER MapPath
CSV с соответствием GUID. С -Folder сюда записывается новое соответствие (по умолчанию
<проект>/guid-map.csv), без -Folder отсюда читается готовое.

.EXAMPLE
.\Update-LocationGuids.ps1 -ProjectPath D:\Work\Projects\PaperFly.AP.Location.Childhood -Folder Assets/Childhood -WhatIf

Показывает, сколько GUID и файлов изменится, ничего не записывая.

.EXAMPLE
.\Update-LocationGuids.ps1 -ProjectPath D:\Work\Projects\PaperFly.AP.Location.Childhood -Folder Assets/Childhood
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectPath,

    [string] $Folder,

    [string] $MapPath
)

$ErrorActionPreference = 'Stop'

# Форматы Unity, в которых хранятся ссылки по GUID. Двоичные файлы (FBX, текстуры) GUID не содержат.
$TextExtensions = @(
    '.meta', '.unity', '.prefab', '.mat', '.asset', '.controller', '.overridecontroller', '.anim',
    '.mask', '.playable', '.preset', '.lighting', '.physicmaterial', '.physicsmaterial2d',
    '.shadergraph', '.shadersubgraph', '.vfx', '.spriteatlas', '.spriteatlasv2', '.terrainlayer',
    '.mixer', '.signal', '.rendertexture', '.cubemap', '.flare', '.giparams', '.brush',
    '.fontsettings', '.guiskin', '.asmdef', '.asmref', '.inputactions', '.uss', '.uxml', '.tss'
)

$GuidPattern = [regex] '(?<![0-9a-fA-F])[0-9a-f]{32}(?![0-9a-fA-F])'
$MetaGuidPattern = [regex] '(?m)^guid: ([0-9a-f]{32})\s*$'

function Assert-UnityClosed([string] $project) {
    $lockFile = Join-Path $project 'Temp\UnityLockfile'

    if (-not (Test-Path $lockFile)) {
        return
    }

    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    }
    catch {
        throw "Проект открыт в Unity. Закройте Unity и запустите скрипт снова."
    }
}

function New-GuidMap([string] $project, [string] $folder) {
    $target = Join-Path $project $folder

    if (-not (Test-Path $target -PathType Container)) {
        throw "Папка не найдена: $target"
    }

    # .meta самой папки лежит рядом с ней, а не внутри.
    $metas = @(Get-ChildItem -LiteralPath $target -Recurse -File -Filter '*.meta')
    $folderMeta = "$target.meta"

    if (Test-Path -LiteralPath $folderMeta) {
        $metas += Get-Item -LiteralPath $folderMeta
    }

    $map = [ordered] @{}

    foreach ($meta in $metas) {
        $match = $MetaGuidPattern.Match([System.IO.File]::ReadAllText($meta.FullName))

        if (-not $match.Success) {
            Write-Warning "Нет GUID в $($meta.FullName), файл пропущен."
            continue
        }

        $relativePath = $meta.FullName.Substring($project.Length + 1).Replace('\', '/')

        $map[$match.Groups[1].Value] = [pscustomobject] @{
            OldGuid = $match.Groups[1].Value
            NewGuid = [guid]::NewGuid().ToString('N')
            Path = $relativePath
        }
    }

    return $map
}

function Read-GuidMap([string] $path) {
    $map = [ordered] @{}

    foreach ($row in Import-Csv -LiteralPath $path) {
        $map[$row.OldGuid] = $row
    }

    return $map
}

function Get-ProjectTextFiles([string] $project) {
    foreach ($root in 'Assets', 'Packages', 'ProjectSettings') {
        $path = Join-Path $project $root

        if (Test-Path $path) {
            Get-ChildItem -LiteralPath $path -Recurse -File |
                Where-Object { $TextExtensions -contains $_.Extension.ToLowerInvariant() }
        }
    }
}

# Кодировка файла сохраняется: Unity пишет UTF-8 без BOM, но asmdef и json бывают с BOM.
function Update-File([System.IO.FileInfo] $file, $map) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    $text = [System.IO.File]::ReadAllText($file.FullName, $encoding)

    $replacements = @($GuidPattern.Matches($text) | Where-Object { $map.Contains($_.Value) }).Count

    if ($replacements -eq 0) {
        return 0
    }

    $updated = $GuidPattern.Replace($text, {
        param($match)

        if ($map.Contains($match.Value)) {
            return $map[$match.Value].NewGuid
        }

        return $match.Value
    })

    if ($PSCmdlet.ShouldProcess($file.FullName, "Заменить GUID: $replacements")) {
        [System.IO.File]::WriteAllText($file.FullName, $updated, $encoding)
    }

    return $replacements
}

$project = (Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\')
Assert-UnityClosed $project

if ($Folder) {
    $map = New-GuidMap $project $Folder.Replace('/', '\')

    if (-not $MapPath) {
        $MapPath = Join-Path $project 'guid-map.csv'
    }
}
elseif ($MapPath) {
    $map = Read-GuidMap $MapPath
}
else {
    throw "Укажите -Folder, чтобы выдать новые GUID, или -MapPath, чтобы применить готовое соответствие."
}

if ($map.Count -eq 0) {
    throw "В папке нет файлов .meta с GUID."
}

$changedFiles = 0
$changedReferences = 0

foreach ($file in Get-ProjectTextFiles $project) {
    $count = Update-File $file $map

    if ($count -gt 0) {
        $changedFiles++
        $changedReferences += $count
    }
}

if ($Folder -and $PSCmdlet.ShouldProcess($MapPath, 'Сохранить соответствие GUID')) {
    $map.Values | Export-Csv -LiteralPath $MapPath -NoTypeInformation -Encoding UTF8
}

Write-Host "GUID в соответствии: $($map.Count)"
Write-Host "Изменено файлов: $changedFiles, ссылок: $changedReferences"

if (-not $WhatIfPreference) {
    Write-Host "Удалите папку Library проекта перед открытием в Unity."
}
