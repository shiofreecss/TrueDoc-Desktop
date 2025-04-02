# Script to download and install PdfiumViewer native dependencies
# This script will detect the system architecture and download the appropriate version of Pdfium

# Set error action preference to stop on any error
$ErrorActionPreference = "Stop"

# Output function to provide feedback
function Write-Status {
    param ([string]$message)
    Write-Output $message
}

# Determine if the system is 64-bit or 32-bit
Write-Status "Detecting system architecture..."
$is64Bit = [Environment]::Is64BitOperatingSystem -and [Environment]::Is64BitProcess

# Set the download URL based on architecture
if ($is64Bit) {
    $downloadUrl = "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/5845/pdfium-windows-x64.zip"
    Write-Status "Detected 64-bit system. Will download 64-bit binaries."
} else {
    $downloadUrl = "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/5845/pdfium-windows-x86.zip"
    Write-Status "Detected 32-bit system. Will download 32-bit binaries."
}

# Create temp folders
$tempFolder = Join-Path $env:TEMP "pdfium_temp"
$downloadPath = Join-Path $tempFolder "pdfium.zip"

# Create temp directory if it doesn't exist
if (Test-Path $tempFolder) {
    try {
        Remove-Item -Path $tempFolder -Recurse -Force
    } catch {
        Write-Status "Could not remove existing temp folder. Will try to continue."
    }
}
New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

# Download the file
try {
    Write-Status "Downloading Pdfium binaries..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath -UseBasicParsing
    Write-Status "Download completed successfully."
} catch {
    Write-Status "Download failed: $_"
    exit 1
}

# Extract the ZIP file
try {
    Write-Status "Extracting files..."
    Expand-Archive -Path $downloadPath -DestinationPath $tempFolder -Force
    Write-Status "Extraction completed."
} catch {
    Write-Status "Extraction failed: $_"
    exit 1
}

# Determine the application directory, libraries directory and bin directory
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $appDir "Libraries"
$binDir = $appDir

# Create Libraries directory if it doesn't exist
if (-not (Test-Path $libDir)) {
    New-Item -ItemType Directory -Path $libDir -Force | Out-Null
    Write-Status "Created Libraries directory."
}

# Find the pdfium.dll file in the extracted folder
Write-Status "Locating pdfium.dll..."
$pdfiumDll = Get-ChildItem -Path $tempFolder -Recurse -Filter "pdfium.dll" | Select-Object -First 1

if ($pdfiumDll) {
    Write-Status "Found pdfium.dll in $($pdfiumDll.DirectoryName)"
    $dllDir = $pdfiumDll.DirectoryName
    
    # Copy all DLL files to the Libraries folder
    Write-Status "Copying DLL files to Libraries folder..."
    foreach ($dllFile in (Get-ChildItem -Path $dllDir -Filter "*.dll")) {
        $targetLibPath = Join-Path $libDir $dllFile.Name
        Copy-Item -Path $dllFile.FullName -Destination $targetLibPath -Force
        Write-Status "Copied $($dllFile.Name) to Libraries folder."
    }
    
    # Also copy all DLL files to the bin folder for backward compatibility
    Write-Status "Copying DLL files to application directory..."
    foreach ($dllFile in (Get-ChildItem -Path $dllDir -Filter "*.dll")) {
        $targetBinPath = Join-Path $binDir $dllFile.Name
        Copy-Item -Path $dllFile.FullName -Destination $targetBinPath -Force
        Write-Status "Copied $($dllFile.Name) to application directory."
    }
    
    # Clean up
    try {
        Write-Status "Cleaning up temporary files..."
        Remove-Item -Path $tempFolder -Recurse -Force
        Write-Status "Installation completed successfully!"
    } catch {
        Write-Status "Note: Could not remove temporary files, but installation was successful."
    }
    
    exit 0
} else {
    Write-Status "Error: Could not find pdfium.dll in the downloaded package."
    exit 1
} 