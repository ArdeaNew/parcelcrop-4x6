// SPDX-License-Identifier: AGPL-3.0-or-later
namespace ParcelCrop;

internal sealed class ConversionResult
{
	internal string OutputPath;

	internal string Warning;

	internal bool Recycled;

	internal void AddWarning(string warning)
	{
		Warning = string.IsNullOrEmpty(Warning) ? warning : Warning + " " + warning;
	}
}
