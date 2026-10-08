# Third-party notices

## ParcelCrop 4x6

本项目代码与文档采用 **GNU Affero General Public License v3.0 or later（AGPL-3.0-or-later）**，完整条款见 [LICENSE](LICENSE)。

## MuPDF 1.28.5

- 版权所有：Artifex Software, Inc. 及 MuPDF 贡献者。
- 官方发行：[MuPDF 1.28.5](https://github.com/ArtifexSoftware/mupdf-downloads/releases/tag/1.28.5)。
- 随包文件：官方未修改的 Windows x64 `mutool.exe`。
- 许可：GNU AGPL v3 or later；第三方组件保留各自许可证。

便携包的 `licenses/MuPDF/` 目录保留上游 `COPYING.txt`、`README.txt`、`CHANGES.txt`，以及源码中依赖、字体和断词资料的许可与版权声明。完整文件清单和官方 SHA-256 记录在源码仓库的 `scripts/mupdf.lock.json`。

This software is based in part on the work of the Independent JPEG Group.

This software is based in part on the work of the FreeType Team (https://freetype.org).

### 对应源码

[本版本 Release](https://github.com/ArdeaNew/parcelcrop-4x6/releases/tag/v2.2.2) 与便携包同时提供未经改动的 **[mupdf-1.28.5-source.tar.gz](https://github.com/ArdeaNew/parcelcrop-4x6/releases/download/v2.2.2/mupdf-1.28.5-source.tar.gz)**，无需收费，可单独下载。该归档包含 MuPDF 与所需第三方源码和构建文件。

源码 SHA-256：`98a5c10cda20c3992cdf76ff6b2a1149c32bd79cc796d3f703230b1185b7e934`。

Windows 工程位于 `platform/win32/mupdf.sln`，配置为 `Release|x64`，使用 Visual Studio v142 工具集与 Windows SDK 10.0。构建说明见源码内文档及[对应版本的官方指南](https://mupdf.readthedocs.io/en/1.28.5/guide/install.html)。

## .NET Framework / Windows

.NET Framework 4.8、WinForms、System.Drawing 和 Windows Shell 是系统依赖，适用 Microsoft 的相应许可；本项目不分发 .NET Framework 安装程序。
