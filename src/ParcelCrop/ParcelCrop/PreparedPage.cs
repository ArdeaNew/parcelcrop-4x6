// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace ParcelCrop;

internal sealed class PreparedPage : IDisposable
{
	internal string DirectoryPath;

	internal string RasterPath;

	internal string SourceHash;

	internal Size PageSize;

	internal Rectangle AutoCrop;

	internal bool IsBlank;

	internal Bitmap LoadThumbnail()
	{
		using FileStream raster = File.OpenRead(RasterPath);
		using Bitmap bitmap = new Bitmap(raster);
		double num = Math.Min(1.0, 1500.0 / (double)Math.Max(bitmap.Width, bitmap.Height));
		Bitmap bitmap2 = new Bitmap(Math.Max(1, (int)((double)bitmap.Width * num)), Math.Max(1, (int)((double)bitmap.Height * num)));
		using (Graphics graphics = Graphics.FromImage(bitmap2))
		{
			graphics.Clear(Color.White);
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.DrawImage(bitmap, new Rectangle(Point.Empty, bitmap2.Size));
		}
		return bitmap2;
	}

	public void Dispose()
	{
		if (DirectoryPath != null)
		{
			try
			{
				Directory.Delete(DirectoryPath, recursive: true);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			DirectoryPath = null;
		}
	}
}
