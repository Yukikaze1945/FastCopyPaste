<p align="center">
  <img src="assets/fastcopy-paste-logo.png" width="180" alt="FastCopy Paste Logo">
</p>

<h1 align="center">FastCopy Paste</h1>

<p align="center">
  <a href="https://github.com/Yukikaze1945/FastCopyPaste/releases/latest"><img src="https://img.shields.io/badge/release-20260723-0078D4" alt="Release 20260723"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2EA44F" alt="MIT License"></a>
</p>

<p align="center">
  <a href="README.md">简体中文</a> |
  <a href="README.en.md">English</a> |
  <a href="README.ja.md">日本語</a>
</p>

FastCopy Paste 是一个面向 64 位 Windows 10/11 的资源管理器集成工具。它在文件夹和文件夹空白处加入“FastCopy 粘贴到这里”菜单，并可在资源管理器文件列表中把文件剪贴板的 `Ctrl+V` 交给 FastCopy。

> 本项目不是 FastCopy 官方项目，不包含或重新分发 FastCopy。使用前请自行从 [FastCopy 官网](https://fastcopy.jp/) 获取 FastCopy，并遵守其许可条款。

## 功能

- 保留原生 `Ctrl+C`、`Ctrl+X` 和 Windows 文件剪贴板；默认接管 Explorer 文件列表中的 `Ctrl+V`。
- 可在托盘中录制任意替换快捷键；更改后原来的 `Ctrl+V` 会完全交回 Explorer。
- 在 Windows 11 提供现代第一层菜单，在 Windows 10 提供传统右键菜单项。
- 复制使用 FastCopy `diff`，剪切使用 `move`，显示 FastCopy 原生进度窗口。
- 支持多选、中文、空格和长路径，任务按顺序排队。
- 冲突默认取消；用户确认后才合并并覆盖。
- 防止盘符根目录源、目标等于源，以及把目录粘贴到自身或子目录。
- 剪切成功且剪贴板未被更新时才清空旧剪贴板。
- 托盘菜单可暂停接管、录制快捷键、修改 FastCopy 路径、打开日志或退出。

## 运行环境

- 64 位 Windows 10 2004 / Build 19041 或更新版本，以及 64 位 Windows 11。
- 64 位 FastCopy；已使用 FastCopy 5.11.3 验证。
- 安装资源管理器右键菜单需要 Windows“开发者模式”允许松散包注册。
- 仅为当前 Windows 用户安装，不需要管理员权限。
- Release ZIP 是自包含 x64 构建，使用者不需要另外安装 .NET Runtime。

本项目不支持 Windows 10 Build 19041 以下版本、32 位系统、ZIP/MTP/回收站等虚拟目录，也不会替换 Explorer 内置鼠标“粘贴”按钮。

## 安装

1. 在 Windows 设置中打开“系统 → 高级 → 开发者选项 → 开发人员模式”。
2. 从 GitHub Releases 下载 `FastCopyPaste-current-user.zip`，先解压完整 ZIP。
3. 在解压目录打开 PowerShell，运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1
```

安装器会从 `PATH`、注册表以及常见安装目录寻找 `FastCopy.exe`；找不到时会弹出文件选择框。也可以明确指定便携版路径：

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

程序安装到 `%LOCALAPPDATA%\Programs\FastCopyPaste`，配置与日志保存到 `%LOCALAPPDATA%\FastCopyPaste`。安装脚本会注册当前用户稀疏包、登录启动项，并启动托盘 Host；重复运行可安全覆盖安装。

如果 Windows 阻止从互联网下载的脚本，可先右键 ZIP →“属性”→“解除锁定”再解压。发布二进制目前没有代码签名，因此 Windows 可能显示未知发布者提示；请只从本仓库的 Releases 下载，并可自行核对 SHA-256。

## 使用

1. 在资源管理器中用原生 `Ctrl+C` 或 `Ctrl+X` 选择文件。
2. 进入普通文件系统目录。
3. 在文件列表按当前快捷键（默认为 `Ctrl+V`），或右键选择“FastCopy 粘贴到这里”。

地址栏、搜索框、其他应用、虚拟目录、非文件剪贴板和暂停状态不会被接管。同目录复制会交回 Explorer。FastCopy 返回退出码 `0` 才视为成功；失败时源文件和剪贴板会保留。

## 配置与日志

配置文件位于 `%LOCALAPPDATA%\FastCopyPaste\settings.json`：

```json
{
  "fastCopyPath": "D:\\Tools\\FastCopy\\FastCopy.exe",
  "hookEnabled": true,
  "hotkey": {
    "virtualKey": 86,
    "modifiers": 1
  }
}
```

推荐通过托盘菜单录制快捷键、修改 FastCopy 路径或暂停接管。快捷键允许任意非修饰键以及任意 `Ctrl` / `Alt` / `Shift` / `Win` 组合；只有单独的修饰键和 Windows 不会交给普通程序的安全组合无法使用。日志位于 `%LOCALAPPDATA%\FastCopyPaste\Logs`，不会上传到网络。

## 卸载

从解压后的发布目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

卸载会移除当前用户的包注册、启动项、安装目录及本工具配置/日志，不会删除或修改 FastCopy。

## 从源码构建

构建环境需要 .NET 8 或更新的 SDK、Visual Studio 2022 C++ Build Tools（v143）以及 Windows 11 SDK：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

脚本会先运行单元测试，再构建 .NET Host 和原生 Shell DLL，最终生成 `artifacts\FastCopyPaste-current-user.zip`。发布包不包含 PDB、FastCopy 或用户配置。

可单独运行测试：

```powershell
dotnet run --project .\tests\FastCopyPaste.Tests\FastCopyPaste.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Test-Integration.ps1 `
  -FastCopyPath "D:\Tools\FastCopy\FastCopy.exe"
```

集成测试会复制 FastCopy 命令行程序到临时目录，避免改动正常使用的 FastCopy 配置。

## 安全与隐私

- Shell DLL 只解析目标目录并通过命名管道通知 Host，不在 Explorer 进程内执行复制。
- FastCopy 参数通过 `ProcessStartInfo.ArgumentList` 传递，不拼接 Shell 命令。
- 所有复制任务在本机完成；本项目没有遥测、联网或自动更新功能。
- FastCopy 本身可能按其设置写入日志和历史记录，本项目不会清理这些内容。

## 已知限制

- 已在 x64 Windows 11 与 FastCopy 5.11.3 上验证；Windows 10 兼容清单和安装路径已经启用，但仍需实机验收。
- 其他 FastCopy 5.x 版本理论上兼容，但尚未逐一验证。
- 未签名的稀疏包要求开发者模式，并可能触发 SmartScreen 或 PowerShell 来源提示。
- Windows 更新可能改变现代右键菜单或 Explorer 焦点结构；遇到问题请附上 Host 日志提交 Issue。
- 当前版本的程序界面以简体中文为主；项目文档提供简体中文、英文和日文版本。

## 许可证

本项目代码采用 [MIT License](LICENSE) 开源。FastCopy 是独立的第三方软件，不包含在本许可证或发布包中。
