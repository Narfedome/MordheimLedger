# Lanceur de Build-Release.ps1 : clic droit > Executer avec PowerShell.
# Publie la release complete (Windows + Android) et remplace les images des fiches Google Play et
# Microsoft Store par celles de StoreListing\output\, sans refaire les captures.
# La fenetre reste ouverte a la fin pour lire le resultat.
& (Join-Path $PSScriptRoot "Build-Release.ps1") -UploadListingImages
