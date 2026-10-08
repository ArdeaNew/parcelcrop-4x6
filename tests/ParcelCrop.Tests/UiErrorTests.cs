// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParcelCrop.Tests
{
    internal static class UiErrorTests
    {
        internal static void Register(Action<string, Action> add)
        {
            add("error presentation / missing component is not described as a damaged PDF", () =>
            {
                AppError error = new AppError("Extract the complete package again.", AppErrorKind.ComponentUnavailable);
                Check.Equal("PDF component unavailable", AppError.PreviewTitle(error), "Wrong component failure title");
                Check.Equal(error.Message, AppError.Describe(error), "Actionable explanation was lost");
                Check.True(!AppError.Describe(new InvalidOperationException()).Contains("damaged"),
                    "Unknown errors incorrectly accuse the PDF of being damaged");
            });
            add("preview recovery / restore component and retry without adding files again", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    string renderer = temp.PathFor("restored-renderer.exe");
                    RunForm(new PdfConverter(renderer, notifyCreated: path => { }), async form =>
                    {
                        string source = temp.FakePdf("valid");
                        form.AddFiles(new[] { source });
                        await form.PreviewTask;
                        LabelJob job = form.Jobs[0];
                        Check.True(job.Failed && job.Page == null, "Missing renderer did not fail preparation");
                        Check.Equal("PDF component unavailable", form.Canvas.EmptyText, "Preview blamed the PDF");
                        Check.Equal(job.Detail, form.Details.Text, "Details omitted part of the error");
                        Check.True(form.Details.Multiline && form.Details.ReadOnly && form.Details.WordWrap &&
                            form.Details.ScrollBars == ScrollBars.Vertical, "Full details are not readable and scrollable");
                        Check.True(form.RetryPreviewButton.Visible && form.RetryPreviewButton.Enabled,
                            "Failed preview has no available retry action");

                        // Install the deterministic test renderer; no customer PDF or download is used.
                        File.Copy(Program.SelfPath, renderer);
                        File.Copy(typeof(PdfConverter).Assembly.Location, temp.PathFor("ParcelCrop.exe"));
                        if (File.Exists(Program.SelfPath + ".config"))
                            File.Copy(Program.SelfPath + ".config", renderer + ".config");
                        form.RetryPreviewButton.PerformClick();
                        await form.PreviewTask;
                        Check.Equal(1, form.Jobs.Count, "Recovery changed the queue");
                        Check.True(!job.Failed && job.Detail == null && job.Page != null,
                            "Successful retry retained the old failure state");
                        Check.True(form.Canvas.PageImage != null && !form.RetryPreviewButton.Visible,
                            "Successful retry did not restore the preview");
                        await form.StartProcessingAsync();
                        Check.True(job.Finished && File.Exists(Path.ChangeExtension(source, ".jpg")),
                            "Recovered file could not be exported");
                        Check.True(File.Exists(source), "Recovery removed the source PDF");
                    });
                }
            });
            add("batch recovery / one failed PDF does not block later queue items", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    RunForm(new PdfConverter(Program.SelfPath, notifyCreated: path => { }), async form =>
                    {
                        string damaged = temp.FakePdf("damaged");
                        string valid = temp.FakePdf("valid");
                        form.AddFiles(new[] { damaged, valid });
                        await form.PreviewTask;
                        await form.StartProcessingAsync();
                        Check.True(form.Jobs[0].Failed && !form.Jobs[0].Finished,
                            "Damaged fixture did not remain an unfinished failure");
                        Check.True(form.Jobs[1].Finished && File.Exists(Path.ChangeExtension(valid, ".jpg")),
                            "A preceding failure blocked a valid queue item");
                        Check.True(!File.Exists(Path.ChangeExtension(damaged, ".jpg")), "Failed file produced a JPG");
                        Check.True(form.StartButton.Enabled && form.StartButton.Text == "Retry unfinished",
                            "Batch did not return to a usable retry state");
                        Check.True(File.Exists(damaged) && File.Exists(valid), "Batch deleted source files");
                    });
                }
            });
            add("preview recovery / selecting a repaired file clears a stale failed task", () =>
            {
                using (TempCase temp = new TempCase())
                {
                    RunForm(new PdfConverter(Program.SelfPath), async form =>
                    {
                        string repaired = temp.FakePdf("damaged");
                        string valid = temp.FakePdf("valid");
                        form.AddFiles(new[] { repaired, valid });
                        await form.PreviewTask;
                        Check.True(form.Jobs[0].Failed, "Fixture did not fail initially");
                        File.WriteAllText(repaired, "PARCELCROP-TEST:valid");
                        form.FileQueue.SelectedIndex = 1;
                        await form.PreviewTask;
                        form.FileQueue.SelectedIndex = 0;
                        await form.PreviewTask;
                        Check.True(!form.Jobs[0].Failed && form.Jobs[0].Detail == null &&
                            form.Jobs[0].Page != null && form.Canvas.PageImage != null,
                            "Reselecting a repaired file reused the failed task or old status");
                    });
                }
            });
        }

        private static void RunForm(PdfConverter converter, Func<MainForm, Task> scenario)
        {
            using (MainForm form = new MainForm(converter) { ShowInTaskbar = false, Opacity = 0 })
            using (Timer timeout = new Timer { Interval = 15000 })
            {
                Exception failure = null;
                bool completed = false;
                timeout.Tick += (sender, args) =>
                {
                    timeout.Stop();
                    failure = new TimeoutException("UI recovery test did not complete.");
                    form.Close();
                };
                form.Shown += async (sender, args) =>
                {
                    timeout.Start();
                    try { await scenario(form); completed = true; }
                    catch (Exception error) { failure = error; }
                    finally { timeout.Stop(); form.Close(); }
                };
                Application.Run(form);
                if (failure != null) throw new InvalidOperationException("UI recovery scenario failed.", failure);
                Check.True(completed, "UI recovery message loop ended before the scenario completed");
            }
        }
    }
}
