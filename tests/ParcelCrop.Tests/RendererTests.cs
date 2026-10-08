// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ParcelCrop.Tests
{
    internal static class RendererTests
    {
        internal static void Register(Action<string, Action> add)
        {
            add("renderer / missing executable is an actionable error", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    AppError error = Check.Throws<AppError>(() =>
                    {
                        using (new PdfConverter(temp.PathFor("not-installed.exe")).Prepare(source, CancellationToken.None)) { }
                    }, "Missing renderer");
                    Check.Equal(AppErrorKind.ComponentUnavailable, error.Kind, "Missing component classification");
                    Check.True(error.Message.IndexOf("complete Windows package", StringComparison.OrdinalIgnoreCase) >= 0, "Missing component message omits recovery step");
                    Check.True(File.Exists(source), "Missing renderer removed source");
                }
            });
            add("renderer / bundled component takes precedence over old environment settings", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string bundled = temp.PathFor("mutool.exe");
                    string other = temp.PathFor("old component.exe");
                    File.WriteAllText(bundled, "test placeholder");
                    File.WriteAllText(other, "test placeholder");
                    Check.Equal(bundled, RendererPathResolver.Resolve(null, temp.DirectoryPath, other), "Bundled component priority");
                    Check.Equal(bundled, RendererPathResolver.Resolve(null, temp.DirectoryPath, "invalid\0path"), "Invalid old setting blocked bundle");
                }
            });
            add("renderer / source builds can use a valid configured component", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string configured = temp.PathFor("configured component.exe");
                    File.WriteAllText(configured, "test placeholder");
                    Check.Equal(configured, RendererPathResolver.Resolve(null, temp.DirectoryPath, configured), "Configured fallback");
                    Check.Equal(temp.PathFor("mutool.exe"), RendererPathResolver.Resolve(null, temp.DirectoryPath, "invalid\0path"), "Invalid setting handling");
                    Check.Equal(temp.PathFor("mutool.exe"), RendererPathResolver.Resolve(null, temp.DirectoryPath, temp.PathFor("missing.exe")), "Missing configured path handling");
                }
            });
            add("renderer / explicit component selections never silently fall back", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    File.WriteAllText(temp.PathFor("mutool.exe"), "test placeholder");
                    string missing = temp.PathFor("explicit missing.exe");
                    Check.Equal(missing, RendererPathResolver.Resolve(missing, temp.DirectoryPath, null), "Explicit override");
                    AppError error = Check.Throws<AppError>(() => RendererPathResolver.Resolve("invalid\0path", temp.DirectoryPath, null), "Invalid explicit path");
                    Check.Equal(AppErrorKind.ComponentUnavailable, error.Kind, "Invalid explicit path classification");
                }
            });
            add("renderer / invalid executable is a component error and preserves PDF", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    string broken = temp.PathFor("broken.exe");
                    File.WriteAllText(broken, "This is not an executable.");
                    AppError error = Check.Throws<AppError>(() => new PdfConverter(broken).Prepare(source, CancellationToken.None), "Invalid executable");
                    Check.Equal(AppErrorKind.ComponentUnavailable, error.Kind, "Invalid executable classification");
                    Check.True(File.Exists(source), "Component failure removed source");
                }
            });
            add("renderer / missing DLL exit is a component error", () => CheckComponentExit("missing-dll"));
            add("renderer / invalid Windows image exit is a component error", () => CheckComponentExit("bad-image"));
            add("renderer / single page prepares a disposable preview", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.FakePdf("single");
                    string rasterDirectory;
                    using (PreparedPage page = new PdfConverter(Program.SelfPath).Prepare(source, CancellationToken.None))
                    {
                        rasterDirectory = page.DirectoryPath;
                        Check.Equal(new Size(200, 300), page.PageSize, "Prepared image size");
                        Check.Equal(TempCase.Hash(source), page.SourceHash, "Preview source hash");
                        Check.True(!page.IsBlank && !page.AutoCrop.IsEmpty, "Prepared label was blank");
                        Check.True(File.Exists(page.RasterPath), "Preview raster missing");
                        using (Bitmap thumbnail = page.LoadThumbnail())
                            Check.True(thumbnail.Width <= 1500 && thumbnail.Height <= 1500, "Thumbnail not bounded");
                    }
                    Check.True(!Directory.Exists(rasterDirectory), "Preview directory leaked after Dispose");
                    Check.True(File.Exists(source), "Preparing preview removed source");
                }
            });
            add("renderer / blank rendered page is identified", () =>
            {
                using (TempCase temp = new TempCase())
                using (PreparedPage page = new PdfConverter(Program.SelfPath).Prepare(temp.FakePdf("blank"), CancellationToken.None))
                    Check.True(page.IsBlank && page.AutoCrop.IsEmpty, "Blank page not identified");
            });
            add("renderer / multiple pages are refused before rendering", () =>
            {
                RejectFake("multi", "Multiple");
            });
            add("renderer / process failure is surfaced and PDF retained", () =>
            {
                RejectFake("damaged", null);
            });
            add("renderer / malformed page count is refused", () =>
            {
                RejectFake("invalid-count", null);
            });
            add("renderer / successful process without raster is refused", () =>
            {
                RejectFake("no-raster", null);
            });
            add("renderer / cancellation before starting preserves PDF", () =>
            {
                using (TempCase temp = new TempCase())
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    string source = temp.FakePdf("single");
                    cancellation.Cancel();
                    Check.Throws<OperationCanceledException>(() =>
                        new PdfConverter(Program.SelfPath).PrepareAsync(source, cancellation.Token).GetAwaiter().GetResult(), "Pre-cancelled preparation");
                    Check.True(File.Exists(source), "Cancelled preparation removed PDF");
                }
            });
            add("renderer / in-flight cancellation terminates child process", () =>
            {
                using (TempCase temp = new TempCase())
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    string source = temp.FakePdf("hang");
                    string marker = temp.PathFor("renderer.pid");
                    File.AppendAllText(source, "\n" + marker);
                    Task<PreparedPage> preparation = new PdfConverter(Program.SelfPath).PrepareAsync(source, cancellation.Token);
                    int processId = 0;
                    try
                    {
                        Stopwatch waiting = Stopwatch.StartNew();
                        while (processId == 0 && !preparation.IsCompleted && waiting.Elapsed.TotalSeconds < 5)
                        {
                            // Creation and writing are separate operations; do not read a half-written PID.
                            try
                            {
                                if (File.Exists(marker)) int.TryParse(File.ReadAllText(marker), out processId);
                            }
                            catch (IOException) { }
                            if (processId == 0) Thread.Sleep(20);
                        }
                        Check.True(processId > 0, "Fake renderer did not start within five seconds");
                        Stopwatch cancelled = Stopwatch.StartNew();
                        cancellation.Cancel();
                        Check.Throws<OperationCanceledException>(() => preparation.GetAwaiter().GetResult(), "In-flight preparation");
                        Check.True(cancelled.Elapsed.TotalSeconds < 5, "Cancellation was not prompt");
                        Check.True(!IsRunning(processId), "Cancelled renderer process remained running");
                        Check.True(File.Exists(source), "In-flight cancellation removed PDF");
                    }
                    finally
                    {
                        cancellation.Cancel();
                        try
                        {
                            PreparedPage page = preparation.GetAwaiter().GetResult();
                            if (page != null) page.Dispose();
                        }
                        catch (OperationCanceledException) { }
                    }
                }
            });
        }

        internal static void RegisterIntegration(Action<string, Action> add, string renderer)
        {
            add("MuPDF integration / generated single-page PDF renders and converts", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf("single page \u4e2d\u6587.pdf");
                    PdfConverter converter = new PdfConverter(renderer);
                    using (PreparedPage page = converter.Prepare(source, CancellationToken.None))
                    {
                        Check.True(!page.IsBlank && !page.AutoCrop.IsEmpty, "Real renderer missed generated label");
                        Check.True(page.PageSize.Width > 0 && page.PageSize.Height > 0, "Real raster size missing");
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0);
                        using (Bitmap output = new Bitmap(result.OutputPath))
                        {
                            Check.Equal(new Size(1600, 2400), output.Size, "Real PDF output size");
                            Check.Near(400, output.HorizontalResolution, 0.1, "Real PDF horizontal DPI");
                            Check.Near(400, output.VerticalResolution, 0.1, "Real PDF vertical DPI");
                            Color center = output.GetPixel(800, 1200);
                            Check.True(center.R < 20 && center.G < 20 && center.B < 20, "Generated black label content was lost");
                        }
                        Check.True(File.Exists(source) && !result.Recycled, "Integration default removed source");
                    }
                    temp.NoTemporaryOutputs();
                }
            });
            add("MuPDF integration / generated two-page PDF is refused", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf(pages: 2);
                    Check.Throws<AppError>(() =>
                    {
                        using (new PdfConverter(renderer).Prepare(source, CancellationToken.None)) { }
                    }, "Real multi-page PDF");
                    Check.True(File.Exists(source), "Multi-page source removed");
                }
            });
            add("MuPDF integration / generated blank PDF is identified", () =>
            {
                using (TempCase temp = new TempCase())
                using (PreparedPage page = new PdfConverter(renderer).Prepare(temp.Pdf(blank: true), CancellationToken.None))
                    Check.True(page.IsBlank && page.AutoCrop.IsEmpty, "Real blank PDF not identified");
            });
            add("MuPDF integration / damaged PDF is refused and retained", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.PathFor("damaged.pdf");
                    File.WriteAllText(source, "This is deliberately not a PDF.\n");
                    Check.Throws<AppError>(() =>
                    {
                        using (new PdfConverter(renderer).Prepare(source, CancellationToken.None)) { }
                    }, "Real damaged PDF");
                    Check.True(File.Exists(source), "Damaged source removed");
                }
            });
        }

        private static void RejectFake(string mode, string messagePart)
        {
            using (TempCase temp = new TempCase())
            {
                string source = temp.FakePdf(mode);
                AppError error = Check.Throws<AppError>(() =>
                {
                    using (new PdfConverter(Program.SelfPath).Prepare(source, CancellationToken.None)) { }
                }, "Synthetic renderer mode " + mode);
                if (messagePart != null)
                    Check.True(error.Message.IndexOf(messagePart, StringComparison.OrdinalIgnoreCase) >= 0,
                        "Wrong diagnostic for " + mode + ": " + error.Message);
                Check.True(File.Exists(source), "Rejected preparation removed source");
                Check.True(!File.Exists(Path.ChangeExtension(source, ".jpg")), "Rejected preparation published output");
            }
        }

        private static void CheckComponentExit(string mode)
        {
            using (TempCase temp = new TempCase())
            {
                string source = temp.FakePdf(mode);
                AppError error = Check.Throws<AppError>(() => new PdfConverter(Program.SelfPath).Prepare(source, CancellationToken.None), "Component load failure");
                Check.Equal(AppErrorKind.ComponentUnavailable, error.Kind, "Component exit classification");
                Check.True(File.Exists(source), "Component exit removed source");
            }
        }

        private static bool IsRunning(int processId)
        {
            try { using (Process process = Process.GetProcessById(processId)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
        }
    }
}
