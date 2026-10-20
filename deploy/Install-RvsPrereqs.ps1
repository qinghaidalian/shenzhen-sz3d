<#
.SYNOPSIS
    RVS 服务器端一键安装脚本 —— 在 EC2 Windows Server 2022 上安装运行 RVS 所需的全部依赖。

.DESCRIPTION
    自动完成：
      1. 关闭 IE 增强安全配置（否则在服务器上下载会被拦）
      2. 启用 TLS 1.2（老系统默认可能未开，导致下载失败）
      3. 安装 .NET 10 Desktop Runtime（WPF 主程序必需）
      4. 安装 ASP.NET Core 10 Runtime（AiService gRPC 后端需要，可用 -SkipAspNetCore 跳过）
      5. 安装 Visual C++ Redistributable x64（原生依赖保险）
      6. 创建 RVS 约定的运行目录（plugins / autosave / logs / config）
      7. 安装后验证 dotnet --list-runtimes

    运行时通过微软官方 dotnet-install.ps1 按 10.0 通道安装，始终获取该通道最新补丁版，
    无需硬编码版本号。默认安装到 C:\Program Files\dotnet（机器级）。

.PARAMETER InstallRoot
    .NET 运行时安装目录。默认 "C:\Program Files\dotnet"（标准机器级位置）。

.PARAMETER RvsHome
    RVS 程序所在目录，用于创建运行子目录。默认 "C:\RVS"。

.PARAMETER SkipAspNetCore
    跳过 ASP.NET Core Runtime 安装（纯前端演示、不起 AiService 时使用）。

.EXAMPLE
    # 在服务器上，以管理员身份打开 PowerShell，然后：
    powershell -ExecutionPolicy Bypass -File .\Install-RvsPrereqs.ps1

.EXAMPLE
    # 纯前端演示，不装 AiService 运行时：
    powershell -ExecutionPolicy Bypass -File .\Install-RvsPrereqs.ps1 -SkipAspNetCore

.NOTES
    必须以管理员身份运行。
#>

[CmdletBinding()]
param(
    [string]$InstallRoot = "C:\Program Files\dotnet",
    [string]$RvsHome     = "C:\RVS",
    [switch]$SkipAspNetCore
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

function Test-IsAdmin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---------------------------------------------------------------------------
# 0. 前置检查
# ---------------------------------------------------------------------------
if (-not (Test-IsAdmin)) {
    Write-Error "请以【管理员身份】运行 PowerShell 后再执行本脚本。"
    exit 1
}

Write-Host "RVS 依赖一键安装脚本" -ForegroundColor Green
Write-Host "安装根目录 : $InstallRoot"
Write-Host "RVS 目录   : $RvsHome"
Write-Host "装 AiService运行时: $(-not $SkipAspNetCore)"

$work = Join-Path $env:TEMP ("rvs_setup_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Path $work -Force | Out-Null

# ---------------------------------------------------------------------------
# 1. 关闭 IE 增强安全配置（ESC）
# ---------------------------------------------------------------------------
Write-Step "关闭 IE 增强安全配置"
try {
    $adminKey = "HKLM:\SOFTWARE\Microsoft\Active Setup\Installed Components\{A509B1A7-37EF-4b3f-8CFC-4F3A74704073}"
    $userKey  = "HKLM:\SOFTWARE\Microsoft\Active Setup\Installed Components\{A509B1A8-37EF-4b3f-8CFC-4F3A74704073}"
    if (Test-Path $adminKey) { Set-ItemProperty -Path $adminKey -Name "IsInstalled" -Value 0 -ErrorAction SilentlyContinue }
    if (Test-Path $userKey)  { Set-ItemProperty -Path $userKey  -Name "IsInstalled" -Value 0 -ErrorAction SilentlyContinue }
    Write-Host "  已关闭（如仍被拦，可手动在服务器管理器里关闭）。"
} catch {
    Write-Warning "  关闭 IE ESC 失败（不影响后续，脚本内下载不走 IE）：$($_.Exception.Message)"
}

# ---------------------------------------------------------------------------
# 2. 启用 TLS 1.2
# ---------------------------------------------------------------------------
Write-Step "启用 TLS 1.2"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
Write-Host "  已启用。"

# ---------------------------------------------------------------------------
# 3. 下载官方 dotnet-install.ps1
# ---------------------------------------------------------------------------
Write-Step "下载 dotnet-install.ps1"
$dotnetInstall = Join-Path $work "dotnet-install.ps1"
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstall -UseBasicParsing
Write-Host "  已下载到 $dotnetInstall"

# ---------------------------------------------------------------------------
# 4. 安装 .NET 10 Desktop Runtime（WPF 必需）
# ---------------------------------------------------------------------------
Write-Step "安装 .NET 10 Desktop Runtime (windowsdesktop)"
& $dotnetInstall -Channel 10.0 -Runtime windowsdesktop -InstallDir $InstallRoot -Architecture x64 -NoPath
Write-Host "  .NET 10 Desktop Runtime 安装完成。"

# ---------------------------------------------------------------------------
# 5. 安装 ASP.NET Core 10 Runtime（AiService 需要）
# ---------------------------------------------------------------------------
if (-not $SkipAspNetCore) {
    Write-Step "安装 ASP.NET Core 10 Runtime (aspnetcore)"
    & $dotnetInstall -Channel 10.0 -Runtime aspnetcore -InstallDir $InstallRoot -Architecture x64 -NoPath
    Write-Host "  ASP.NET Core 10 Runtime 安装完成。"
} else {
    Write-Step "跳过 ASP.NET Core 10 Runtime（-SkipAspNetCore）"
}

# ---------------------------------------------------------------------------
# 6. 把 dotnet 加入机器级 PATH
# ---------------------------------------------------------------------------
Write-Step "配置 PATH 环境变量"
$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($machinePath -notlike "*$InstallRoot*") {
    [Environment]::SetEnvironmentVariable("Path", "$machinePath;$InstallRoot", "Machine")
    $env:Path = "$env:Path;$InstallRoot"
    Write-Host "  已将 $InstallRoot 加入机器级 PATH（新开的终端生效）。"
} else {
    Write-Host "  PATH 已包含 $InstallRoot。"
}
# 让 host 能找到运行时
[Environment]::SetEnvironmentVariable("DOTNET_ROOT", $InstallRoot, "Machine")
$env:DOTNET_ROOT = $InstallRoot

# ---------------------------------------------------------------------------
# 7. 安装 Visual C++ Redistributable x64
# ---------------------------------------------------------------------------
Write-Step "安装 Visual C++ Redistributable (x64)"
try {
    $vcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    $vcExe = Join-Path $work "vc_redist.x64.exe"
    Invoke-WebRequest -Uri $vcUrl -OutFile $vcExe -UseBasicParsing
    $p = Start-Process -FilePath $vcExe -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
    if ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010) {
        Write-Host "  VC++ Redistributable 安装完成（ExitCode=$($p.ExitCode)）。"
    } else {
        Write-Warning "  VC++ 安装返回 ExitCode=$($p.ExitCode)（可能已安装过，通常可忽略）。"
    }
} catch {
    Write-Warning "  VC++ 下载/安装失败：$($_.Exception.Message)"
}

# ---------------------------------------------------------------------------
# 8. 创建 RVS 运行目录
# ---------------------------------------------------------------------------
Write-Step "创建 RVS 运行目录"
foreach ($sub in @("", "plugins", "autosave", "logs", "config")) {
    $dir = if ($sub) { Join-Path $RvsHome $sub } else { $RvsHome }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Write-Host "  $dir"
}

# ---------------------------------------------------------------------------
# 9. 验证
# ---------------------------------------------------------------------------
Write-Step "验证已安装运行时"
$dotnetExe = Join-Path $InstallRoot "dotnet.exe"
if (Test-Path $dotnetExe) {
    & $dotnetExe --list-runtimes
    Write-Host ""
    $runtimes = & $dotnetExe --list-runtimes
    $hasDesktop = $runtimes | Where-Object { $_ -match "Microsoft\.WindowsDesktop\.App 10\." }
    $hasAspNet  = $runtimes | Where-Object { $_ -match "Microsoft\.AspNetCore\.App 10\." }

    Write-Host ""
    Write-Host "---- 检查结果 ----" -ForegroundColor Green
    if ($hasDesktop) { Write-Host "  [OK] .NET 10 Desktop Runtime 已安装（WPF 可运行）" -ForegroundColor Green }
    else             { Write-Host "  [缺] 未检测到 .NET 10 Desktop Runtime！" -ForegroundColor Red }

    if (-not $SkipAspNetCore) {
        if ($hasAspNet) { Write-Host "  [OK] ASP.NET Core 10 Runtime 已安装（AiService 可运行）" -ForegroundColor Green }
        else            { Write-Host "  [缺] 未检测到 ASP.NET Core 10 Runtime！" -ForegroundColor Red }
    }
} else {
    Write-Error "未找到 $dotnetExe，安装可能失败。"
}

# ---------------------------------------------------------------------------
# 清理
# ---------------------------------------------------------------------------
Write-Step "清理临时文件"
Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  完成。"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " 全部完成！" -ForegroundColor Green
Write-Host " 下一步：把 RVS 程序拷到 $RvsHome，运行 Sz3D.Presentation.WPF.exe" -ForegroundColor Green
Write-Host " 注意：新开一个 PowerShell 窗口 dotnet 命令才会生效（PATH 刷新）。" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green
