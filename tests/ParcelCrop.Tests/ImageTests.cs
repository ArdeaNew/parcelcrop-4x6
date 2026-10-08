// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Drawing;

namespace ParcelCrop.Tests
{
    internal static class ImageTests
    {
        internal static void Register(Action<string, Action> add)
        {
            add("crop / white page is blank", () =>
            {
                using (Bitmap image = Images.White(80, 120))
                    Check.Equal(Rectangle.Empty, ContentCrop.Detect(image), "Blank page crop");
            });
            add("crop / transparent page is blank", () =>
            {
                using (Bitmap image = new Bitmap(80, 120))
                    Check.Equal(Rectangle.Empty, ContentCrop.Detect(image), "Transparent pixels must composite on white");
            });
            add("crop / content receives padding", () =>
            {
                using (Bitmap image = Images.White(200, 300))
                {
                    Images.Fill(image, new Rectangle(50, 70, 60, 90), Color.Black);
                    Check.Equal(new Rectangle(47, 67, 66, 96), ContentCrop.Detect(image), "Content bounds plus padding");
                }
            });
            add("crop / padding stays within image at each edge", () =>
            {
                foreach (Rectangle ink in new[] { new Rectangle(0, 0, 5, 5), new Rectangle(75, 0, 5, 5),
                            new Rectangle(0, 115, 5, 5), new Rectangle(75, 115, 5, 5) })
                using (Bitmap image = Images.White(80, 120))
                {
                    Images.Fill(image, ink, Color.Black);
                    Rectangle crop = ContentCrop.Detect(image);
                    Check.True(new Rectangle(0, 0, 80, 120).Contains(crop), "Crop escaped image at " + ink);
                    Check.True(crop.Contains(ink), "Edge content was cut off at " + ink);
                }
            });
            add("crop / isolated noise and three-pixel components ignored", () =>
            {
                using (Bitmap image = Images.White(100, 100))
                {
                    image.SetPixel(5, 5, Color.Black);
                    image.SetPixel(20, 20, Color.Black);
                    image.SetPixel(21, 21, Color.Black);
                    image.SetPixel(22, 22, Color.Black);
                    Check.Equal(Rectangle.Empty, ContentCrop.Detect(image), "Small noise must not create a label");
                }
            });
            add("crop / diagonally connected content is retained", () =>
            {
                using (Bitmap image = Images.White(100, 100))
                {
                    for (int i = 0; i < 4; i++) image.SetPixel(20 + i, 20 + i, Color.Black);
                    Check.Equal(new Rectangle(17, 17, 10, 10), ContentCrop.Detect(image), "Eight-connected four-pixel mark");
                }
            });
            add("crop / near-white threshold", () =>
            {
                using (Bitmap image = Images.White(100, 100))
                {
                    Images.Fill(image, new Rectangle(1, 1, 8, 8), Color.FromArgb(242, 242, 242));
                    Images.Fill(image, new Rectangle(50, 50, 8, 8), Color.FromArgb(241, 241, 241));
                    Check.Equal(new Rectangle(47, 47, 14, 14), ContentCrop.Detect(image), "Near-white scan background");
                }
            });
            add("crop / one label frame excludes unrelated outside marks", () =>
            {
                using (Bitmap image = Images.White(300, 400))
                {
                    Images.Frame(image, new Rectangle(50, 60, 120, 180));
                    Images.Fill(image, new Rectangle(220, 320, 20, 20), Color.Black);
                    Check.Equal(new Rectangle(47, 57, 126, 186), ContentCrop.Detect(image), "Frame priority");
                }
            });
            add("crop / nested label frames select the enclosing frame", () =>
            {
                using (Bitmap image = Images.White(300, 400))
                {
                    Images.Frame(image, new Rectangle(50, 60, 120, 180));
                    Images.Frame(image, new Rectangle(65, 80, 90, 135));
                    Check.Equal(new Rectangle(47, 57, 126, 186), ContentCrop.Detect(image), "Outer frame selection");
                }
            });
            add("crop / two separate frames retain both", () =>
            {
                using (Bitmap image = Images.White(400, 400))
                {
                    Rectangle first = new Rectangle(20, 30, 100, 150);
                    Rectangle second = new Rectangle(250, 200, 100, 150);
                    Images.Frame(image, first); Images.Frame(image, second);
                    Rectangle crop = ContentCrop.Detect(image);
                    Check.True(crop.Contains(first) && crop.Contains(second), "Ambiguous frames must not silently discard a label");
                }
            });
            add("compose / exact output size and 400 DPI", () =>
            {
                using (Bitmap source = Images.White(80, 120))
                using (Bitmap output = PdfConverter.Compose(source, new Rectangle(0, 0, 80, 120), 0))
                {
                    Check.Equal(new Size(1600, 2400), output.Size, "Output dimensions");
                    Check.Near(400, output.HorizontalResolution, 0.1, "Horizontal DPI");
                    Check.Near(400, output.VerticalResolution, 0.1, "Vertical DPI");
                }
            });
            add("compose / aspect ratio and centered white padding", () =>
            {
                using (Bitmap source = Images.White(100, 100))
                {
                    Images.Fill(source, new Rectangle(0, 0, 100, 100), Color.Black);
                    using (Bitmap output = PdfConverter.Compose(source, new Rectangle(0, 0, 100, 100), 0, 320, 480))
                    {
                        Rectangle bounds = Images.DarkBounds(output);
                        Check.Near(1, (double)bounds.Width / bounds.Height, 0.01, "Square content must stay square");
                        Check.Near(160, bounds.Left + bounds.Width / 2.0, 1, "Horizontal centering");
                        Check.Near(240, bounds.Top + bounds.Height / 2.0, 1, "Vertical centering");
                        Check.True(bounds.Left >= 4 && bounds.Top >= 4, "White output margin missing");
                        Check.White(output.GetPixel(0, 0), "Top-left padding");
                        Check.White(output.GetPixel(319, 479), "Bottom-right padding");
                    }
                }
            });
            add("compose / all quarter turns and negative turns", () =>
            {
                Color[] original = { Color.Red, Color.Lime, Color.Blue, Color.Gold };
                int[][] order = { new[] { 0, 1, 2, 3 }, new[] { 2, 0, 3, 1 },
                                  new[] { 3, 2, 1, 0 }, new[] { 1, 3, 0, 2 } };
                using (Bitmap source = Images.White(80, 80))
                {
                    for (int i = 0; i < 4; i++) Images.Fill(source, new Rectangle((i % 2) * 40, (i / 2) * 40, 40, 40), original[i]);
                    foreach (int turns in new[] { 0, 1, 2, 3, 4, 5, -1, -5 })
                    using (Bitmap output = PdfConverter.Compose(source, new Rectangle(0, 0, 80, 80), turns, 200, 200))
                        for (int i = 0; i < 4; i++)
                            Check.ColorNear(original[order[(turns % 4 + 4) % 4][i]],
                                output.GetPixel(50 + (i % 2) * 100, 50 + (i / 2) * 100), "Rotation " + turns + ", quadrant " + i);
                    Check.Equal(new Size(80, 80), source.Size, "Composition changed source dimensions");
                    Check.ColorNear(Color.Red, source.GetPixel(10, 10), "Composition changed source pixels");
                }
            });
            add("compose / invalid and out-of-range crops rejected", () =>
            {
                using (Bitmap source = Images.White(80, 120))
                foreach (Rectangle crop in new[] { Rectangle.Empty, new Rectangle(0, 0, 1, 10),
                            new Rectangle(-1, 0, 10, 10), new Rectangle(70, 110, 20, 20) })
                    Check.Throws<AppError>(() => { using (PdfConverter.Compose(source, crop, 0)) { } }, "Invalid crop " + crop);
            });
            add("jpeg / dimensions and DPI survive encoding", () =>
            {
                using (TempCase temp = new TempCase())
                using (Bitmap source = Images.White(80, 120))
                using (Bitmap composed = PdfConverter.Compose(source, new Rectangle(0, 0, 80, 120), 0))
                {
                    string output = temp.PathFor("dpi.jpg");
                    PdfConverter.SaveJpeg(composed, output);
                    using (Bitmap jpeg = new Bitmap(output))
                    {
                        Check.Equal(new Size(1600, 2400), jpeg.Size, "Encoded JPEG dimensions");
                        Check.Near(400, jpeg.HorizontalResolution, 0.1, "Encoded horizontal DPI");
                        Check.Near(400, jpeg.VerticalResolution, 0.1, "Encoded vertical DPI");
                    }
                }
            });
        }
    }
}
