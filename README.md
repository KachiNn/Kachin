# Kachin Cleaner

Kachin Cleaner 是一款 Windows 桌面清理应用。以下实现范围依据当前源代码核对；GUI 尚未进行交互式运行验证，因此不代表已完成运行时测试。

## 清理范围与安全

- 扫描范围仅为当前用户的 `%LOCALAPPDATA%\Temp`（即 `Environment.SpecialFolder.LocalApplicationData` 下的 `Temp`），且该路径必须位于 `C:`；其他位置不会扫描。程序不读取 `TEMP` 环境变量来决定扫描路径。
- 不遍历根路径链或其子项中的重解析点；遇到无法访问或正在使用的目录/文件时跳过并记录问题。
- 扫描结果按组显示，预览包含文件数和总大小；用户选择要清理的组后，还必须在删除确认对话框中明确确认。取消确认不会删除。
- 个人文件夹、回收站、Windows 系统目录和浏览器数据不在清理目标中。删除时会再次检查路径仍位于允许范围内，并拒绝目录或重解析点。
- 上述扫描与删除约束是代码实现事实，不等同于 GUI 已经交互测试通过。本文不声称已验证管理员权限要求、遥测行为或网络访问行为。

**安全提示：**清理操作可能造成数据丢失。确认前请仔细审核所选组及其内容。

## 环境要求

- Windows 10 或更新版本。
- .NET 10 SDK；项目目标框架为 `net10.0-windows`。可运行 `dotnet --version` 检查 SDK。

## 构建与运行

在仓库根目录的 PowerShell 或命令提示符中执行：

```powershell
dotnet restore .\KachinCleaner\KachinCleaner.csproj
dotnet build .\KachinCleaner\KachinCleaner.csproj --no-restore -f net10.0-windows
dotnet run --project .\KachinCleaner\KachinCleaner.csproj -f net10.0-windows
```

这些是源代码构建和运行命令，不是发布或安装命令。Kachin Cleaner 不是已签名安装程序；仓库不承诺提供预构建的 EXE。GUI 尚未交互式运行验证。

## 故障排查

- `dotnet` 命令无法识别：安装 .NET 10 SDK，并重新打开终端后运行 `dotnet --version`。
- 找不到项目文件：确认当前目录为仓库根目录，且项目文件位于 `KachinCleaner\KachinCleaner.csproj`。
- 提示目标框架不可用：确认安装的是 .NET 10 SDK，并核对项目目标框架为 `net10.0-windows`。
- 构建错误：保留完整错误输出，并确认 SDK 版本；README 不声称 GUI 已通过运行时测试。
