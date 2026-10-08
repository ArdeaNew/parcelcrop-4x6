// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ParcelCrop;

/// <summary>Publishes completed filesystem changes to Windows Explorer.</summary>
internal static class ShellNotifications
{
	private const uint PathUnicode = 0x0005;
	private const uint FlushNoWait = 0x2000;
	private const uint NotifyRecursive = 0x10000;
	private const int Create = 0x0002;
	private const int Delete = 0x0004;
	private const int UpdateDirectory = 0x1000;

	internal static void Created(string path) => Notify(path, Create);
	internal static void Deleted(string path) => Notify(path, Delete);

	private static void Notify(string path, int change)
	{
		string fullPath = Path.GetFullPath(path);
		if (fullPath.Length < 260)
		{
			SHChangeNotifyPath(change, PathUnicode | FlushNoWait, fullPath, IntPtr.Zero);
		}
		else if (change == Create)
		{
			// SHCNF_PATH is limited to MAX_PATH. Use a shell ID list for long names.
			NotifyIdList(change, fullPath);
		}

		string directory = Path.GetDirectoryName(fullPath);
		if (directory.Length < 260)
		{
			SHChangeNotifyPath(UpdateDirectory, PathUnicode | FlushNoWait, directory, IntPtr.Zero);
		}
		else if (!NotifyIdList(UpdateDirectory, directory))
		{
			// Some shell extensions cannot parse a long path. Refresh its reachable
			// ancestor recursively; never truncate a path or notify an unrelated item.
			while (directory != null && directory.Length >= 260) directory = Path.GetDirectoryName(directory);
			if (directory != null)
			{
				SHChangeNotifyPath(UpdateDirectory, PathUnicode | FlushNoWait | NotifyRecursive, directory, IntPtr.Zero);
			}
		}
	}

	private static bool NotifyIdList(int change, string path)
	{
		IntPtr item = IntPtr.Zero;
		try
		{
			int result = SHParseDisplayName(path, IntPtr.Zero, out item, 0, out _);
			if (result < 0 || item == IntPtr.Zero) return false;
			SHChangeNotify(change, FlushNoWait, item, IntPtr.Zero);
			return true;
		}
		finally
		{
			if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item);
		}
	}

	[DllImport("shell32.dll", EntryPoint = "SHChangeNotify", CharSet = CharSet.Unicode)]
	private static extern void SHChangeNotifyPath(int eventId, uint flags, [MarshalAs(UnmanagedType.LPWStr)] string item1, IntPtr item2);

	[DllImport("shell32.dll")]
	private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
	private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr itemIdList, uint attributesIn, out uint attributesOut);
}
