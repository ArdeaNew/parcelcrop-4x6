# ParcelCrop

**A simple way to crop PDF shipping labels into 4×6-inch JPGs.**

[Download](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) · [简体中文](README.md) · [User guide](docs/USER_GUIDE.md) · [Changelog](CHANGELOG.md)

![ParcelCrop demo](docs/assets/parcelcrop-demo.gif)

ParcelCrop is a lightweight Windows label-cropping tool: drop in single-page PDFs, check the preview, and click **Process all**. JPGs are saved beside the original PDFs, and everything is processed locally without extra configuration. For UPS, FedEx and similar labels downloaded from marketplaces such as Amazon, the valid label area is usually in the upper-left of the single-page A4 PDF and is detected automatically.

## Three simple steps

1. **Add**: click **Add files**, or drag multiple PDFs into the window.
2. **Check**: review the automatic crop; drag the crop box or use **Detect** and **Rotate** when needed.
3. **Export**: click **Process all** to create JPGs beside the source PDFs.

## Features

- Batch import of single-page PDFs with automatic content-bound detection.
- Crop preview with proportional adjustment, repositioning and 90° rotation.
- Consistent 4×6-inch JPG output with centered white margins and preserved proportions.
- Original PDFs are kept by default and can be moved to the Recycle Bin after a successful export.
- Local processing with queue removal, batch export and retry support.

## Output

- 1600×2400 pixels, 400 DPI, 4×6 inches, 2:3 aspect ratio.
- Cropped content is scaled proportionally and centered on white; JPEG quality is 95.
- The 2:3 ratio applies to the output canvas, so content is never stretched. Existing JPGs are not overwritten.

## Quick start

Requires Windows x64 and .NET Framework 4.8.

1. Download the `win-x64.zip` portable package from [Releases](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) and extract the entire archive.
2. Open `ParcelCrop.exe`. The PDF processing component is included; no extra setup is needed.
3. Add PDFs, check the preview, and click **Process all**.

Each input PDF must contain one page. See the [user guide](docs/USER_GUIDE.md) for the full workflow.

## License

Licensed under [AGPL-3.0-or-later](LICENSE). Bundled component licenses, copyright notices and corresponding source information are in [third-party notices](THIRD_PARTY_NOTICES.md).
