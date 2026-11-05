# Kachin Cleaner

Windows 临时文件清理工具。扫描后先查看文件数量和占用空间，勾选需要清理的分组并确认后才会删除。

## 下载运行

下载 [KachinCleaner.exe](dist/KachinCleaner.exe)，在 Windows 10/11 x64 上双击运行。程序为自包含版本，不需要单独安装 .NET。由于程序未签名，Windows 可能会显示 SmartScreen 提示。

## 清理范围

程序只扫描 C 盘当前用户的 LocalAppData\Temp 目录。不会扫描个人文件夹、回收站、Windows 系统目录或浏览器数据；遇到正在使用或无法访问的文件会跳过。扫描本身不会修改文件，删除前会再次要求确认。

## 从源码运行

需要 .NET 10 SDK。在仓库根目录执行：

~~~powershell
dotnet run --project .\KachinCleaner\KachinCleaner.csproj
~~~

重新生成 Windows x64 自包含 EXE：

~~~powershell
dotnet publish .\KachinCleaner\KachinCleaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist\publish
Copy-Item .\dist\publish\KachinCleaner.exe .\dist\KachinCleaner.exe -Force
~~~
