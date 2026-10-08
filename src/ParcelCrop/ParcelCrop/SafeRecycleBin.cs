// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ParcelCrop;

/// <summary>Requests recycling and refuses any permanent-delete fallback.</summary>
internal static class SafeRecycleBin
{
	internal static void Recycle(string path)
	{
		string fullPath = Path.GetFullPath(path);
		if (new DriveInfo(Path.GetPathRoot(fullPath)).DriveType != DriveType.Fixed ||
			(File.GetAttributes(fullPath) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
		{
			throw new IOException("This file cannot be safely sent to the Recycle Bin.");
		}
		if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
		{
			RecycleOnSta(fullPath);
			return;
		}
		// IFileOperation requires STA; conversions normally run on a pool (MTA) thread.
		Exception failure = null;
		Thread thread = new Thread(() =>
		{
			try { RecycleOnSta(fullPath); }
			catch (Exception error) { failure = error; }
		}) { IsBackground = true, Name = "ParcelCrop recycle" };
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
	}

	private static void RecycleOnSta(string path)
	{
		IFileOperation operation = null;
		IShellItem item = null;
		try
		{
			operation = (IFileOperation)new FileOperation();
			// Silent, no confirmation, no error UI, recycle only, stop on the first error.
			operation.SetOperationFlags(0x0004 | 0x0010 | 0x0400 | 0x00080000 | 0x00100000);
			Guid shellItemId = typeof(IShellItem).GUID;
			Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemId, out item));
			RecycleOnlyProgressSink sink = new RecycleOnlyProgressSink();
			operation.DeleteItem(item, sink);
			operation.PerformOperations();
			operation.GetAnyOperationsAborted(out bool aborted);
			GC.KeepAlive(sink);
			if (aborted || !sink.RecycleApproved || !sink.DeleteCompleted)
			{
				throw new IOException("The Recycle Bin is unavailable or recycling was cancelled. PDF kept.");
			}
		}
		finally
		{
			if (item != null) Marshal.FinalReleaseComObject(item);
			if (operation != null) Marshal.FinalReleaseComObject(operation);
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
	private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid interfaceId, out IShellItem item);

	[ComImport, Guid("3AD05575-8857-4850-9277-11B85BDB8E09"), ClassInterface(ClassInterfaceType.None)]
	private class FileOperation { }

	[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IShellItem { }

	// Method order is the native IFileOperation vtable. Unused pointer parameters
	// remain opaque; only SetOperationFlags, DeleteItem and completion are invoked.
	[ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IFileOperation
	{
		void Advise(IntPtr sink, out uint cookie);
		void Unadvise(uint cookie);
		void SetOperationFlags(uint flags);
		void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
		void SetProgressDialog(IntPtr dialog);
		void SetProperties(IntPtr properties);
		void SetOwnerWindow(IntPtr owner);
		void ApplyPropertiesToItem(IntPtr item);
		void ApplyPropertiesToItems(IntPtr items);
		void RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
		void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
		void MoveItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
		void MoveItems(IntPtr items, IntPtr destination);
		void CopyItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
		void CopyItems(IntPtr items, IntPtr destination);
		void DeleteItem(IShellItem item, IRecycleProgressSink sink);
		void DeleteItems(IntPtr items);
		void NewItem(IntPtr destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, IntPtr sink);
		void PerformOperations();
		void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
	}
}

// Explicit COM visibility is required for Windows to call the managed progress
// sink. Its only decision is to veto deletion unless the shell says it can recycle.
[ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRecycleProgressSink
{
	[PreserveSig] int StartOperations();
	[PreserveSig] int FinishOperations(int result);
	[PreserveSig] int PreRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string newName);
	[PreserveSig] int PostRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string newName, int result, IntPtr newItem);
	[PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName);
	[PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, int result, IntPtr newItem);
	[PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName);
	[PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, int result, IntPtr newItem);
	[PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
	[PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr newItem);
	[PreserveSig] int PreNewItem(uint flags, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName);
	[PreserveSig] int PostNewItem(uint flags, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IntPtr newItem);
	[PreserveSig] int UpdateProgress(uint total, uint completed);
	[PreserveSig] int ResetTimer();
	[PreserveSig] int PauseTimer();
	[PreserveSig] int ResumeTimer();
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RecycleOnlyProgressSink : IRecycleProgressSink
{
	internal bool RecycleApproved { get; private set; }
	internal bool DeleteCompleted { get; private set; }
	public int PreDeleteItem(uint flags, IntPtr item)
	{
		RecycleApproved = (flags & 0x80) != 0; // TSF_DELETE_RECYCLE_IF_POSSIBLE
		return RecycleApproved ? 0 : unchecked((int)0x80004004); // E_ABORT
	}
	public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr newItem)
	{
		DeleteCompleted = result >= 0;
		return 0;
	}
	public int StartOperations() => 0;
	public int FinishOperations(int result) => 0;
	public int PreRenameItem(uint flags, IntPtr item, string newName) => 0;
	public int PostRenameItem(uint flags, IntPtr item, string newName, int result, IntPtr newItem) => 0;
	public int PreMoveItem(uint flags, IntPtr item, IntPtr destination, string newName) => 0;
	public int PostMoveItem(uint flags, IntPtr item, IntPtr destination, string newName, int result, IntPtr newItem) => 0;
	public int PreCopyItem(uint flags, IntPtr item, IntPtr destination, string newName) => 0;
	public int PostCopyItem(uint flags, IntPtr item, IntPtr destination, string newName, int result, IntPtr newItem) => 0;
	public int PreNewItem(uint flags, IntPtr destination, string newName) => 0;
	public int PostNewItem(uint flags, IntPtr destination, string newName, string template, uint attributes, int result, IntPtr newItem) => 0;
	public int UpdateProgress(uint total, uint completed) => 0;
	public int ResetTimer() => 0;
	public int PauseTimer() => 0;
	public int ResumeTimer() => 0;
}
