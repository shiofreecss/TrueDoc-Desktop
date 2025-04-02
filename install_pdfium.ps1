$downloadUrl = "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/5845/pdfium-windows-x64.zip"
$tempFolder = Join-Path $env:TEMP "pdfium_temp"
$downloadPath = Join-Path $tempFolder "pdfium.zip"

Write-Output "Downloading Pdfium binaries..."

# Create temp directory if it doesn't exist
if (Test-Path $tempFolder) {
    Remove-Item -Path $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

# Download the file
Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath -UseBasicParsing

Write-Output "Extracting files..."
# Extract the ZIP file
Expand-Archive -Path $downloadPath -DestinationPath $tempFolder -Force

# Find the pdfium.dll file in the extracted folder
$pdfiumDll = Get-ChildItem -Path $tempFolder -Recurse -Filter "pdfium.dll" | Select-Object -First 1

if ($pdfiumDll) {
    $dllDir = $pdfiumDll.DirectoryName
    
    Write-Output "Found pdfium.dll in $dllDir"
    
    # Copy all DLL files to the application directory
    $targetDir = Join-Path (Get-Location) "TrueDocDesktop.App\bin\Debug\net8.0-windows"
    Write-Output "Copying files to $targetDir"
    
    if (!(Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    
    foreach ($dllFile in (Get-ChildItem -Path $dllDir -Filter "*.dll")) {
        $targetPath = Join-Path $targetDir $dllFile.Name
        Copy-Item -Path $dllFile.FullName -Destination $targetPath -Force
        Write-Output "Copied $($dllFile.Name) to application directory."
    }
    
    # Make Libraries directory
    $libDir = Join-Path $targetDir "Libraries"
    if (!(Test-Path $libDir)) {
        New-Item -ItemType Directory -Path $libDir -Force | Out-Null
    }
    
    # Copy all DLL files to Libraries folder too
    foreach ($dllFile in (Get-ChildItem -Path $dllDir -Filter "*.dll")) {
        $targetLibPath = Join-Path $libDir $dllFile.Name
        Copy-Item -Path $dllFile.FullName -Destination $targetLibPath -Force
        Write-Output "Copied $($dllFile.Name) to Libraries folder."
    }
    
    # Clean up
    Remove-Item -Path $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Output "Installation completed successfully!"
} else {
    Write-Output "Error: Could not find pdfium.dll in the downloaded package."
} 