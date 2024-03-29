#!/bin/bash

rm -rf publish
dotnet publish ToplingHelper.Ava -c release --runtime osx-x64 --self-contained true /p:PublishSingleFile=true  /p:EnableCompressionInSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:PublishReadyToRun=true -o publish

cat >> publish/start-toplinghelper.sh << EOF
#!/bin/bash
# 运行 bash start-toplinghelper.sh 启动程序
xattr -rd com.apple.quarantine ToplingHelper
chmod +x ./ToplingHelper
./ToplingHelper
EOF

cd publish
mv ToplingHelper.Ava ToplingHelper
zip -1r ToplingHelper-mac.zip ToplingHelper start-toplinghelper.sh
open .