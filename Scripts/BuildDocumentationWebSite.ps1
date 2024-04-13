# Install .NET 6 SDK using winget (uncomment if needed)
# winget install Microsoft.DotNet.SDK.6 --silent

Write-Output "Building documentation using MkDocs"
mkdocs build -f docs/mkdocs.yml

Write-Output "Installing DocFX"
dotnet tool update -g docfx --version 2.75.3

Write-Output "Generating website in docs\api\_site folder"
docfx docs\api\docfx.json

Write-Output "Deleting existing 'website'' folder and contents"
Remove-Item -Path "website" -Recurse -Force -ErrorAction SilentlyContinue

Write-Output "Creating a new 'website' folder"
New-Item -ItemType Directory -Path "website"

Write-Output "Copying CNAME file to 'website'. This is necessary for the mapsui.com domain"
Copy-Item -Path "docs\CNAME" -Destination "website" -Force

Write-Output "Copying '_site' to 'website''"
Copy-Item -Path "docs\_site\*" -Destination "website" -Recurse -Force
