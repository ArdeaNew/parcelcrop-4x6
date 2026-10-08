# 使用指南

## 下载与启动

从 [Releases](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) 下载 `ParcelCrop-4x6-版本号-win-x64.zip`，完整解压后打开 `ParcelCrop.exe`。不要在 ZIP 内直接运行，也不要只复制主程序。便携包已包含所需 PDF 处理组件。

运行环境为 Windows 64 位与 .NET Framework 4.8。程序需要源 PDF 所在文件夹的写入权限。

## 裁切与导出

1. 点击 **Browse files / Add files** 选择一个或多个单页 PDF，也可拖入窗口。将 PDF 拖到程序图标上也可导入。
2. 选择右侧文件，等待预览。左侧 **CROP** 显示裁切范围，右侧 **OUTPUT** 显示输出效果。
3. 拖动裁切框内部移动位置，拖动角点等比调整大小。**Detect** 重新检测范围，**Rotate** 每次顺时针旋转 90°。
4. 按需逐个调整文件。不同文件保存各自的裁切范围和方向。
5. 点击 **Process all**。例如 `order.pdf` 会在同一文件夹生成 `order.jpg`。
6. 点击 **Open folder** 查看文件。

默认保留原 PDF。勾选 **Move PDF to Recycle Bin after successful export** 后，只有成功导出的源文件才会尝试移入回收站。

**Clear** 和移除按钮仅清理队列；**Stop after current** 在当前文件完成后停止后续任务。

## 失败后重试

失败原因显示在右侧详情框，可滚动查看和复制完整文字。修复原因后，点击 **Retry preview** 重试当前预览，或点击 **Retry unfinished** 处理尚未完成的文件。

| 提示或现象 | 处理方式 |
| --- | --- |
| PDF component unavailable | 重新下载并完整解压最新版便携包，不要只复制主程序；然后重试 |
| Multiple pages found | 将 PDF 拆分为单页后再导入 |
| Blank page / No label found | 检查页面内容，并调整裁切范围 |
| 同名 JPG 已存在 | 移动或重命名已有图片后重试 |
| PDF 在预览后发生变化 | 从队列移除，再重新添加 |
| PDF 无法读取 | 检查文件是否完整、是否需要密码；本工具不提供密码输入 |

## 输出规格

- 1600×2400 像素，400 DPI，4×6 英寸，宽高比 2:3。
- 裁切内容等比缩放并居中，白色背景，至少 24 像素边距；JPEG 质量 95。
- 2:3 是输出画布比例；手动调整裁切框保持初始框比例。
- 4×6 英寸约为 101.6×152.4 毫米，与 100×150 毫米并不完全相同。打印前检查纸张尺寸和缩放，并试印一张。

文件在本地处理。预览所需的临时副本会在移除任务或正常退出时清理。程序异常终止可能留下临时文件。
