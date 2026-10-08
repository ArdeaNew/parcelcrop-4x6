# ParcelCrop 4x6

**2:3 面单裁切 · 单页 PDF → 4×6 英寸 JPG**

[English](README.en.md) · [使用指南](docs/USER_GUIDE.md) · [开发指南](docs/DEVELOPMENT.md) · [验证记录](docs/VALIDATION.md) · [更新记录](CHANGELOG.md)

ParcelCrop 4x6 是 Windows 桌面面单裁切工具。批量加入多个单页 PDF，检查自动检测的范围，按需移动、缩放裁切框和旋转方向，再导出到原文件夹。文档在本机处理，应用没有上传服务、账户或遥测。

当前版本：**2.2.1**。本项目从用户提供的 LabelTrim 2.1.1.0 可执行程序恢复源码并继续开发；恢复范围与限制见[来源说明](docs/PROVENANCE.md)。原名与已有同类产品重名，改名依据见[名称检索记录](docs/NAMING.md)。

## 输出规格

| 项目 | 规格 |
| --- | --- |
| 输入 | 每个 PDF 仅一页；可同时加入多个文件 |
| 输出 | 同目录、同主文件名的 `.jpg` |
| 像素尺寸 | 1600 × 2400，宽高比 2:3 |
| 分辨率元数据 | 400 DPI，对应 4 × 6 英寸（101.6 × 152.4 mm） |
| 图像处理 | 等比缩放、居中、白底，至少 24 px 边距；JPEG 质量 95 |
| 原文件 | 默认保留；可明确勾选成功导出后移入回收站 |

**2:3 是输出画布的比例。** 手动缩放裁切框保持初始检测框比例，内容等比放入输出画布；不会强行拉伸到 2:3。源图清晰度、裁切范围和打印机仍会影响条码质量，400 DPI 元数据不会补回原图缺失的细节。

## 开始使用

运行环境：Windows x64、.NET Framework 4.8，以及单独提供的 MuPDF `mutool.exe`。应用界面目前为英文。

1. 从本仓库 Releases 下载便携包并完整解压；也可按下文从源码构建。
2. 从 [MuPDF 官方下载入口](https://mupdf.com/releases)取得 Windows 版 `mutool.exe`，保留其许可文件。将它放在 `ParcelCrop.exe` 旁，或设置 `PARCELCROP_MUTOOL` 为其完整路径。详细操作见[渲染器配置](docs/USER_GUIDE.md#配置-pdf-渲染器)。**本项目公开便携包不包含该渲染器。**
3. 打开 `ParcelCrop.exe`，点击 **Browse files / Add files** 或拖入单页 PDF。
4. 检查左侧裁切范围和右侧输出预览；拖动框内部移动，拖动角点等比缩放，按需旋转。
5. 点击 **Process all**。JPG 保存到每个 PDF 所在目录，完成后可用 **Open folder** 定位。

已有同名 JPG 时会停止处理该文件，避免覆盖。空白页、多页 PDF、无法读取的加密或损坏 PDF 会显示错误并保留原文件。请先试打并扫描一张面单，再批量打印。

## 从源码构建与验证

准备 Windows x64、.NET SDK 10 和 .NET Framework 4.8 Developer Pack / Targeting Pack。在仓库根目录运行：

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/test.ps1 -RendererPath 'C:\Tools\MuPDF\mutool.exe'
./scripts/package.ps1 -Version 2.2.1
```

构建产物：`src/ParcelCrop/bin/Release/net48/ParcelCrop.exe`。便携包：`artifacts/ParcelCrop-4x6-2.2.1-win-x64.zip`，附 SHA-256 校验文件。默认测试验证核心行为；指定渲染器后增加真实 PDF 集成测试。自动化测试不能替代资源管理器可见性和实际打印的人工验收，验收项目见[发布流程](docs/RELEASING.md)。

## 范围与限制

- 只支持单页 PDF；多页面单请先拆分，不会默默只导出第一页。
- 自动裁切是基于图像边框和内容的启发式检测，不识别快递公司或业务字段，也不验证条码是否可扫描。
- 输出为 JPG 栅格图，不保留 PDF 矢量、文字层或交互表单。
- 没有打印驱动、邮资购买、运单生成、OCR 或云端同步功能。
- 回收站是否可用取决于存储位置和 Windows 设置；导出成功但回收失败时保留 PDF 并提示。

## 参与项目与许可

问题报告和贡献流程见 [CONTRIBUTING.md](CONTRIBUTING.md)，安全问题见 [SECURITY.md](SECURITY.md)。请使用脱敏或合成面单，不要把真实姓名、地址、电话、条码或追踪编号提交到公开仓库。

项目代码与文档采用 **AGPL-3.0-or-later**，完整条款见 [LICENSE](LICENSE)。MuPDF 与构建工具有各自许可和来源；见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
