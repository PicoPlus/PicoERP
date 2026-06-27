# ============================================================
#  PicoERP -- Windows Plesk / IIS Deploy Script
#  Builds a framework-dependent publish, then uploads via FTP.
#
#  Prerequisites on the Plesk server:
#    - ASP.NET Core 9 Runtime (Hosting Bundle) installed
#    - The application's IIS App Pool must use .NET CLR "No Managed Code"
#      and its identity must have Write permission on the app folder.
#
#  Usage (defaults pre-filled):
#    .\deploy.ps1
#
#  Override any param from the command line:
#    .\deploy.ps1 -FtpPassword "newpass"
# ============================================================

param(
    [string]$FtpHost     = "78.129.155.237",
    [string]$FtpUser     = "erplocale",
    [string]$FtpPassword = "#y1Wm23k8",
    # On Plesk the FTP user's home IS the site root, so uploads go to "/"
    # (the FTP home = /erp.novincn.ir on disk).
    # Change to "/subfolder" only if you want files inside a sub-directory.
    [string]$RemotePath  = "/",
    [string]$PublishDir  = ".\publish_output",
    # Skip dotnet publish and re-use the existing publish_output folder.
    [bool]$SkipBuild     = $false,
    # Set to $true to publish as self-contained (no runtime needed on server).
    [bool]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------
# 1. Build & Publish  (skipped when -SkipBuild $true)
# -----------------------------------------------------------------------
Write-Host ""
if ($SkipBuild) {
    Write-Host "[1/3] Skipping publish (using existing $PublishDir)." -ForegroundColor Yellow
} else {
    Write-Host "[1/3] Publishing PicoERP.Web (Release, win-x64)..." -ForegroundColor Cyan

    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    $publishArgs = @(
        "publish", "src\PicoERP.Web\PicoERP.Web.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "-o", $PublishDir,
        "/p:UseAppHost=true"
    )

    if ($SelfContained) {
        $publishArgs += "--self-contained", "true"
    } else {
        $publishArgs += "--self-contained", "false"
    }

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed. Aborting deploy."
        exit 1
    }

    Write-Host "[1/3] Publish complete." -ForegroundColor Green
}

# -----------------------------------------------------------------------
# 2. Collect files
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "[2/3] Collecting published files..." -ForegroundColor Cyan

$resolvedPublish = (Resolve-Path $PublishDir).Path
$files = Get-ChildItem -Path $resolvedPublish -Recurse -File
Write-Host "      Found $($files.Count) files to upload." -ForegroundColor Gray

# -----------------------------------------------------------------------
# 3. FTP Upload
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "[3/3] Uploading to ftp://$FtpHost$RemotePath ..." -ForegroundColor Cyan

$ftpBase = "ftp://$FtpHost$RemotePath"
$cred    = New-Object System.Net.NetworkCredential($FtpUser, $FtpPassword)

function Ftp-EnsureDirectory {
    param([string]$dirUrl)
    try {
        $req             = [System.Net.FtpWebRequest]::Create($dirUrl)
        $req.Credentials = $cred
        $req.Method      = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
        $req.UsePassive  = $true
        $req.UseBinary   = $true
        $req.KeepAlive   = $false
        $resp = $req.GetResponse()
        $resp.Close()
    } catch {
        # 550 = directory already exists on most FTP servers -- safe to ignore.
        if ($_.Exception.Message -notmatch "550") {
            Write-Warning "MkDir warning [$dirUrl]: $($_.Exception.Message)"
        }
    }
}

# Ensure every segment of the remote path exists (skip if RemotePath is just "/").
$rootTrimmed = $RemotePath.Trim('/')
if ($rootTrimmed -ne "") {
    $builtPath = ""
    foreach ($seg in ($rootTrimmed -split '/')) {
        if ($seg -eq "") { continue }
        $builtPath += "/$seg"
        Ftp-EnsureDirectory "ftp://$FtpHost$builtPath"
    }
}
Write-Host "      Remote root: ftp://$FtpHost$RemotePath" -ForegroundColor Gray

function Ftp-UploadFile {
    param([string]$localPath, [string]$remoteUrl)
    $req              = [System.Net.FtpWebRequest]::Create($remoteUrl)
    $req.Credentials  = $cred
    $req.Method       = [System.Net.WebRequestMethods+Ftp]::UploadFile
    $req.UsePassive   = $true
    $req.UseBinary    = $true
    $req.KeepAlive    = $false
    $fileBytes        = [System.IO.File]::ReadAllBytes($localPath)
    $req.ContentLength = $fileBytes.Length
    $stream = $req.GetRequestStream()
    $stream.Write($fileBytes, 0, $fileBytes.Length)
    $stream.Close()
    $resp = $req.GetResponse()
    $resp.Close()
}

$uploadOk    = 0
$uploadFail  = 0
$createdDirs = @{}

# Build a clean base URL: strip any trailing slash so we can always append /relative.
$ftpBaseClean = $ftpBase.TrimEnd('/')

foreach ($file in $files) {
    $relative    = $file.FullName.Substring($resolvedPublish.Length).TrimStart('\', '/')
    $relativeFwd = $relative -replace '\\', '/'

    # Ensure every parent directory exists on the server
    $parts   = $relativeFwd -split '/'
    $dirPath = ""
    for ($i = 0; $i -lt ($parts.Count - 1); $i++) {
        $dirPath += "/" + $parts[$i]
        $dirUrl   = "$ftpBaseClean$dirPath"
        if (-not $createdDirs.ContainsKey($dirUrl)) {
            Ftp-EnsureDirectory $dirUrl
            $createdDirs[$dirUrl] = $true
        }
    }

    $remoteFileUrl = "$ftpBaseClean/$relativeFwd"

    try {
        Ftp-UploadFile $file.FullName $remoteFileUrl
        Write-Host "  [OK]   $relativeFwd" -ForegroundColor DarkGreen
        $uploadOk++
    } catch {
        Write-Host "  [FAIL] $relativeFwd -- $($_.Exception.Message)" -ForegroundColor Red
        $uploadFail++
    }
}

# -----------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "-----------------------------------------------" -ForegroundColor Cyan
if ($uploadFail -eq 0) {
    Write-Host " Deploy complete!  $uploadOk files uploaded, 0 failed." -ForegroundColor Green
} else {
    Write-Host " Deploy finished.  $uploadOk uploaded  |  $uploadFail FAILED." -ForegroundColor Yellow
    Write-Host " Re-run the script or upload the failed files manually." -ForegroundColor Yellow
}
Write-Host "-----------------------------------------------" -ForegroundColor Cyan
Write-Host ""

if ($uploadFail -gt 0) { exit 1 }
