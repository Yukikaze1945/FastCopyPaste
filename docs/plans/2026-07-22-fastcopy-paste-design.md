# FastCopy 菜单粘贴与 Ctrl+V 接管设计

本项目由三个部分组成：.NET 8 WinForms 常驻 Host、原生 x64 `IExplorerCommand` Shell 扩展，以及当前用户安装脚本。Host 只在资源管理器文件列表中接管带文件剪贴板的 `Ctrl+V`；Shell 扩展在 Windows 11 第一层菜单和 Windows 10 传统菜单提供“FastCopy 粘贴到这里”。实际复制或移动始终交给用户配置的现有 `FastCopy.exe`。

复制使用 `diff`，移动使用 `move`。启动前验证源和目标路径，拒绝盘符根目录及递归粘贴；目标存在同名顶层项目时先确认。FastCopy 成功退出后才把剪切剪贴板视为完成，而且只在剪贴板序列号未变化时清空。非文件剪贴板、虚拟 Shell 位置、地址栏与搜索框均保持 Windows 原生行为。

发布包安装到 `%LOCALAPPDATA%\Programs\FastCopyPaste`，配置和日志存放在 `%LOCALAPPDATA%\FastCopyPaste`。稀疏包只为现代菜单提供应用身份；安装、启动和卸载均限制在当前用户范围，不修改或捆绑 FastCopy。
