Remove-Item publish -Recurse -Force
dotnet publish ToplingHelper --runtime win-x64   /p:PublishSingleFile=true --self-contained false  /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x64-lite
dotnet publish ToplingHelper --runtime win-x86   /p:PublishSingleFile=true --self-contained false  /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x86-lite
dotnet publish ToplingHelper --runtime win-x64   /p:PublishSingleFile=true --self-contained true   /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x64-full
dotnet publish ToplingHelper --runtime win-x86   /p:PublishSingleFile=true --self-contained true   /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x86-full
Move-Item publish/x64-lite/ToplingHelper.exe publish/ToplingHelper-lite-x64.exe
Move-Item publish/x86-lite/ToplingHelper.exe publish/ToplingHelper-lite-x86.exe
Move-Item publish/x64-full/ToplingHelper.exe publish/ToplingHelper-full-x64.exe
Move-Item publish/x86-full/ToplingHelper.exe publish/ToplingHelper-full-x86.exe

&"${env:ProgramFiles(x86)}\Microsoft SDKs\ClickOnce\SignTool\signtool.exe" sign /tr http://ts.ssl.com /sha1  652298E27FBDFDD0312360C93E9922DC05863299 /fd sha256 .\publish\*.exe

Get-ChildItem publish -Directory | Remove-Item -Recurse -Force

Get-ChildItem publish | Foreach-Object {   
    $output = "publish/" + $_.BaseName + ".zip";
    $input_ = "publish/" + $_.Name;
    zip -j1 $output $input_
}

explorer.exe ./publish
