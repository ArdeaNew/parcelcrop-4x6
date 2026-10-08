// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ParcelCrop.Tests
{
    internal static class Check
    {
        internal static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + ": expected " + expected + ", got " + actual);
        }

        internal static void Near(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException(message + ": expected " + expected + " +/- " + tolerance + ", got " + actual);
        }

        internal static T Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            catch (Exception error)
            {
                throw new InvalidOperationException(message + ": expected " + typeof(T).Name + ", got " + error.GetType().Name, error);
            }
            throw new InvalidOperationException(message + ": expected " + typeof(T).Name + ", but no exception was thrown");
        }

        internal static void White(Color color, string message)
        {
            True(color.R >= 250 && color.G >= 250 && color.B >= 250, message);
        }

        internal static void ColorNear(Color expected, Color actual, string message)
        {
            True(Math.Abs(expected.R - actual.R) < 12 && Math.Abs(expected.G - actual.G) < 12 &&
                 Math.Abs(expected.B - actual.B) < 12, message + ": expected " + expected + ", got " + actual);
        }
    }

    internal sealed class TempCase : IDisposable
    {
        private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), "ParcelCrop.Tests");
        internal readonly string DirectoryPath;

        internal TempCase()
        {
            DirectoryPath = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        internal string PathFor(string name) { return Path.Combine(DirectoryPath, name); }

        internal string Pdf(string name = "label.pdf", int pages = 1, bool blank = false)
        {
            string path = PathFor(name);
            PdfFixture.Write(path, pages, blank);
            return path;
        }

        internal string FakePdf(string mode)
        {
            string path = PathFor(mode + ".pdf");
            File.WriteAllText(path, "PARCELCROP-TEST:" + mode, Encoding.ASCII);
            return path;
        }

        internal PreparedPage Page(string source, bool blank = false)
        {
            string pageDirectory = PathFor("page-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pageDirectory);
            string raster = Path.Combine(pageDirectory, "page.png");
            using (Bitmap bitmap = Images.White(200, 300))
            {
                if (!blank)
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                        graphics.FillRectangle(Brushes.Black, 40, 60, 100, 160);
                bitmap.Save(raster, ImageFormat.Png);
            }
            return new PreparedPage
            {
                DirectoryPath = pageDirectory,
                RasterPath = raster,
                SourceHash = Hash(source),
                PageSize = new Size(200, 300),
                AutoCrop = blank ? Rectangle.Empty : new Rectangle(37, 57, 106, 166),
                IsBlank = blank
            };
        }

        internal void NoTemporaryOutputs()
        {
            Check.Equal(0, Directory.GetFiles(DirectoryPath, ".parcelcrop-*.tmp").Length, "Temporary output leaked");
        }

        internal static string Hash(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream));
        }

        public void Dispose()
        {
            string resolved = Path.GetFullPath(DirectoryPath);
            string prefix = Path.GetFullPath(TestRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing cleanup outside the test directory");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }

    internal static class Images
    {
        internal static Bitmap White(int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.White);
            return bitmap;
        }

        internal static void Fill(Bitmap bitmap, Rectangle rectangle, Color color)
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(color)) graphics.FillRectangle(brush, rectangle);
        }

        internal static void Frame(Bitmap bitmap, Rectangle rectangle)
        {
            Fill(bitmap, new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, 1), Color.Black);
            Fill(bitmap, new Rectangle(rectangle.Left, rectangle.Bottom - 1, rectangle.Width, 1), Color.Black);
            Fill(bitmap, new Rectangle(rectangle.Left, rectangle.Top, 1, rectangle.Height), Color.Black);
            Fill(bitmap, new Rectangle(rectangle.Right - 1, rectangle.Top, 1, rectangle.Height), Color.Black);
        }

        internal static Rectangle DarkBounds(Bitmap bitmap)
        {
            int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100)
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                    }
                }
            return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
    }

    // Tiny deterministic PDFs with valid byte offsets; no third-party PDF library or customer data.
    internal static class PdfFixture
    {
        internal static void Write(string path, int pageCount, bool blank)
        {
            List<string> objects = new List<string>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            StringBuilder kids = new StringBuilder();
            for (int page = 0; page < pageCount; page++) kids.Append((3 + page * 2).ToString(CultureInfo.InvariantCulture) + " 0 R ");
            objects.Add("<< /Type /Pages /Kids [" + kids + "] /Count " + pageCount.ToString(CultureInfo.InvariantCulture) + " >>");
            for (int page = 0; page < pageCount; page++)
            {
                objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 216] /Resources << >> /Contents " +
                            (4 + page * 2).ToString(CultureInfo.InvariantCulture) + " 0 R >>");
                string content = blank ? "q\nQ\n" : "q\n0 g\n24 36 96 144 re f\nQ\n";
                objects.Add("<< /Length " + Encoding.ASCII.GetByteCount(content).ToString(CultureInfo.InvariantCulture) +
                            " >>\nstream\n" + content + "endstream");
            }
            using (FileStream stream = File.Create(path))
            {
                Action<string> write = text =>
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);
                };
                write("%PDF-1.4\n");
                List<long> offsets = new List<long>();
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(stream.Position);
                    write((i + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + objects[i] + "\nendobj\n");
                }
                long xref = stream.Position;
                write("xref\n0 " + (objects.Count + 1).ToString(CultureInfo.InvariantCulture) + "\n0000000000 65535 f \n");
                foreach (long offset in offsets) write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
                write("trailer\n<< /Size " + (objects.Count + 1).ToString(CultureInfo.InvariantCulture) +
                      " /Root 1 0 R >>\nstartxref\n" + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
            }
        }
    }
}
