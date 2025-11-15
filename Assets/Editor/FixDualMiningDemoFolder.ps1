# PowerShell script to fix the DualMiningDemo.unity folder issue
# Run this from PowerShell (not as a Unity menu item)

$folderPath = "Assets\Scenes\Demo\DualMiningDemo.unity"
$fullPath = Join-Puth $PSScriptRoot ".." ".." $folderPath | Resolve-Path -ErrorAction SilentlyContinue

if ($fullPath -and (Test-Path $fullPath)) {
    $item = Get-Item $fullPath
    if ($item.PSIsContainer) {
        Write-Host "Found DualMiningDemo.unity as a DIRECTORY - deleting it..."
        try {
            Remove-Item -Path $fullPath -Recurse -Force
            Write-Host "✓ Successfully deleted the folder!"
            Write-Host "Now you can run the setup menu item in Unity."
        }
        catch {
            Write-Host "✗ Error: $_"
            Write-Host "You may need to run this script as Administrator."
            Write-Host "Or manually delete: $fullPath"
        }
    }
    else {
        Write-Host "DualMiningDemo.unity exists as a file (this is correct)."
    }
}
else {
    Write-Host "DualMiningDemo.unity does not exist."
}



























