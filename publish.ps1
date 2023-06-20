Remove-Item publish -Recurse -Force -ErrorAction SilentlyContinue

$common_args = "ToplingHelper.Ava -c release --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true"

Invoke-Expression "dotnet publish $common_args --runtime win-x86 -o publish/x86"
Invoke-Expression "dotnet publish $common_args --runtime win-x64 -o publish/x64"

Move-Item publish/x64/ToplingHelper.Ava.exe publish/ToplingHelper-x64.exe
Move-Item publish/x86/ToplingHelper.Ava.exe publish/ToplingHelper-x86.exe


$signtoolPath = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\signtool.exe"
& $signtoolPath sign /tr http://ts.ssl.com /sha1  652298E27FBDFDD0312360C93E9922DC05863299 /td sha1 /fd sha256 .\publish\*.exe
Get-ChildItem publish -Directory | Remove-Item -Recurse -Force

Get-ChildItem publish | Foreach-Object {   
    $output = "publish/" + $_.BaseName + ".zip";
    $input_ = "publish/" + $_.Name;
    zip -j1 $output $input_
}
explorer.exe publish