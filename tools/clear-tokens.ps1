$ErrorActionPreference = "SilentlyContinue"

# Default locations I used in the TokenStore/Google helper snippets:
$roots = @(
  "$env:LOCALAPPDATA\TripperApp\Auth",     # tokens.json (TokenStore)
  "$env:LOCALAPPDATA\TripperApp\GoogleAuth" # Google OAuth cache (optional)
) | Where-Object { Test-Path $_ }

$found = @()
foreach ($r in $roots) {
  $found += Get-ChildItem -Path $r -Recurse -File
}

# Fallback: search broadly for any *token* file under Tripper/TripPlanner cache folders
if ($found.Count -eq 0) {
  $broadRoots = @(
    "$env:LOCALAPPDATA\TripperApp",
    "$env:LOCALAPPDATA\TripPlanner",
    "$env:APPDATA\TripperApp",
    "$env:APPDATA\TripPlanner"
  ) | Where-Object { Test-Path $_ }
  foreach ($r in $broadRoots) {
    $found += Get-ChildItem -Path $r -Recurse -File -Include "*token*.*","tokens.json","auth.json"
  }
}

if ($found.Count -eq 0) {
  Write-Host "No token files found (that's OK before first login)." -ForegroundColor Yellow
  exit 0
}

$found | ForEach-Object {
  Write-Host "Deleting $($_.FullName)" -ForegroundColor Yellow
  Remove-Item $_.FullName -Force
}
Write-Host "Done. Start the WPF app again and you'll get the login dialog." -ForegroundColor Green
