// SPDX-License-Identifier: AGPL-3.0-or-later
namespace ParcelCrop;

internal static class OutputSpec
{
	// 1600 x 2400 at 400 DPI is a 4 x 6 inch canvas (2:3 aspect ratio).
	// Cropped content is fitted proportionally, centered, and never stretched.
	internal const int Width = 1600;

	internal const int Height = 2400;

	internal const int Dpi = 400;

	internal const int Border = 24;
}
