// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ParcelCrop;

internal sealed class PdfConverter
{
	private static readonly SemaphoreSlim RenderGate = new SemaphoreSlim(1, 1);
	private readonly string explicitRenderer;
	private readonly Action<string> notifyCreated;
	private readonly Action<string> recycleFile;
	private readonly Action<string> notifyDeleted;

	// File-system side effects are injectable so tests never need the real Recycle Bin.
	internal PdfConverter(string renderer = null, Action<string> notifyCreated = null,
		Action<string> recycleFile = null, Action<string> notifyDeleted = null)
	{
		explicitRenderer = renderer;
		this.notifyCreated = notifyCreated ?? ShellNotifications.Created;
		this.recycleFile = recycleFile ?? SafeRecycleBin.Recycle;
		this.notifyDeleted = notifyDeleted ?? ShellNotifications.Deleted;
	}

	internal async Task<PreparedPage> PrepareAsync(string source, CancellationToken token)
	{
		await RenderGate.WaitAsync(token).ConfigureAwait(false);
		try
		{
			return await Task.Run(() => Prepare(source, token), token).ConfigureAwait(false);
		}
		finally
		{
			RenderGate.Release();
		}
	}

	internal PreparedPage Prepare(string source, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		source = ValidateSource(source);
		string mutoolPath = RendererPathResolver.Resolve(explicitRenderer, AppDomain.CurrentDomain.BaseDirectory,
			Environment.GetEnvironmentVariable("PARCELCROP_MUTOOL"));
		if (!File.Exists(mutoolPath))
		{
			throw new AppError("The PDF component is missing. Extract the complete Windows package again. PDF kept.",
				AppErrorKind.ComponentUnavailable);
		}
		RendererPathResolver.ValidateExecutable(mutoolPath);
		PreparedPage page = new PreparedPage
		{
			DirectoryPath = Path.Combine(Path.GetTempPath(), "parcelcrop", Guid.NewGuid().ToString("N"))
		};
		try
		{
			Directory.CreateDirectory(page.DirectoryPath);
			string input = Path.Combine(page.DirectoryPath, "input.pdf");
			using (FileStream stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				page.SourceHash = Hash(stream);
				stream.Position = 0;
				using FileStream destination = File.Create(input);
				stream.CopyTo(destination);
			}
			token.ThrowIfCancellationRequested();
			if (!int.TryParse(RunTool(mutoolPath, "show " + Quote(input) + " trailer/Root/Pages/Count", token).Trim(), out int count) || count < 1)
			{
				throw new AppError("Cannot read this PDF. PDF kept.");
			}
			if (count != 1)
			{
				throw new AppError("Multiple pages found. Split into single-page PDFs first. PDF kept.");
			}
			page.RasterPath = Path.Combine(page.DirectoryPath, "page.png");
			RunTool(mutoolPath, "draw -q -F png -c rgb -r 300 -w 5000 -h 5000 -o " + Quote(page.RasterPath) + " " + Quote(input) + " 1", token);
			if (!File.Exists(page.RasterPath))
			{
				throw new AppError("Cannot render this PDF. PDF kept.");
			}
			using (FileStream raster = File.OpenRead(page.RasterPath))
			using (Bitmap bitmap = new Bitmap(raster))
			{
				page.PageSize = bitmap.Size;
				page.AutoCrop = ContentCrop.Detect(bitmap);
				page.IsBlank = page.AutoCrop.IsEmpty;
			}
			token.ThrowIfCancellationRequested();
			return page;
		}
		catch
		{
			page.Dispose();
			throw;
		}
	}

	private static string RunTool(string mutoolPath, string arguments, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		using Process process = StartRenderer(mutoolPath, arguments);
		if (process == null)
		{
			throw new AppError("The PDF component could not start. Extract the complete Windows package again. PDF kept.",
				AppErrorKind.ComponentUnavailable);
		}
		Task<string> stdout = process.StandardOutput.ReadToEndAsync();
		Task<string> stderr = process.StandardError.ReadToEndAsync();
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			while (!process.WaitForExit(100))
			{
				token.ThrowIfCancellationRequested();
				if (stopwatch.Elapsed.TotalSeconds > 120)
				{
					throw new TimeoutException("PDF reading timed out. PDF kept.");
				}
			}
			token.ThrowIfCancellationRequested();
			Task.WaitAll(stdout, stderr);
			if (process.ExitCode != 0)
			{
				uint exitCode = unchecked((uint)process.ExitCode);
				if (exitCode == 0xC0000135 || exitCode == 0xC000007B || exitCode == 0xC0000142 || exitCode == 0xC0000139)
				{
					throw new AppError("The PDF component could not load. Extract the complete Windows package again. PDF kept.",
						AppErrorKind.ComponentUnavailable);
				}
				throw new AppError("Cannot read this PDF. It may be damaged or password protected. PDF kept.");
			}
			return stdout.Result;
		}
		finally
		{
			// A renderer can exit between HasExited and Kill; do not mask cancellation.
			try
			{
				if (!process.HasExited)
				{
					process.Kill();
					process.WaitForExit(5000);
				}
			}
			catch (InvalidOperationException) { }
			catch (System.ComponentModel.Win32Exception) { }
		}
	}

	private static Process StartRenderer(string mutoolPath, string arguments)
	{
		try
		{
			return Process.Start(new ProcessStartInfo(mutoolPath, arguments)
			{
				UseShellExecute = false,
				ErrorDialog = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = Path.GetDirectoryName(mutoolPath)
			});
		}
		catch (System.ComponentModel.Win32Exception)
		{
			throw new AppError("The PDF component could not start. Extract the complete Windows package again and check file permissions. PDF kept.",
				AppErrorKind.ComponentUnavailable);
		}
	}

	internal ConversionResult Convert(string source, PreparedPage page, Rectangle crop, int turns, bool recycleSource = false)
	{
		source = ValidateSource(source);
		if (page == null) throw new ArgumentNullException(nameof(page));
		string output = Path.ChangeExtension(source, ".jpg");
		string temporary = Path.Combine(Path.GetDirectoryName(output), ".parcelcrop-" + Guid.NewGuid().ToString("N") + ".tmp");
		try
		{
			if (File.Exists(output) || Directory.Exists(output))
			{
				throw new AppError("A JPG with this name already exists. Move or rename it first. PDF kept.");
			}
			if (page.IsBlank)
			{
				throw new AppError("No label found on this page. PDF kept.");
			}
			using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				VerifySource(input, page);
				using (FileStream raster = File.OpenRead(page.RasterPath))
				using (Bitmap rendered = new Bitmap(raster))
				using (Bitmap composed = Compose(rendered, crop, turns))
				{
					if (ContentCrop.Detect(composed).IsEmpty)
					{
						throw new AppError("No content inside the crop. PDF kept.");
					}
					SaveJpeg(composed, temporary);
				}
				// Streams avoid GDI+'s filename-length limit. Dispose before publishing.
				using (FileStream imageFile = File.OpenRead(temporary))
				using (Bitmap image = new Bitmap(imageFile))
				{
					if (image.Width != OutputSpec.Width || image.Height != OutputSpec.Height ||
						Math.Abs(image.HorizontalResolution - OutputSpec.Dpi) > 0.1 ||
						Math.Abs(image.VerticalResolution - OutputSpec.Dpi) > 0.1)
					{
						throw new AppError("JPG verification failed. PDF kept.");
					}
				}
				// Same-directory rename is atomic and refuses an intervening collision.
				File.Move(temporary, output);
			}

			ConversionResult result = new ConversionResult { OutputPath = output };
			// Explorer sees only the final, verified JPG after every file handle is closed.
			Notify(notifyCreated, output, result);
			if (recycleSource)
			{
				try
				{
					using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						VerifySource(input, page);
					}
					recycleFile(source);
					result.Recycled = !File.Exists(source);
					if (result.Recycled) Notify(notifyDeleted, source, result);
					else result.AddWarning("JPG saved. PDF could not be moved to the Recycle Bin.");
				}
				catch (Exception)
				{
					if (File.Exists(source))
					{
						result.AddWarning("JPG saved. PDF kept: it changed, is in use, or the Recycle Bin is unavailable.");
					}
					else
					{
						result.AddWarning("JPG saved. PDF recycling could not be confirmed. Check its folder and the Recycle Bin.");
						Notify(notifyDeleted, source, result);
					}
				}
			}
			return result;
		}
		finally
		{
			// Cleanup errors must not replace the original conversion error or a success.
			try { if (File.Exists(temporary)) File.Delete(temporary); }
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private static void Notify(Action<string> notification, string path, ConversionResult result)
	{
		try { notification(path); }
		catch (Exception) { result.AddWarning("JPG saved. Windows could not refresh the folder view; press F5 if needed."); }
	}

	private static string ValidateSource(string source)
	{
		if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A PDF path is required.", nameof(source));
		string fullPath = Path.GetFullPath(source);
		if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
		{
			throw new AppError("Only PDF files are supported. Source file kept.");
		}
		return fullPath;
	}

	private static void VerifySource(Stream source, PreparedPage page)
	{
		if (!string.Equals(Hash(source), page.SourceHash, StringComparison.Ordinal))
		{
			throw new AppError("The PDF changed after preview. Remove it from the list and add it again. PDF kept.");
		}
	}

	internal static Bitmap Compose(Bitmap source, Rectangle crop, int turns, int width = OutputSpec.Width, int height = OutputSpec.Height)
	{
		if (source == null) throw new ArgumentNullException(nameof(source));
		if (crop.Width < 2 || crop.Height < 2 || !new Rectangle(Point.Empty, source.Size).Contains(crop))
		{
			throw new AppError("Invalid crop bounds. Detect the label again.");
		}
		int border = Math.Max(1, (int)Math.Round((double)OutputSpec.Border * width / OutputSpec.Width));
		if (width <= 2 * border || height <= 2 * border)
		{
			throw new ArgumentOutOfRangeException(nameof(width), "Output dimensions must leave room for the label and border.");
		}
		using Bitmap cropped = source.Clone(crop, PixelFormat.Format24bppRgb);
		for (int i = 0; i < (turns % 4 + 4) % 4; i++) cropped.RotateFlip(RotateFlipType.Rotate90FlipNone);
		Bitmap result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		try
		{
			result.SetResolution(OutputSpec.Dpi, OutputSpec.Dpi);
			using (Graphics graphics = Graphics.FromImage(result))
			using (ImageAttributes attributes = new ImageAttributes())
			{
				graphics.Clear(Color.White);
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				attributes.SetWrapMode(WrapMode.TileFlipXY);
				graphics.SetClip(new Rectangle(border, border, width - 2 * border, height - 2 * border));
				double scale = Math.Min((double)(width - 2 * border) / cropped.Width, (double)(height - 2 * border) / cropped.Height);
				int scaledWidth = Math.Max(1, (int)Math.Round(cropped.Width * scale));
				int scaledHeight = Math.Max(1, (int)Math.Round(cropped.Height * scale));
				graphics.DrawImage(cropped, new Rectangle((width - scaledWidth) / 2, (height - scaledHeight) / 2, scaledWidth, scaledHeight),
					0, 0, cropped.Width, cropped.Height, GraphicsUnit.Pixel, attributes);
			}
			return result;
		}
		catch { result.Dispose(); throw; }
	}

	internal static void SaveJpeg(Bitmap bitmap, string destination)
	{
		ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
		using EncoderParameters parameters = new EncoderParameters(1);
		parameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
		using FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		bitmap.Save(output, encoder, parameters);
		output.Flush(true);
	}

	private static string Quote(string path)
	{
		if (path.IndexOf('"') >= 0) throw new ArgumentException("Invalid quote in file path.", nameof(path));
		return "\"" + path + "\"";
	}

	private static string Hash(Stream stream)
	{
		using SHA256 algorithm = SHA256.Create();
		return BitConverter.ToString(algorithm.ComputeHash(stream));
	}
}
