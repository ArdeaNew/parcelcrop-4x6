# ParcelCrop 4x6

**2:3 shipping-label crops · single-page PDF → 4×6-inch JPG**

[简体中文](README.md) · [User guide](docs/USER_GUIDE.md) · [Development](docs/DEVELOPMENT.md) · [Changelog](CHANGELOG.md)

ParcelCrop 4x6 is a Windows desktop utility for cropping shipping labels. Add a batch of single-page PDFs, review the detected bounds, move or resize the crop and rotate as needed, then export beside each original. Processing is local; the application has no upload service, accounts, or telemetry. The current interface is in English.

Version **2.2.0** continues development from source recovered from a user-provided LabelTrim 2.1.1.0 executable. See [provenance](docs/PROVENANCE.md) for the recovery limits and [naming research](docs/NAMING.md) for the rename.

## Output contract

| Property | Value |
| --- | --- |
| Input | One page per PDF; multiple files can be queued |
| Output | Same directory and basename, with `.jpg` extension |
| Dimensions | 1600 × 2400 pixels; 2:3 width-to-height ratio |
| Resolution metadata | 400 DPI; 4 × 6 inches / 101.6 × 152.4 mm |
| Composition | Proportional fit, centered on white, at least 24 px border; JPEG quality 95 |
| Originals | Kept by default; optional move to Recycle Bin after successful export |

The **output canvas** has a 2:3 ratio. Resizing the crop retains the initial detection rectangle's ratio; the selected content fits proportionally within the canvas. It is not stretched. DPI metadata does not recover detail missing from a low-resolution source.

## Quick start

Requirements: Windows x64, .NET Framework 4.8, and a separately supplied MuPDF `mutool.exe`.

1. Download and extract the portable archive from this repository's Releases, or build from source before the first release.
2. Obtain Windows `mutool.exe` from the [official MuPDF releases](https://mupdf.com/releases), retaining its licensing material. Place it beside `ParcelCrop.exe`, or set `PARCELCROP_MUTOOL` to its full path. **The public portable package does not bundle the renderer.** See [renderer setup](docs/USER_GUIDE.md#配置-pdf-渲染器).
3. Launch `ParcelCrop.exe`. Choose **Browse files / Add files**, or drop single-page PDFs onto the window.
4. Review both previews. Drag inside the crop to move it, drag a corner to resize proportionally, and rotate if needed.
5. Click **Process all**. Use **Open folder** to locate the saved JPG.

Existing JPGs are never overwritten. Blank pages, multi-page PDFs, and unreadable encrypted or damaged documents are rejected while preserving the original. Test-print and scan one label before a full batch.

## Build and test

On Windows x64, install .NET SDK 10 and the .NET Framework 4.8 Developer Pack / Targeting Pack. From the repository root:

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/test.ps1 -RendererPath 'C:\Tools\MuPDF\mutool.exe'
./scripts/package.ps1 -Version 2.2.0
```

The executable is written to `src/ParcelCrop/bin/Release/net48/ParcelCrop.exe`. Packaging produces `artifacts/ParcelCrop-4x6-2.2.0-win-x64.zip` and a SHA-256 checksum file. The default tests cover core contracts; supplying a renderer adds real-PDF integration checks. Explorer visibility and physical printing need separate manual acceptance checks in the [release process](docs/RELEASING.md).

## Limitations

- Single-page PDFs only. Split multi-page input before importing.
- Automatic bounds detection is heuristic, not carrier-specific parsing, OCR, or barcode validation.
- JPG output is rasterized; PDF vectors, text layers, and interactive forms are not preserved.
- The application does not generate tracking numbers, buy postage, or provide a printer driver.
- Recycle Bin availability depends on the storage location and Windows settings. If recycling fails after export, the PDF is retained with a warning.

## Contributing and license

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Use synthetic or redacted samples; never publish real names, addresses, phone numbers, or tracking barcodes in issues or tests.

Project code and documentation are licensed under **AGPL-3.0-or-later**; see [LICENSE](LICENSE). MuPDF and development tools retain their respective licenses. See [third-party notices](THIRD_PARTY_NOTICES.md).
