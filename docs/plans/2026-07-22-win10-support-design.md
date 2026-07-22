# Windows 10 x64 兼容设计

## 范围

兼容目标为 64 位 Windows 10 2004（Build 19041）及以上版本，同时保持 Windows 11 行为不变。不支持 Build 19041 以下版本或 32 位 Windows，也不新增传统注册表 COM 安装路径。

## 方案

继续复用现有的 .NET 8 自包含 Host、x64 `IExplorerCommand` DLL 和稀疏包安装架构。包清单的最低版本降为 `10.0.19041.0`，这是外部位置稀疏包可用的最低 Windows 构建。Host 使用 Windows 10/11 共用的应用兼容 GUID。安装器在修改包注册、启动项或安装目录之前检查 64 位系统和最低构建号。

Windows 11 继续显示现代第一层菜单；Windows 10 使用其传统资源管理器菜单呈现同一 `IExplorerCommand`。剪贴板、命名管道、FastCopy 参数和安全策略不随系统版本分叉。

## 验证边界

自动验证包括单元测试、原生 DLL 构建、包清单验证、PowerShell 语法、Win11 安装回归以及真实 FastCopy 复制/剪切流程。Windows 10 实机菜单和快捷键验收由测试者在目标机器完成；在完成前，文档明确标记为尚未实机验证。
