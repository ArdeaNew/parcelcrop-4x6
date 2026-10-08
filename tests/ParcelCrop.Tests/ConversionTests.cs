// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ParcelCrop.Tests
{
    internal static class ConversionTests
    {
        internal static void Register(Action<string, Action> add)
        {
            add("recycle guard / permanent-delete fallback is vetoed", () =>
            {
                RecycleOnlyProgressSink sink = new RecycleOnlyProgressSink();
                Check.True(sink.PreDeleteItem(0, IntPtr.Zero) < 0 && !sink.RecycleApproved,
                    "Shell request without recycle capability must be vetoed");
                Check.Equal(0, sink.PreDeleteItem(0x80, IntPtr.Zero), "Shell recycle-capable request should be accepted");
                Check.True(sink.RecycleApproved, "Recycle request was not recorded");
                Check.True(sink.PreDeleteItem(0, IntPtr.Zero) < 0 && !sink.RecycleApproved,
                    "A later fallback request must also be vetoed");
            });
            add("recycle guard / native progress interface is callable", () =>
            {
                Check.Equal(new Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), typeof(IRecycleProgressSink).GUID,
                    "IFileOperationProgressSink interface identifier");
                RecycleOnlyProgressSink sink = new RecycleOnlyProgressSink();
                IntPtr native = Marshal.GetComInterfaceForObject(sink, typeof(IRecycleProgressSink));
                try { Check.True(native != IntPtr.Zero, "Shell cannot acquire progress callback interface"); }
                finally { if (native != IntPtr.Zero) Marshal.Release(native); }
            });
            add("convert / existing JPG is never overwritten", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    string output = Path.ChangeExtension(source, ".jpg");
                    File.WriteAllText(output, "existing-customer-output");
                    string sourceHash = TempCase.Hash(source);
                    using (PreparedPage page = temp.Page(source))
                        Check.Throws<AppError>(() => new PdfConverter().Convert(source, page, page.AutoCrop, 0), "Existing output");
                    Check.Equal("existing-customer-output", File.ReadAllText(output), "Existing JPG changed");
                    Check.Equal(sourceHash, TempCase.Hash(source), "Source changed after refusal");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / changed PDF after preview is refused", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    using (PreparedPage page = temp.Page(source))
                    {
                        File.AppendAllText(source, "\n% changed after preview\n");
                        string changedHash = TempCase.Hash(source);
                        Check.Throws<AppError>(() => new PdfConverter().Convert(source, page, page.AutoCrop, 0), "Changed source");
                        Check.Equal(changedHash, TempCase.Hash(source), "Changed source was altered or removed");
                    }
                    Check.True(!File.Exists(Path.ChangeExtension(source, ".jpg")), "Changed source produced a JPG");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / blank prepared page is refused", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf(blank: true);
                    using (PreparedPage page = temp.Page(source, true))
                        Check.Throws<AppError>(() => new PdfConverter().Convert(source, page, new Rectangle(0, 0, 100, 100), 0), "Blank page");
                    Check.True(File.Exists(source), "Blank PDF was removed");
                    Check.True(!File.Exists(Path.ChangeExtension(source, ".jpg")), "Blank page produced a JPG");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / empty and white-only crop do not publish output", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter();
                        Check.Throws<AppError>(() => converter.Convert(source, page, Rectangle.Empty, 0), "Empty crop");
                        Check.Throws<AppError>(() => converter.Convert(source, page, new Rectangle(0, 0, 20, 20), 0), "White crop");
                    }
                    Check.True(File.Exists(source), "Source removed after invalid crop");
                    Check.True(!File.Exists(Path.ChangeExtension(source, ".jpg")), "Empty crop produced a JPG");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / default keeps PDF and publishes valid JPEG", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf("label with spaces \u4e2d\u6587.pdf");
                    string sourceHash = TempCase.Hash(source);
                    int recycleCalls = 0;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(recycleFile: ignored => recycleCalls++);
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0);
                        Check.Equal(Path.ChangeExtension(source, ".jpg"), result.OutputPath, "Output path");
                        Check.True(!result.Recycled, "Default conversion recycled the PDF");
                        Check.True(string.IsNullOrEmpty(result.Warning), "Successful keep-source conversion had a warning");
                        using (Bitmap output = new Bitmap(result.OutputPath))
                        {
                            Check.Equal(new Size(1600, 2400), output.Size, "Published JPG size");
                            Check.Near(400, output.HorizontalResolution, 0.1, "Published JPG DPI");
                        }
                    }
                    Check.Equal(0, recycleCalls, "Default conversion invoked recycler");
                    Check.Equal(sourceHash, TempCase.Hash(source), "Default conversion changed source");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / commit precedes creation notification and files are unlocked", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    int created = 0;
                    Exception callbackError = null;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(notifyCreated: output =>
                        {
                            try
                            {
                                created++;
                                using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                                    Check.True(input.Length > 0, "Source unavailable at creation notification");
                                using (FileStream jpeg = new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                                using (Bitmap image = new Bitmap(jpeg))
                                    Check.Equal(new Size(1600, 2400), image.Size, "Notification saw incomplete JPG");
                            }
                            catch (Exception error) { callbackError = error; throw; }
                        });
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0);
                        if (callbackError != null) throw new InvalidOperationException("Creation callback assertion failed", callbackError);
                        Check.Equal(1, created, "Creation notification count");
                        Check.True(string.IsNullOrEmpty(result.Warning), "Callback unexpectedly failed");
                    }
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / recycle and delete notification follow committed JPG", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    string output = Path.ChangeExtension(source, ".jpg");
                    List<string> events = new List<string>();
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(
                            notifyCreated: path => events.Add("created:" + File.Exists(path)),
                            recycleFile: path =>
                            {
                                events.Add("recycle:" + File.Exists(output));
                                File.Delete(path); // Only this test's generated fixture; never invokes the real Recycle Bin.
                            },
                            notifyDeleted: path => events.Add("deleted:" + !File.Exists(path)));
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true);
                        Check.True(result.Recycled, "Explicit successful recycler did not set Recycled");
                        Check.Equal("created:True,recycle:True,deleted:True", string.Join(",", events), "Publication/recycle/notification order");
                        Check.True(File.Exists(output) && !File.Exists(source), "Successful explicit recycle state");
                    }
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / recycle failure keeps committed JPG and PDF with warning", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    int deleted = 0;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(recycleFile: ignored => { throw new IOException("Synthetic recycle failure"); },
                            notifyDeleted: ignored => deleted++);
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true);
                        Check.True(!result.Recycled && !string.IsNullOrEmpty(result.Warning), "Recycle failure result");
                        Check.True(File.Exists(result.OutputPath) && File.Exists(source), "Recycle failure lost a file");
                    }
                    Check.Equal(0, deleted, "Deletion notification sent for retained source");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / no-op recycler cannot claim PDF was recycled", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    int deleted = 0;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(recycleFile: ignored => { }, notifyDeleted: ignored => deleted++);
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true);
                        Check.True(!result.Recycled && !string.IsNullOrEmpty(result.Warning), "No-op recycler claimed success");
                        Check.True(File.Exists(source) && File.Exists(result.OutputPath), "No-op recycler lost a file");
                    }
                    Check.Equal(0, deleted, "No-op recycler caused deleted notification");
                }
            });
            add("convert / source changed during creation notification is not recycled", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    int recycled = 0;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(notifyCreated: ignored => File.AppendAllText(source, "\n% newer revision\n"),
                            recycleFile: ignored => recycled++);
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true);
                        Check.True(!result.Recycled && !string.IsNullOrEmpty(result.Warning), "Late source change must produce warning");
                        Check.True(File.Exists(source) && File.Exists(result.OutputPath), "Late source change lost a file");
                    }
                    Check.Equal(0, recycled, "Changed PDF was passed to recycler");
                }
            });
            add("convert / creation notification failure cannot roll back output", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(notifyCreated: ignored => { throw new IOException("Synthetic shell notification failure"); });
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0);
                        Check.True(File.Exists(result.OutputPath) && File.Exists(source), "Notification failure lost a file");
                        Check.True(!string.IsNullOrEmpty(result.Warning), "Notification failure was not reported");
                    }
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / deletion notification failure cannot roll back output", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(recycleFile: File.Delete,
                            notifyDeleted: ignored => { throw new IOException("Synthetic shell notification failure"); });
                        ConversionResult result = converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true);
                        Check.True(result.Recycled && File.Exists(result.OutputPath), "Delete notification failure lost success state");
                        Check.True(!string.IsNullOrEmpty(result.Warning), "Delete notification failure was not reported");
                    }
                }
            });
            add("convert / failed output commit leaves source and sends no notifications", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    string output = Path.ChangeExtension(source, ".jpg");
                    Directory.CreateDirectory(output); // Collision forces File.Move to fail after temporary encoding.
                    int effects = 0;
                    using (PreparedPage page = temp.Page(source))
                    {
                        PdfConverter converter = new PdfConverter(notifyCreated: ignored => effects++,
                            recycleFile: ignored => effects++, notifyDeleted: ignored => effects++);
                        Check.Throws<IOException>(() => converter.Convert(source, page, page.AutoCrop, 0, recycleSource: true), "Output commit collision");
                    }
                    Check.Equal(0, effects, "Uncommitted conversion produced side effects");
                    Check.True(File.Exists(source) && Directory.Exists(output), "Commit collision changed source or destination");
                    temp.NoTemporaryOutputs();
                }
            });
            add("convert / missing raster leaves PDF intact", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    using (PreparedPage page = temp.Page(source))
                    {
                        File.Delete(page.RasterPath);
                        Check.Throws<Exception>(() => new PdfConverter().Convert(source, page, page.AutoCrop, 0), "Missing raster");
                    }
                    Check.True(File.Exists(source) && !File.Exists(Path.ChangeExtension(source, ".jpg")), "Missing raster lost or published files");
                    temp.NoTemporaryOutputs();
                }
            });
            add("prepared page / dispose removes only its owned raster directory", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string source = temp.Pdf();
                    PreparedPage page = temp.Page(source);
                    string directory = page.DirectoryPath;
                    page.Dispose(); page.Dispose();
                    Check.True(!Directory.Exists(directory), "Prepared raster directory remained");
                    Check.True(File.Exists(source), "Prepared page disposal removed original PDF");
                }
            });
        }
    }
}
