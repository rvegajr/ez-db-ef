# Define the input file and output directory
$inputFile = "/Users/admin/Dev/Noctusoft/ez-db-ef/Src/EzDbEf/api.proj.txt" # Replace with your input file path
$outputDirectory = "/Users/admin/Dev/Noctusoft/ez-db-ef/Src/artifaccts/UnpackedProject" # Replace with your desired output directory

# Define markers for each file content
$startMarker = "<<START_FILE>>"
$endMarker = "<<END_FILE>>"

# Delete everything in the output directory if it exists
if (Test-Path $outputDirectory) {
    Write-Host "Deleting existing content in output directory..."
    Remove-Item -Path $outputDirectory -Recurse -Force
}

# Create the output directory
New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null

# Read the input file line by line
$content = Get-Content -Path $inputFile

$currentFile = $null
$fileContent = @()

foreach ($line in $content) {
    if ($line.StartsWith($startMarker)) {
        # Start of a new file
        if ($currentFile) {
            # Write the previous file if exists
            $fileContent | Set-Content -Path $currentFile -Force
            Write-Host "Unpacked file: $currentFile"
        }
        
        # Get the relative path and create full path
        $relativePath = $line.Substring($startMarker.Length).Trim()
        $currentFile = Join-Path -Path $outputDirectory -ChildPath $relativePath
        
        # Create the directory for the file if it doesn't exist
        $directory = Split-Path -Path $currentFile -Parent
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
        
        # Reset file content
        $fileContent = @()
    }
    elseif ($line -eq $endMarker) {
        # End of the current file
        if ($currentFile) {
            $fileContent | Set-Content -Path $currentFile -Force
            Write-Host "Unpacked file: $currentFile"
            $currentFile = $null
        }
    }
    else {
        # File content
        if ($currentFile) {
            $fileContent += $line
        }
    }
}

# Write the last file if exists
if ($currentFile) {
    $fileContent | Set-Content -Path $currentFile -Force
    Write-Host "Unpacked file: $currentFile"
}

Write-Host "All files have been unpacked to: $outputDirectory"