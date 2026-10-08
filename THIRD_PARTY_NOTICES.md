# Third-party notices

## ParcelCrop 4x6

本仓库应用代码、恢复后继续开发的改动与项目文档按 **GNU Affero General Public License v3.0 or later（AGPL-3.0-or-later）** 发布，完整条款见 [LICENSE](LICENSE)。来源和恢复范围见 [docs/PROVENANCE.md](docs/PROVENANCE.md)。原始包中的代码与资源来源说明不会因恢复源码而自动变成第三方授权证明。

## MuPDF / mutool — 外部运行依赖

- 开发者：Artifex Software, Inc. 及 MuPDF 贡献者。
- 官方源码：[ArtifexSoftware/mupdf](https://github.com/ArtifexSoftware/mupdf)。
- 官方下载：[MuPDF releases](https://mupdf.com/releases)。
- 许可：官方提供 GNU AGPL 和商业许可选择，详见[官方许可说明](https://mupdf.readthedocs.io/en/latest/license.html)及所使用版本附带的 `COPYING`、版权和第三方许可材料。

应用通过独立进程调用 `mutool show` 与 `mutool draw`。仓库和公开便携包**不捆绑** `mutool.exe`，使用者自行取得并配置。项目的 AGPL 许可不替代渲染器自身的许可条款。

原始便携包的 `ThirdPartyNotices.txt` 声明使用未修改的 mutool 1.26.2。该声明只作为来源记录：原二进制的构建来源、对应源码和完整依赖许可未得到独立核验，因此不会将其复制到公开发布物。未来若决定捆绑渲染器，应针对确切版本保留完整许可和版权说明，提供适用的对应源码与构建材料，核验所有传递依赖，并在发布清单列明来源、版本及哈希。仅链接上游主页不能代替应履行的发布义务。

## .NET Framework / Windows — 系统依赖

应用使用 .NET Framework 4.8 的 WinForms、System.Drawing 等系统程序集及 Windows Shell API。运行时和目标框架引用程序集不随本应用源码或便携包重新授权，按 Microsoft 的相应许可使用。项目不包含 .NET Framework 安装程序。

## ILSpy — 仅源码恢复工具

恢复过程使用 ILSpy / ilspycmd 11.1。项目和许可分别见 [ILSpy](https://github.com/icsharpcode/ILSpy) 与 [MIT license](https://github.com/icsharpcode/ILSpy/blob/master/LICENSE)。它不是 ParcelCrop 的运行时依赖，未随应用发布；重新构建本项目不需要它。

## 图标与测试材料

应用图标从用户提供的原程序集恢复，其来源记录与原程序一致。测试样例应由测试代码生成或明确标注可再分发来源；不应使用真实快递面单。没有采用第三方字体包、图标库或外部 NuGet 测试框架。
