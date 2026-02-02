param(
    [Parameter(Mandatory = $true)]
    [string[]]$SourceFolders,
    [Parameter(Mandatory = $true)]
    [string]$TargetFolder,
    [ValidateSet("Error", "Info", "Debug")]
    [string]$LogLevel = "Info"
)

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runFolder = Join-Path $TargetFolder $timestamp
$logFolder = Join-Path $TargetFolder "logs"
$logFile = Join-Path $logFolder "backup_$timestamp.log"

New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
New-Item -ItemType Directory -Path $logFolder -Force | Out-Null

function Write-Log {
    param(
        [string]$Level,
        [string]$Message
    )

    $levels = @{ "Error" = 0; "Info" = 1; "Debug" = 2 }
    if ($levels[$Level] -gt $levels[$LogLevel]) {
        return
    }

    $line = "$(Get-Date -Format o) [$Level] $Message"
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

Write-Log -Level "Info" -Message "Backup started."

foreach ($source in $SourceFolders) {
    if (-not (Test-Path $source)) {
        Write-Log -Level "Info" -Message "Source folder does not exist: $source"
        continue
    }

    Write-Log -Level "Info" -Message "Processing source folder: $source"
    Get-ChildItem -Path $source -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        $destination = Join-Path $runFolder $relative
        $destinationDir = Split-Path $destination -Parent
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null

        try {
            Copy-Item -Path $_.FullName -Destination $destination -Force -ErrorAction Stop
            Write-Log -Level "Debug" -Message "Copied: $($_.FullName) -> $destination"
        } catch {
            Write-Log -Level "Info" -Message "Skipped file due to error: $($_.FullName). Reason: $($_.Exception.Message)"
        }
    }
}

Write-Log -Level "Info" -Message "Backup completed."
