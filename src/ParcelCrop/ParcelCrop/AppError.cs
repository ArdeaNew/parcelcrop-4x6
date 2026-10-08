// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;

namespace ParcelCrop;

internal enum AppErrorKind
{
	General,
	ComponentUnavailable
}

internal sealed class AppError : IOException
{
	internal AppErrorKind Kind { get; }

	internal AppError(string message, AppErrorKind kind = AppErrorKind.General)
		: base(message)
	{
		Kind = kind;
	}

	internal static string PreviewTitle(Exception error)
	{
		return error is AppError appError && appError.Kind == AppErrorKind.ComponentUnavailable
			? "PDF component unavailable" : "Preview unavailable";
	}

	internal static string Describe(Exception error)
	{
		if (error is AppError)
		{
			return error.Message;
		}
		if (error is UnauthorizedAccessException)
		{
			return "Access denied. Check folder permissions. PDF kept.";
		}
		if (error is FileNotFoundException || error is DirectoryNotFoundException)
		{
			return "File or folder not found. Add the PDF again.";
		}
		if (error is TimeoutException)
		{
			return "PDF reading timed out. PDF kept.";
		}
		if (error is IOException)
		{
			return "Cannot read or write this file. Check permissions and available space. PDF kept.";
		}
		return "Cannot complete this operation. Try again. PDF kept.";
	}
}
