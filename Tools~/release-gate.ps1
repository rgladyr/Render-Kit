<#
.SYNOPSIS
Проверяет локацию перед отправкой на сервер. Вызывается хуком pre-push, который ставит Render Kit.

.DESCRIPTION
Если проект открыт в Unity, кладёт файл-запрос в Library/PaperFly/ReleaseGate и ждёт, пока открытый
редактор выполнит проверку и запишет результат. Если проект закрыт, запускает Unity той же версии
без окна с той же проверкой.

Код выхода 0 — ошибок нет, 1 — есть ошибки или проверку не удалось выполнить.

Unity проверяет проект в том виде, в каком он открыт, поэтому сначала скрипт убеждается, что это и есть
отправляемая версия: проект стоит на отправляемом коммите, а в папке пакета нет незакоммиченных изменений.

.PARAMETER PushedObjects
Отправляемые коммиты и теги, которые передаёт хук.

.PARAMETER TimeoutSeconds
Сколько ждать ответа открытой Unity.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $PushedObjects,

    [int] $TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$MethodName = 'PaperFly.RenderKit.Release.ReleaseGate.RunFromCommandLine'

function Test-UnityOpen([string] $project) {
    $lockFile = Join-Path $project 'Temp\UnityLockfile'

    if (-not (Test-Path $lockFile)) {
        return $false
    }

    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()

        return $false
    }
    catch {
        return $true
    }
}

function Find-Unity([string] $project) {
    $versionFile = Join-Path $project 'ProjectSettings\ProjectVersion.txt'
    $version = ((Get-Content $versionFile | Where-Object { $_ -like 'm_EditorVersion:*' }) -split ':\s*')[1].Trim()

    $roots = @()

    if ($env:PAPERFLY_UNITY_EDITORS) {
        $roots += $env:PAPERFLY_UNITY_EDITORS
    }

    # Папку установки редакторов, если её поменяли, Unity Hub хранит в secondaryInstallPath.json.
    $hubSetting = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'

    if (Test-Path $hubSetting) {
        $roots += (Get-Content $hubSetting -Raw | ConvertFrom-Json)
    }

    $roots += 'C:\Program Files\Unity\Hub\Editor'

    foreach ($root in $roots | Where-Object { $_ }) {
        $candidate = Join-Path $root "$version\Editor\Unity.exe"

        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Не найдена Unity $version. Укажите папку с редакторами в переменной PAPERFLY_UNITY_EDITORS."
}

function Wait-OpenEditor([string] $resultPath, [string] $requestId) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if (Test-Path $resultPath) {
            $lines = @(Get-Content $resultPath -Encoding UTF8)

            if ($lines.Count -gt 0 -and $lines[0] -eq $requestId) {
                return $lines
            }
        }

        Start-Sleep -Milliseconds 500
    }

    return $null
}

function Get-ReleaseErrors([string[]] $pushedObjects) {
    $errors = @()
    $head = (git rev-parse HEAD).Trim()

    foreach ($pushedObject in $pushedObjects) {
        # Аннотированный тег указывает на объект тега, а не на коммит: разыменовываем.
        $commit = (git rev-parse "$pushedObject^{commit}").Trim()
        $short = $commit.Substring(0, 7)

        if ($commit -ne $head) {
            $errors += "Отправляется коммит $short, а проект открыт на $($head.Substring(0, 7)). Переключитесь на отправляемый коммит и повторите push."
        }
    }

    # Смотрим только папки пакетов: остальной проект в выпуск не входит.
    $packageFolders = @(git ls-files -- 'Assets/**/package.json' | ForEach-Object { Split-Path $_ -Parent })

    foreach ($folder in $packageFolders) {
        $changes = @(git status --porcelain --untracked-files=all -- $folder)

        if ($changes.Count -gt 0) {
            $errors += "В папке пакета $($folder.Replace('\', '/')) есть незакоммиченные изменения ($($changes.Count)), проверка увидела бы не ту версию. Закоммитьте или отмените их."
        }
    }

    return $errors
}

function Invoke-BatchEditor([string] $project, [string] $resultPath) {
    $unity = Find-Unity $project
    $logPath = Join-Path $project 'Library\PaperFly\ReleaseGate\unity.log'

    Write-Host "Проект закрыт: запускаю Unity без окна, это займёт около минуты..."

    $arguments = @(
        '-batchmode', '-nographics',
        '-projectPath', "`"$project`"",
        '-executeMethod', $MethodName,
        '-logFile', "`"$logPath`""
    )

    $process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow

    if (Test-Path $resultPath) {
        return @(Get-Content $resultPath -Encoding UTF8)
    }

    Write-Host "Unity завершилась с кодом $($process.ExitCode) и не записала результат. Лог: $logPath"

    return $null
}

$project = (git rev-parse --show-toplevel).Replace('/', '\')
Set-Location $project

Write-Host "Проверка локации перед отправкой..."

if ($PushedObjects) {
    $releaseErrors = @(Get-ReleaseErrors $PushedObjects)

    if ($releaseErrors.Count -gt 0) {
        Write-Host "Проверка не пройдена, push отменён:"
        $releaseErrors | ForEach-Object { Write-Host "  - $_" }

        exit 1
    }
}

$folder = Join-Path $project 'Library\PaperFly\ReleaseGate'
$requestPath = Join-Path $folder 'request.txt'
$resultPath = Join-Path $folder 'result.txt'
$requestId = [guid]::NewGuid().ToString('N')

New-Item -ItemType Directory -Force -Path $folder | Out-Null
Remove-Item -LiteralPath $resultPath -ErrorAction SilentlyContinue
[System.IO.File]::WriteAllText($requestPath, $requestId)

$started = Get-Date

if (Test-UnityOpen $project) {
    Write-Host "Проект открыт в Unity: проверку выполняет редактор."
    $lines = Wait-OpenEditor $resultPath $requestId

    if ($null -eq $lines) {
        Remove-Item -LiteralPath $requestPath -ErrorAction SilentlyContinue
        Write-Host "Unity не ответила за $TimeoutSeconds с. Возможно, она занята импортом или компиляцией. Попробуйте ещё раз."

        exit 1
    }
}
else {
    $lines = Invoke-BatchEditor $project $resultPath

    if ($null -eq $lines) {
        exit 1
    }
}

$seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)

if ($lines[1] -eq 'OK') {
    Write-Host "Проверка пройдена за $seconds с."

    exit 0
}

Write-Host "Проверка не пройдена ($seconds с), push отменён. Подробности в консоли Unity и окне отчёта:"
$lines | Select-Object -Skip 2 | ForEach-Object { Write-Host "  - $_" }

exit 1
