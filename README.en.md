# ParcelCrop 4x6

**Crop PDF shipping labels and export 4×6-inch JPGs**

[Download](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) · [简体中文](README.md) · [User guide](docs/USER_GUIDE.md) · [Changelog](CHANGELOG.md)

ParcelCrop 4x6 imports batches of single-page PDFs, detects content bounds and previews the result. Adjust the crop rectangle or rotate the label before exporting.

Output is a 1600×2400-pixel JPG at 400 DPI: a 4×6-inch, 2:3 canvas. Content keeps its proportions, is centered on white, and is saved beside the original PDF.

## Features

- Add or drag in multiple single-page PDFs.
- Automatic bounds detection, crop previews, proportional adjustments and 90° rotation.
- Consistent JPG dimensions with centered white margins.
- Keep original PDFs by default; optionally recycle them after successful export.
- Local processing, queue management, batch export and retry.

## Quick start

Requires Windows x64 and .NET Framework 4.8.

1. Download the `win-x64.zip` portable package from [Releases](https://github.com/ArdeaNew/parcelcrop-4x6/releases/latest) and extract the entire archive.
2. Open `ParcelCrop.exe`. **The PDF processing component is included; no separate configuration is needed.**
3. Click **Add files** or drop PDFs onto the window. Review and adjust each crop.
4. Click **Process all**. JPGs are saved beside their source PDFs.

Each input PDF must contain one page. Existing JPGs are not overwritten. The 2:3 ratio applies to the output canvas; content is not stretched. See the [user guide](docs/USER_GUIDE.md) for more detail.

## Development and feedback

See [development](docs/DEVELOPMENT.md), [validation](docs/VALIDATION.md), [contributing](CONTRIBUTING.md) and [security](SECURITY.md).

## License

Licensed under [AGPL-3.0-or-later](LICENSE). Bundled component licenses, copyright notices and corresponding source information are in [third-party notices](THIRD_PARTY_NOTICES.md).
