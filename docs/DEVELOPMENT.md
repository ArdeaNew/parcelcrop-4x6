# 开发指南

## 工具链

- Windows x64。
- .NET SDK 10。
- .NET Framework 4.8 Developer Pack / Targeting Pack（运行时本身不包含编译用引用程序集）。
- PowerShell，用于仓库脚本；Git 用于版本管理。
- 真实 PDF 集成测试另需可信来源的 Windows `mutool.exe`。

应用目标框架为 `net48`，输出类型为 WinForms `WinExe`，平台为 `x64`；现代 .NET SDK 用于编译，运行时仍为 .NET Framework。项目使用系统引用程序集，无外部 NuGet 测试框架。构建启用确定性编译，并将编译警告视为错误；这不等于不同编译器版本必然得到逐字节相同的发行包。

## 常用命令

在仓库根目录执行：

```powershell
./scripts/build.ps1 -Configuration Release
./scripts/test.ps1
./scripts/test.ps1 -RendererPath 'C:\Tools\MuPDF\mutool.exe'
./scripts/package.ps1 -Version 2.2.0
```

也可直接构建应用：

```powershell
dotnet build ./src/ParcelCrop/ParcelCrop.csproj -c Release
```

默认测试验证核心合同，不要求外部渲染器；带 `RendererPath` 的测试增加真实 PDF 路径。测试程序以非零退出码报告失败，脚本应传播失败状态。不要在未指定渲染器的结果上标记“真实 PDF 测试通过”。

## 目录

```text
src/ParcelCrop/              应用工程、清单、图标与 C# 源码
tests/ParcelCrop.Tests/      无外部测试框架的测试程序
scripts/                    构建、测试和便携包脚本
docs/                       使用、设计、来源、命名和发布文档
.github/                    自动化检查与协作模板
artifacts/                  本地生成的发行包；不纳入源码版本管理
```

## 开发约定

保持公开输出合同稳定：1600×2400、400 DPI、等比适配、质量 95、默认保留源文件、拒绝覆盖。行为修改必须同步[架构文档](ARCHITECTURE.md)与用户说明。

核心逻辑尽量不依赖真实桌面或真实业务文件。裁切算法用合成位图验证，转换用临时目录验证写入、尺寸、DPI、冲突与错误路径；真实渲染器测试单独运行。UI 相关回归应覆盖快速切换任务、清空、停止和关闭期间的异步完成，不只验证“正常点击一遍”。

测试必须使用专门的临时目录；回收或删除路径要显式限定。不要将用户源文件或原始便携包作为会被修改的测试夹具。公共测试样例不包含个人信息。

Windows Shell 通知需要既验证“提交最终文件之后才通知”的代码合同，也在打开的资源管理器窗口中人工验证；仅检查文件存在或 API 被调用不能证明 Explorer 已显示更新。实际发布流程见 [RELEASING.md](RELEASING.md)。

## 调试入口

| 问题 | 主要位置 |
| --- | --- |
| PDF 页数、渲染器进程、超时 | `PdfConverter` |
| 自动裁切不正确 | `ContentCrop` |
| 拖动、比例或输出预览 | `CropCanvas` |
| 批处理、重试、回收选项 | `MainForm` |
| 输出尺寸、DPI 和边距 | `OutputSpec`、`PdfConverter.Compose` |
| 任务生命周期与临时文件 | `LabelJob`、`PreparedPage` |

恢复代码仍有反编译形成的局部变量名；在修改相关逻辑时可以逐步改善，但避免与行为修复无关的大面积重命名。不要把恢复所得结构当作未经验证的原始设计意图。
