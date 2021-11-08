Remove-Item publish -Recurse -Force
dotnet publish --runtime win-x64   /p:PublishSingleFile=true --self-contained false  /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x64-lite
dotnet publish --runtime win-x86   /p:PublishSingleFile=true --self-contained false  /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x86-lite
dotnet publish --runtime win-x64   /p:PublishSingleFile=true --self-contained true   /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x64-full
dotnet publish --runtime win-x86   /p:PublishSingleFile=true --self-contained true   /p:IncludeNativeLibrariesForSelfExtract=true  -c release -o publish/x86-full
mv publish/x64-lite/ToplingHelper.exe publish/ToplingHelper-lite-x64.exe
mv publish/x86-lite/ToplingHelper.exe publish/ToplingHelper-lite-x86.exe
mv publish/x64-full/ToplingHelper.exe publish/ToplingHelper-full-x64.exe
mv publish/x86-full/ToplingHelper.exe publish/ToplingHelper-full-x86.exe

Get-ChildItem publish -Directory | Remove-Item -Recurse -Force

Get-ChildItem publish | Foreach-Object {   
    $output = "publish/" + $_.BaseName + ".zip";
    $input = "publish/" + $_.Name;
    zip -j1 $output $input
}
