# Lanceur de Build-Release.ps1 : clic droit > Executer avec PowerShell.
# Refait les captures du site et des stores, puis publie la release complete (Windows + Android)
# avec les nouvelles images des fiches Google Play et Microsoft Store. Si une capture a change, s'arrete
# avant de construire quoi que ce soit : commiter les captures par une PR, puis relancer
# Release-AvecImages.ps1 (inutile de les refaire une deuxieme fois).
# La fenetre reste ouverte a la fin pour lire le resultat.
& (Join-Path $PSScriptRoot "Build-Release.ps1") -Screenshots -UploadListingImages
