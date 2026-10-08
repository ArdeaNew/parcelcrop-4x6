// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ParcelCrop;

internal sealed class LabelJob : IDisposable
{
	internal string Source;

	internal string Status = "Queued";

	internal string Detail;

	internal string Output;

	internal PreparedPage Page;

	internal Task<PreparedPage> Preparation;

	internal Rectangle Crop;

	internal int Turns;

	internal bool Finished;

	internal bool Failed;

	internal bool Removed;

	private CancellationTokenSource preparationCancellation;

	private int disposed;

	internal CancellationToken CreatePreparationToken(CancellationToken lifetimeToken)
	{
		if (Removed) throw new ObjectDisposedException(nameof(LabelJob));
		preparationCancellation?.Dispose();
		preparationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
		return preparationCancellation.Token;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0) return;
		Removed = true;
		preparationCancellation?.Cancel();
		preparationCancellation?.Dispose();
		PreparedPage currentPage = Page;
		Page = null;
		currentPage?.Dispose();
		if (Preparation != null)
		{
			// A render can finish before its UI continuation runs. Dispose its result
			// on the default scheduler even if the form/message loop has already closed.
			Preparation.ContinueWith(task =>
			{
				if (task.Status == TaskStatus.RanToCompletion && !ReferenceEquals(task.Result, currentPage))
				{
					task.Result.Dispose();
				}
				else if (task.IsFaulted)
				{
					_ = task.Exception;
				}
			}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}
	}

	public override string ToString()
	{
		return Path.GetFileName(Source) + " - " + Status;
	}
}
