# Define the directory containing the C# project files
$projectDirectory = "/Users/admin/Dev/Noctusoft/webapi-model/MultiEdmODataApi" # Replace with your project directory path
$outputFile = "/Users/admin/Dev/Noctusoft/ez-db-ef/Src/EzDbEf/api.proj.txt" # Replace with your desired output file path

# Define markers for each file content
$startMarker = "<<START_FILE>>"
$endMarker = "<<END_FILE>>"

# Define directories to ignore
$ignoredDirectories = @("bin", "obj", "Api")

# Initialize the output file
New-Item -Path $outputFile -ItemType File -Force | Out-Null

# Function to check if a file should be ignored
function Should-IgnoreFile {
    param (
        [string]$filePath
    )
    foreach ($dir in $ignoredDirectories) {
        if ($filePath -match "[\\/]$dir[\\/]") {
            return $true
        }
    }
    return $false
}

# Function to get all relevant project files
function Get-ProjectFiles {
    param (
        [string]$directory
    )
    # Get all .csproj, .cs, .config, and .json files (you can add more extensions as needed)
    return Get-ChildItem -Path $directory -Recurse -Include *.csproj, *.cs, *.config, *.json
}

# Loop through each project file and write its content to the output file
$projectFiles = Get-ProjectFiles -directory $projectDirectory

foreach ($file in $projectFiles) {
    $relativePath = $file.FullName.Substring($projectDirectory.Length).TrimStart('\', '/')
    
    # Check if the file should be ignored based on its path
    if (Should-IgnoreFile -filePath $file.FullName) {
        Write-Host "Ignoring file: $($file.FullName)" -ForegroundColor Yellow
        continue
    }

    Write-Host "Processing file: $($file.FullName)"
    
    # Read the content of the current file
    $fileContent = Get-Content -Path $file.FullName -Raw

    # Check if the file contains the ignore string
    if ($fileContent -match "PROJECT PACK IGNORE") {
        Write-Host "Ignoring file (content rule): $($file.FullName)" -ForegroundColor Yellow
        continue
    }
    
    # Add start marker and relative file path to the output file
    Add-Content -Path $outputFile -Value "$startMarker $relativePath"
    
    # Add file content to the output file
    Add-Content -Path $outputFile -Value $fileContent
    
    # Add end marker to the output file
    Add-Content -Path $outputFile -Value "$endMarker"
}

Write-Host "All files have been consolidated into: $outputFile"