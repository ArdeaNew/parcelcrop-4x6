// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParcelCrop.Tests
{
    internal static class UiLifecycleTests
    {
        internal static void Register(Action<string, Action> add)
        {
            add("crop canvas / sub-two-pixel thumbnail crop does not throw", () =>
            {
                using (CropCanvas canvas = new CropCanvas())
                {
                    canvas.SetPage(Images.White(100, 100), new Size(5000, 5000), new Rectangle(0, 0, 2, 2));
                    foreach (Rectangle crop in new[] { new Rectangle(0, 0, 2, 2), new Rectangle(4998, 4998, 2, 2),
                                new Rectangle(100, 100, 2, 500), new Rectangle(100, 100, 500, 2) })
                    {
                        canvas.Crop = crop;
                        canvas.ShowOutput(0);
                        canvas.ShowOutput(1);
                        Check.Equal(crop, canvas.Crop, "Preview rounding changed full-resolution crop");
                    }
                    canvas.Crop = new Rectangle(0, 0, 5000, 5000);
                    canvas.ShowOutput(0); // A later normal crop must still produce a preview without stale errors.
                }
            });
            add("job disposal / pending preparation is cancelled and late result cleaned", () =>
            {
                using (TempCase temp = new TempCase())
                using (LabelJob job = new LabelJob())
                {
                    CancellationToken token = job.CreatePreparationToken(CancellationToken.None);
                    TaskCompletionSource<PreparedPage> pending = new TaskCompletionSource<PreparedPage>();
                    job.Preparation = pending.Task;
                    PreparedPage page = temp.Page(temp.Pdf());
                    string rasterDirectory = page.DirectoryPath;
                    job.Dispose();
                    Check.True(token.IsCancellationRequested && job.Removed, "Disposed job did not cancel preparation");
                    pending.SetResult(page);
                    Check.True(SpinWait.SpinUntil(() => !Directory.Exists(rasterDirectory), 5000), "Late prepared raster leaked");
                    Check.True(job.Page == null, "Disposed job retained a page");
                }
            });
            add("job disposal / completed task before UI assignment is cleaned", () =>
            {
                using (TempCase temp = new TempCase())
                using (LabelJob job = new LabelJob())
                {
                    PreparedPage page = temp.Page(temp.Pdf());
                    string rasterDirectory = page.DirectoryPath;
                    job.Preparation = Task.FromResult(page);
                    job.Dispose();
                    Check.True(SpinWait.SpinUntil(() => !Directory.Exists(rasterDirectory), 5000), "Completed but unassigned preview leaked");
                }
            });
            add("job disposal / assigned and prepared same page is safely released once", () =>
            {
                using (TempCase temp = new TempCase())
                using (LabelJob job = new LabelJob())
                {
                    PreparedPage page = temp.Page(temp.Pdf());
                    string rasterDirectory = page.DirectoryPath;
                    job.Page = page;
                    job.Preparation = Task.FromResult(page);
                    job.Dispose(); job.Dispose();
                    Check.True(!Directory.Exists(rasterDirectory) && job.Page == null, "Assigned preview remained after disposal");
                }
            });
            add("job disposal / faulted and cancelled preparation can be disposed", () =>
            {
                TaskCompletionSource<PreparedPage> faulted = new TaskCompletionSource<PreparedPage>();
                faulted.SetException(new IOException("Synthetic preparation failure"));
                TaskCompletionSource<PreparedPage> cancelled = new TaskCompletionSource<PreparedPage>();
                cancelled.SetCanceled();
                foreach (Task<PreparedPage> task in new[] { faulted.Task, cancelled.Task })
                using (LabelJob job = new LabelJob())
                {
                    job.Preparation = task;
                    job.Dispose(); job.Dispose();
                    Check.True(job.Removed && job.Page == null, "Failed preparation disposal did not finish");
                }
            });
            add("job cancellation / per-job cancellation is isolated and lifetime cancellation is linked", () =>
            {
                using (CancellationTokenSource lifetime = new CancellationTokenSource())
                using (LabelJob first = new LabelJob())
                using (LabelJob second = new LabelJob())
                {
                    CancellationToken firstToken = first.CreatePreparationToken(lifetime.Token);
                    CancellationToken secondToken = second.CreatePreparationToken(lifetime.Token);
                    first.Dispose();
                    Check.True(firstToken.IsCancellationRequested, "Removed job preparation was not cancelled");
                    Check.True(!secondToken.IsCancellationRequested && !lifetime.IsCancellationRequested,
                        "Removing one job cancelled another job or the window lifetime");
                    lifetime.Cancel();
                    Check.True(secondToken.IsCancellationRequested, "Window lifetime did not cancel remaining job");
                }
            });
            add("job cancellation / disposed job refuses new preparation", () =>
            {
                using (LabelJob job = new LabelJob())
                {
                    job.Dispose();
                    Check.Throws<ObjectDisposedException>(() => job.CreatePreparationToken(CancellationToken.None),
                        "Disposed job accepted new preparation");
                }
            });
            add("crop canvas / losing capture ends drag before later mouse moves", () =>
            {
                using (CropCanvas canvas = new CropCanvas())
                {
                    Rectangle original = new Rectangle(100, 200, 600, 900);
                    canvas.SetPage(Images.White(100, 150), new Size(1000, 1500), original);
                    canvas.Editable = true;
                    // Set the display transform without a visible desktop window or native input injection.
                    typeof(CropCanvas).GetField("pageDisplay", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(canvas, new RectangleF(0, 0, 1000, 1500));
                    Invoke(canvas, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 400, 650, 0));
                    Check.True((bool)typeof(CropCanvas).GetField("dragging", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(canvas), "Synthetic mouse-down did not enter drag mode");
                    canvas.Capture = false;
                    Invoke(canvas, "OnMouseCaptureChanged", EventArgs.Empty);
                    Invoke(canvas, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, 500, 750, 0));
                    Check.Equal(original, canvas.Crop, "Crop continued moving after mouse capture was lost");
                }
            });
            add("form closing / waits for active renderer before ending message loop", () => CloseDuringRendering(false));
            add("form closing / cleared job renderer is also awaited", () => CloseDuringRendering(true));
        }

        private static void CloseDuringRendering(bool clearQueue)
        {
            using (TempCase temp = new TempCase())
            using (MainForm form = new MainForm(new PdfConverter(Program.SelfPath)))
            using (System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer { Interval = 20 })
            {
                // The real WinForms message loop is exercised with an invisible, unlisted window.
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                string source = temp.FakePdf("hang");
                string marker = temp.PathFor("renderer.pid");
                File.AppendAllText(source, "\n" + marker);
                LabelJob job = null;
                int processId = 0;
                bool requestedClose = false, firstCloseCancelled = false, closed = false;
                bool completedAtClose = false, exitedAtClose = false;
                Exception loopError = null;
                Stopwatch elapsed = Stopwatch.StartNew();
                form.FormClosing += (sender, arguments) =>
                {
                    if (!requestedClose) return;
                    if (!firstCloseCancelled) firstCloseCancelled = arguments.Cancel;
                };
                form.FormClosed += (sender, arguments) =>
                {
                    closed = true;
                    completedAtClose = job != null && job.Preparation != null && job.Preparation.IsCompleted;
                    exitedAtClose = processId > 0 && !IsRunning(processId);
                    poll.Stop();
                };
                form.Shown += (sender, arguments) =>
                {
                    form.AddFiles(new[] { source });
                    job = form.Jobs[0];
                    poll.Start();
                };
                poll.Tick += (sender, arguments) =>
                {
                    try
                    {
                        if (elapsed.Elapsed.TotalSeconds > 7) throw new TimeoutException("Form did not complete shutdown within seven seconds");
                        if (requestedClose) return;
                        try
                        {
                            if (File.Exists(marker)) int.TryParse(File.ReadAllText(marker), out processId);
                        }
                        catch (IOException) { }
                        if (processId == 0) return;
                        Check.True(job.Preparation != null && !job.Preparation.IsCompleted, "Delayed renderer was not active before close");
                        requestedClose = true;
                        if (clearQueue) Invoke(form, "ClearQueue");
                        form.Close();
                    }
                    catch (Exception error)
                    {
                        loopError = error;
                        poll.Stop();
                        form.Dispose();
                        Application.ExitThread();
                    }
                };
                try { Application.Run(form); }
                finally
                {
                    poll.Stop();
                    if (job != null)
                    {
                        job.Dispose();
                        if (job.Preparation != null)
                        {
                            try { job.Preparation.GetAwaiter().GetResult()?.Dispose(); }
                            catch (OperationCanceledException) { }
                        }
                    }
                }
                if (loopError != null) throw new InvalidOperationException("Window lifecycle test failed", loopError);
                Check.True(requestedClose && firstCloseCancelled, "Initial close did not wait for pending rendering");
                Check.True(closed && completedAtClose, "FormClosed ran before preparation finished");
                Check.True(exitedAtClose, "FormClosed ran while external renderer was still running");
                Check.True(form.ShutdownTask.IsCompleted, "ShutdownTask was incomplete after message loop exited");
                Check.True(File.Exists(source), "Closing window removed the PDF");
            }
        }

        private static bool IsRunning(int processId)
        {
            try { using (Process process = Process.GetProcessById(processId)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Check.True(info != null, "Missing test event method " + method);
            info.Invoke(target, arguments);
        }
    }
}
