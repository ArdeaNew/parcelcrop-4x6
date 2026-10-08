// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace ParcelCrop;

internal static class ContentCrop
{
	internal static Rectangle Detect(Bitmap image)
	{
		int width = image.Width;
		int height = image.Height;
		int num = checked(width * height);
		byte[] array = new byte[num];
		using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
		{
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.White);
				graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, width, height, GraphicsUnit.Pixel);
			}
			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
			try
			{
				byte[] array2 = new byte[Math.Abs(bitmapData.Stride)];
				for (int i = 0; i < height; i++)
				{
					Marshal.Copy(IntPtr.Add(bitmapData.Scan0, i * bitmapData.Stride), array2, 0, array2.Length);
					for (int j = 0; j < width; j++)
					{
						int num2 = j * 3;
						if (Math.Min(array2[num2], Math.Min(array2[num2 + 1], array2[num2 + 2])) < 242)
						{
							array[i * width + j] = 1;
						}
					}
				}
			}
			finally
			{
				bitmap.UnlockBits(bitmapData);
			}
		}
		int num3 = width;
		int num4 = height;
		int num5 = -1;
		int num6 = -1;
		List<Rectangle> frames = new List<Rectangle>();
		int[] array3 = new int[4096];
		int num7 = Math.Max(4, (int)((double)num / 2000000.0));
		for (int k = 0; k < num; k++)
		{
			if (array[k] != 1)
			{
				continue;
			}
			int num8 = 0;
			int num9 = 0;
			int num10 = width;
			int num11 = height;
			int num12 = 0;
			int num13 = 0;
			array3[num8++] = k;
			array[k] = 2;
			while (num8 > 0)
			{
				int num14 = array3[--num8];
				int num15 = num14 / width;
				int num16 = num14 % width;
				num9++;
				num10 = Math.Min(num10, num16);
				num12 = Math.Max(num12, num16);
				num11 = Math.Min(num11, num15);
				num13 = Math.Max(num13, num15);
				for (int l = -1; l <= 1; l++)
				{
					int num17 = num15 + l;
					if (num17 < 0 || num17 >= height)
					{
						continue;
					}
					for (int m = -1; m <= 1; m++)
					{
						int num18 = num16 + m;
						if (num18 < 0 || num18 >= width)
						{
							continue;
						}
						int num19 = num17 * width + num18;
						if (array[num19] == 1)
						{
							array[num19] = 2;
							if (num8 == array3.Length)
							{
								Array.Resize(ref array3, array3.Length * 2);
							}
							array3[num8++] = num19;
						}
					}
				}
			}
			if (num9 >= num7)
			{
				num3 = Math.Min(num3, num10);
				num5 = Math.Max(num5, num12);
				num4 = Math.Min(num4, num11);
				num6 = Math.Max(num6, num13);
				Rectangle rectangle = Rectangle.FromLTRB(num10, num11, num12 + 1, num13 + 1);
				double num20 = (double)Math.Min(rectangle.Width, rectangle.Height) / (double)Math.Max(rectangle.Width, rectangle.Height);
				if (rectangle.Width >= 16 && rectangle.Height >= 16 && (double)rectangle.Width > (double)width * 0.12 && (double)rectangle.Height > (double)height * 0.12 && num20 >= 0.45 && num20 <= 0.85 && HasFrame(array, width, rectangle))
				{
					frames.Add(rectangle);
				}
			}
		}
		if (num5 < num3)
		{
			return Rectangle.Empty;
		}
		List<Rectangle> list = frames.Where((Rectangle f) => !frames.Any((Rectangle other) => other != f && other.Contains(f))).ToList();
		if (list.Count == 1)
		{
			Rectangle rectangle2 = list[0];
			num3 = rectangle2.Left;
			num4 = rectangle2.Top;
			num5 = rectangle2.Right - 1;
			num6 = rectangle2.Bottom - 1;
		}
		int num21 = Math.Max(3, (int)Math.Ceiling((double)Math.Max(num5 - num3, num6 - num4) * 0.005));
		return Rectangle.FromLTRB(Math.Max(0, num3 - num21), Math.Max(0, num4 - num21), Math.Min(width, num5 + num21 + 1), Math.Min(height, num6 + num21 + 1));
	}

	private static bool HasFrame(byte[] mask, int width, Rectangle r)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = Math.Max(3, Math.Min(r.Width, r.Height) / 150);
		for (int i = r.Left; i < r.Right; i++)
		{
			bool flag = false;
			bool flag2 = false;
			for (int j = 0; j < num5; j++)
			{
				flag |= mask[(r.Top + j) * width + i] != 0;
				flag2 |= mask[(r.Bottom - 1 - j) * width + i] != 0;
			}
			if (flag)
			{
				num++;
			}
			if (flag2)
			{
				num2++;
			}
		}
		for (int k = r.Top; k < r.Bottom; k++)
		{
			bool flag3 = false;
			bool flag4 = false;
			for (int l = 0; l < num5; l++)
			{
				flag3 |= mask[k * width + r.Left + l] != 0;
				flag4 |= mask[k * width + r.Right - 1 - l] != 0;
			}
			if (flag3)
			{
				num3++;
			}
			if (flag4)
			{
				num4++;
			}
		}
		if ((double)num > (double)r.Width * 0.8 && (double)num2 > (double)r.Width * 0.8 && (double)num3 > (double)r.Height * 0.8)
		{
			return (double)num4 > (double)r.Height * 0.8;
		}
		return false;
	}
}
