// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;

namespace ParcelCrop;

internal static class RendererPathResolver
{
	internal static string Resolve(string explicitPath, string applicationDirectory, string configuredPath)
	{
		// An explicit caller selection is deliberate and must never silently switch engines.
		if (!string.IsNullOrWhiteSpace(explicitPath))
		{
			try { return Path.GetFullPath(explicitPath); }
			catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
			{
				throw new AppError("The PDF component path is invalid. PDF kept.", AppErrorKind.ComponentUnavailable);
			}
		}
		string bundled = Path.Combine(applicationDirectory, "mutool.exe");
		// The verified component shipped with the application wins over stale machine settings.
		if (File.Exists(bundled)) return bundled;
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			try
			{
				string configured = Path.GetFullPath(configuredPath);
				if (File.Exists(configured)) return configured;
			}
			catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException) { }
		}
		return bundled;
	}

	internal static void ValidateExecutable(string path)
	{
		// Reject obvious damaged/unsupported files before CreateProcess can display a blocking OS dialog.
		try
		{
			using FileStream stream = File.OpenRead(path);
			using BinaryReader reader = new BinaryReader(stream);
			if (stream.Length < 64 || reader.ReadUInt16() != 0x5A4D) throw InvalidComponent();
			stream.Position = 0x3C;
			uint header = reader.ReadUInt32();
			if (header < 64 || header > stream.Length - 26) throw InvalidComponent();
			stream.Position = header;
			if (reader.ReadUInt32() != 0x00004550) throw InvalidComponent();
			ushort machine = reader.ReadUInt16();
			if (machine != 0x8664 && machine != 0x014C) throw InvalidComponent();
			stream.Position = header + 20;
			ushort optionalSize = reader.ReadUInt16();
			ushort characteristics = reader.ReadUInt16();
			if ((characteristics & 0x0002) == 0 || (characteristics & 0x2000) != 0 ||
				optionalSize < 96 || header + 24L + optionalSize > stream.Length) throw InvalidComponent();
			ushort magic = reader.ReadUInt16();
			if (magic != (machine == 0x8664 ? 0x020B : 0x010B)) throw InvalidComponent();
		}
		catch (AppError) { throw; }
		catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
		{
			throw new AppError("The PDF component cannot be opened. Extract the complete Windows package again and check file permissions. PDF kept.",
				AppErrorKind.ComponentUnavailable);
		}
	}

	private static AppError InvalidComponent() => new AppError(
		"The PDF component is damaged or incompatible. Extract the complete Windows package again. PDF kept.",
		AppErrorKind.ComponentUnavailable);
}
