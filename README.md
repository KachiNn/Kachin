# Kachin Cleaner

Kachin Cleaner 用于查看并清理临时文件。界面支持中文和 English，默认显示中文。

## 下载

下载 [dist/KachinCleaner.exe](dist/KachinCleaner.exe)，在 Windows 10/11 x64 上运行。该版本为自包含的 win-x64 程序，无需单独安装 .NET。程序未签名时，Windows SmartScreen 可能会显示提示。

## 选择与清理

默认扫描当前用户的 C:\Users\<用户名>\AppData\Local\Temp。也可以选择任意驱动器上的普通文件夹。扫描结果会显示完整文件路径；文件默认不勾选，请先选中要删除的文件，再确认清理。

扫描会递归查找文件，但跳过重解析点，不跟随目录联接等链接。为避免误操作，驱动器根目录、Windows、Program Files 和用户配置文件根目录不能作为清理目标。清理只删除确认时选中的文件，不会删除所选文件夹本身。

## 从源码运行

需要 .NET 10 SDK。在仓库根目录运行：

~~~powershell
dotnet run --project .\KachinCleaner\KachinCleaner.csproj
~~~

## 发布

在仓库根目录发布自包含的 win-x64 单文件程序，并复制到下载位置：

~~~powershell
dotnet publish .\KachinCleaner\KachinCleaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist\publish
Copy-Item .\dist\publish\KachinCleaner.exe .\dist\KachinCleaner.exe -Force
~~~
