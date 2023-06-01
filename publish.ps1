$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
chcp 65001

$fileX86 = (dotnet publish ToplingHelperMaui -f net6.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x86 | Select-Object -Last 1).Split()[-1]

$fileX64 = (dotnet publish ToplingHelperMaui -f net6.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64 | Select-Object -Last 1).Split()[-1]
Remove-Item ./*.msix
move-Item $fileX86 ToplingHelperMaui-x86.msix
move-Item $fileX64 ToplingHelperMaui-x64.msix

$signtoolPath = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\signtool.exe"
& $signtoolPath sign /tr http://ts.ssl.com /sha1  652298E27FBDFDD0312360C93E9922DC05863299 /fd sha256 *.msix