# ============================================================
#  PicoERP -- Windows Plesk / IIS Deploy Script
#  Builds a framework-dependent publish, then:
#    1. Uploads the under-construction page
#    2. Wipes everything on the server
#    3. Uploads the full new build
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
    [string]$FtpUser     = "erpadmin",
    [string]$FtpPassword = '@Karim50106735',
    # Writable sub-path under the FTP home (the site lives at /publish/)
    [string]$RemotePath  = "/publish/",
    [string]$PublishDir  = ".\publish_output",
    [string]$MaintenanceDir = ".\maintenance",
    # Skip dotnet publish and re-use the existing publish_output folder.
    [bool]$SkipBuild     = $false,
    # Set to $true to publish as self-contained (no runtime needed on server).
    [bool]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

$cred        = New-Object System.Net.NetworkCredential($FtpUser, $FtpPassword)
$ftpBase     = "ftp://$FtpHost$RemotePath".TrimEnd('/')

# -----------------------------------------------------------------------
# Helper: ensure a remote directory exists (ignore 550 = already exists)
# -----------------------------------------------------------------------
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
        if ($_.Exception.Message -notmatch "550") {
            Write-Warning "MkDir warning [$dirUrl]: $($_.Exception.Message)"
        }
    }
}

# -----------------------------------------------------------------------
# Helper: upload a single file
# -----------------------------------------------------------------------
function Ftp-UploadFile {
    param([string]$localPath, [string]$remoteUrl)
    $req               = [System.Net.FtpWebRequest]::Create($remoteUrl)
    $req.Credentials   = $cred
    $req.Method        = [System.Net.WebRequestMethods+Ftp]::UploadFile
    $req.UsePassive    = $true
    $req.UseBinary     = $true
    $req.KeepAlive     = $false
    $fileBytes         = [System.IO.File]::ReadAllBytes($localPath)
    $req.ContentLength = $fileBytes.Length
    $stream = $req.GetRequestStream()
    $stream.Write($fileBytes, 0, $fileBytes.Length)
    $stream.Close()
    $resp = $req.GetResponse()
    $resp.Close()
}

# -----------------------------------------------------------------------
# Helper: list all files in a remote directory (recursive via NLST)
# -----------------------------------------------------------------------
function Ftp-ListFiles {
    param([string]$dirUrl)
    $results = @()
    try {
        $req             = [System.Net.FtpWebRequest]::Create($dirUrl)
        $req.Credentials = $cred
        $req.Method      = [System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails
        $req.UsePassive  = $true
        $req.UseBinary   = $false
        $req.KeepAlive   = $false
        $resp   = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $lines  = $reader.ReadToEnd() -split "`n" | Where-Object { $_.Trim() -ne "" }
        $reader.Close(); $resp.Close()
        foreach ($line in $lines) {
            $parts = $line.Trim() -split '\s+', 9
            if ($parts.Count -ge 9) {
                $name = $parts[8].Trim()
                if ($name -notin @(".", "..")) { $results += $name }
            }
        }
    } catch { }
    return $results
}

# -----------------------------------------------------------------------
# Helper: delete a remote file
# -----------------------------------------------------------------------
function Ftp-DeleteFile {
    param([string]$fileUrl)
    try {
        $req             = [System.Net.FtpWebRequest]::Create($fileUrl)
        $req.Credentials = $cred
        $req.Method      = [System.Net.WebRequestMethods+Ftp]::DeleteFile
        $req.UsePassive  = $true
        $req.KeepAlive   = $false
        $resp = $req.GetResponse()
        $resp.Close()
    } catch {
        Write-Warning "Delete warning [$fileUrl]: $($_.Exception.Message)"
    }
}

# -----------------------------------------------------------------------
# Helper: remove a remote directory
# -----------------------------------------------------------------------
function Ftp-RemoveDirectory {
    param([string]$dirUrl)
    try {
        $req             = [System.Net.FtpWebRequest]::Create($dirUrl)
        $req.Credentials = $cred
        $req.Method      = [System.Net.WebRequestMethods+Ftp]::RemoveDirectory
        $req.UsePassive  = $true
        $req.KeepAlive   = $false
        $resp = $req.GetResponse()
        $resp.Close()
    } catch {
        Write-Warning "RmDir warning [$dirUrl]: $($_.Exception.Message)"
    }
}

# -----------------------------------------------------------------------
# Helper: recursively wipe all contents of a remote directory
# -----------------------------------------------------------------------
function Ftp-WipeDirectory {
    param([string]$dirUrl)
    $entries = Ftp-ListFiles $dirUrl
    foreach ($entry in $entries) {
        $entryUrl = "$($dirUrl.TrimEnd('/'))/$entry"
        # Try to list as a directory; if it succeeds it's a folder
        $subEntries = Ftp-ListFiles $entryUrl
        if ($subEntries -ne $null) {
            Ftp-WipeDirectory $entryUrl
            Ftp-RemoveDirectory $entryUrl
        } else {
            Ftp-DeleteFile $entryUrl
        }
    }
}

# -----------------------------------------------------------------------
# Helper: upload a local folder tree
# -----------------------------------------------------------------------
function Ftp-UploadDir {
    param([string]$localDir, [string]$remoteBase)
    $resolved = (Resolve-Path $localDir).Path
    $files    = Get-ChildItem -Path $resolved -Recurse -File
    $ok = 0; $fail = 0
    $createdDirs = @{}
    foreach ($file in $files) {
        $relative    = $file.FullName.Substring($resolved.Length).TrimStart('\', '/')
        $relativeFwd = $relative -replace '\\', '/'
        # Ensure parent dirs exist
        $parts   = $relativeFwd -split '/'
        $dirPath = ""
        for ($i = 0; $i -lt ($parts.Count - 1); $i++) {
            $dirPath += "/" + $parts[$i]
            $dirUrl   = "$($remoteBase.TrimEnd('/'))$dirPath"
            if (-not $createdDirs.ContainsKey($dirUrl)) {
                Ftp-EnsureDirectory $dirUrl
                $createdDirs[$dirUrl] = $true
            }
        }
        $remoteFileUrl = "$($remoteBase.TrimEnd('/'))/$relativeFwd"
        try {
            Ftp-UploadFile $file.FullName $remoteFileUrl
            Write-Host "  [OK]   $relativeFwd" -ForegroundColor DarkGreen
            $ok++
        } catch {
            Write-Host "  [FAIL] $relativeFwd -- $($_.Exception.Message)" -ForegroundColor Red
            $fail++
        }
    }
    return @{ Ok = $ok; Fail = $fail }
}

# ═══════════════════════════════════════════════════════════════════════
# 1. Build & Publish
# ═══════════════════════════════════════════════════════════════════════
Write-Host ""
if ($SkipBuild) {
    Write-Host "[1/4] Skipping publish (using existing $PublishDir)." -ForegroundColor Yellow
} else {
    Write-Host "[1/4] Publishing PicoERP.Web (Release, win-x64)..." -ForegroundColor Cyan
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    $publishArgs = @(
        "publish", "src\PicoERP.Web\PicoERP.Web.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "-o", $PublishDir,
        "/p:UseAppHost=true"
    )
    if ($SelfContained) { $publishArgs += "--self-contained", "true" }
    else                { $publishArgs += "--self-contained", "false" }
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed. Aborting."; exit 1 }
    Write-Host "[1/4] Publish complete." -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════════════
# 2. Upload under-construction page
# ═══════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "[2/4] Uploading under-construction page to ftp://$FtpHost$RemotePath ..." -ForegroundColor Cyan
$step2 = Ftp-UploadDir $MaintenanceDir $ftpBase
Write-Host "[2/4] Maintenance page live ($($step2.Ok) file(s))." -ForegroundColor Green

# ═══════════════════════════════════════════════════════════════════════
# 3. Wipe the server (clean-slate)
# ═══════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "[3/4] Wiping remote directory $ftpBase ..." -ForegroundColor Yellow
Ftp-WipeDirectory $ftpBase
Write-Host "[3/4] Remote directory wiped." -ForegroundColor Green

# ═══════════════════════════════════════════════════════════════════════
# 4. Upload the new build
# ═══════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "[4/4] Uploading new build from $PublishDir ..." -ForegroundColor Cyan
$step4 = Ftp-UploadDir $PublishDir $ftpBase

# ─── Summary ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "-----------------------------------------------" -ForegroundColor Cyan
if ($step4.Fail -eq 0) {
    Write-Host " Deploy complete!  $($step4.Ok) files uploaded, 0 failed." -ForegroundColor Green
} else {
    Write-Host " Deploy finished.  $($step4.Ok) uploaded  |  $($step4.Fail) FAILED." -ForegroundColor Yellow
    Write-Host " Re-run the script or upload the failed files manually." -ForegroundColor Yellow
}
Write-Host "-----------------------------------------------" -ForegroundColor Cyan
Write-Host ""

if ($step4.Fail -gt 0) { exit 1 }
