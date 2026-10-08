# ParcelCrop 4x6

**PDF 面单裁切与 JPG 导出（2:3 比例）**

[下载最新版](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) · [English](README.en.md) · [使用指南](docs/USER_GUIDE.md) · [更新记录](CHANGELOG.md)

ParcelCrop 4x6 支持批量导入单页 PDF，并自动检测裁切范围。导入后可预览裁切效果，手动调整裁切框或旋转面单。

导出为 4×6 英寸、2:3 比例的 JPG（1600×2400 像素，400 DPI），保持内容比例，保存到原文件夹。

## 主要功能

- 批量添加或拖入多个单页 PDF。
- 自动检测内容范围，预览并调整裁切框，按 90° 旋转面单。
- 等比缩放、居中留白，输出统一规格的 JPG。
- 默认保留原 PDF，可选择成功导出后移入回收站。
- 本地处理，支持任务移除、批量导出和失败后重试。

## 快速开始

运行环境：Windows 64 位、.NET Framework 4.8。

1. 从 [Releases](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) 下载名称以 `win-x64.zip` 结尾的便携包，完整解压。
2. 打开解压后的 `ParcelCrop.exe`。**便携包已包含 PDF 处理组件，无需另行配置。**
3. 点击 **Add files** 或拖入 PDF，检查预览并调整裁切范围。
4. 点击 **Process all**，JPG 保存到原 PDF 所在文件夹。

每个输入 PDF 仅支持一页；已有同名 JPG 不会被覆盖。2:3 是输出画布比例，内容保持原比例，不会被拉伸。更多操作见[使用指南](docs/USER_GUIDE.md)。

## 开发与反馈

源码构建、测试与打包见[开发指南](docs/DEVELOPMENT.md)；测试结果见[验证记录](docs/VALIDATION.md)。

问题反馈与贡献方式见 [CONTRIBUTING.md](CONTRIBUTING.md)，安全问题见 [SECURITY.md](SECURITY.md)。

## 许可

本项目采用 [AGPL-3.0-or-later](LICENSE)。随包组件的许可、版权和对应源码见[第三方声明](THIRD_PARTY_NOTICES.md)。
