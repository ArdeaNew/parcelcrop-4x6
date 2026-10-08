# 开发指南

## 环境

Windows x64、.NET SDK 10、.NET Framework 4.8 Developer Pack / Targeting Pack、PowerShell、Git 和 Windows 自带的 `tar.exe`。

应用目标框架为 `net48`，使用 WinForms 和系统程序集。Release 构建启用确定性编译、路径映射与警告视为错误。

## 构建和测试

在仓库根目录运行：

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

默认测试不要求下载 PDF 组件；真实 PDF 集成测试必须指定组件：

```powershell
./scripts/test.ps1 -RendererPath 'C:\Tools\MuPDF\mutool.exe'
```

应用优先使用程序目录中的 `mutool.exe`。仅在同目录组件不存在时，使用 `PARCELCROP_MUTOOL` 指定的有效路径；无效设置不会导致程序启动崩溃。

## 完整打包

```powershell
./scripts/package.ps1 -Version 2.2.2
```

打包脚本依据 `scripts/mupdf.lock.json` 从官方地址取得固定版本的组件和完整对应源码，校验 SHA-256，运行包含真实 PDF 的测试，并验证成包内组件。缓存仅供本机构建使用，不提交到源码仓库。

`artifacts/` 生成便携 ZIP、MuPDF 对应源码归档和校验文件。组件缺失、哈希不符、测试或成包验证失败时，打包必须终止。详细发布步骤见 [RELEASING.md](RELEASING.md)。

## 代码与测试

| 目录 | 内容 |
| --- | --- |
| `src/ParcelCrop/` | WinForms 应用与图像处理代码 |
| `tests/ParcelCrop.Tests/` | 合成图片、PDF、进程与界面回归测试 |
| `scripts/` | 构建、依赖恢复、测试、打包 |
| `docs/` | 使用、架构、验证和发布文档 |

保持 1600×2400、400 DPI、等比适配、默认保留源 PDF 和拒绝覆盖的行为。测试只使用合成数据；不要提交真实面单、凭据、构建缓存或含个人路径的截图。架构说明见 [ARCHITECTURE.md](ARCHITECTURE.md)。
