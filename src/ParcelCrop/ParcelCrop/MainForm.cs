// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParcelCrop;

internal sealed class MainForm : Form
{
	private static readonly Color Ink = Color.FromArgb(44, 54, 57);

	private static readonly Color Muted = Color.FromArgb(113, 123, 127);

	private static readonly Color Accent = Color.FromArgb(31, 119, 120);

	internal readonly List<LabelJob> Jobs = new List<LabelJob>();

	internal readonly CropCanvas Canvas;

	internal readonly ListBox FileQueue;

	internal readonly Button StartButton;

	internal Task PreviewTask = Task.FromResult(0);

	internal Task BatchTask = Task.FromResult(0);

	internal Task ShutdownTask = Task.FromResult(0);

	private readonly PdfConverter converter;

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private readonly HashSet<Task> activePreparations = new HashSet<Task>();

	private readonly ToolTip tips = new ToolTip();

	private readonly Label heading;

	private readonly Label specification;

	private readonly Label queueHeading;

	private readonly Label status;

	private readonly Label note;

	private readonly Label details;

	private readonly Button addButton;

	private readonly Button browseButton;

	private readonly Button removeButton;

	private readonly Button clearButton;

	private readonly Button autoButton;

	private readonly Button rotateButton;

	private readonly Button openButton;

	private readonly CheckBox recycleSource;

	private bool busy;

	private bool stopRequested;

	private bool closing;

	private bool waitingForPreviews;

	private int selectionVersion;

	private float uiScale = 1f;

	private LabelJob Selected => FileQueue.SelectedItem as LabelJob;

	private bool HasPending => Jobs.Any((LabelJob j) => !j.Finished);

	internal MainForm(PdfConverter service = null)
	{
		converter = service ?? new PdfConverter();
		Text = "ParcelCrop 4x6";
		BackColor = Color.White;
		ForeColor = Ink;
		Font = new Font("Consolas", 9f, FontStyle.Regular);
		AutoScaleMode = AutoScaleMode.None;
		DoubleBuffered = true;
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		StartPosition = FormStartPosition.CenterScreen;
		using (Graphics graphics = CreateGraphics())
		{
			uiScale = graphics.DpiX / 96f;
		}
		Rectangle workingArea = Screen.FromControl(this).WorkingArea;
		ClientSize = new Size(Math.Min(S(1160), workingArea.Width - S(48)), Math.Min(S(720), workingArea.Height - S(64)));
		MinimumSize = new Size(Math.Min(S(960), workingArea.Width), Math.Min(S(620), workingArea.Height));
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		heading = MakeLabel("Shipping labels", 17f, Ink);
		specification = MakeLabel("4 x 6 in  /  1600 x 2400 px  /  400 dpi", 9f, Muted);
		queueHeading = MakeLabel("FILES", 9f, Ink);
		status = MakeLabel("", 9f, Ink);
		note = MakeLabel("JPGs save beside the PDFs. Original PDFs are kept by default.", 8.5f, Muted);
		recycleSource = new CheckBox
		{
			Text = "Move PDF to Recycle Bin after successful export",
			AccessibleName = "Recycle original PDF after successful export",
			Checked = false,
			ForeColor = Muted,
			Font = new Font("Consolas", 8.5f),
			AutoSize = false
		};
		details = MakeLabel("", 8.5f, Muted);
		details.AutoEllipsis = true;
		addButton = MakeButton("Add files", primary: false);
		browseButton = MakeButton("Browse files", primary: true);
		removeButton = MakeIcon("Remove", "Remove the selected file from the list");
		clearButton = MakeButton("Clear", primary: false);
		autoButton = MakeIcon("Detect", "Detect label bounds");
		rotateButton = MakeIcon("Rotate", "Rotate output clockwise");
		autoButton.BackColor = (rotateButton.BackColor = Color.FromArgb(247, 249, 248));
		openButton = MakeButton("Open folder", primary: false);
		StartButton = MakeButton("Process all", primary: true);
		StartButton.Enabled = false;
		StartButton.AccessibleName = "Process all PDFs";
		tips.SetToolTip(clearButton, "Clear the list. Source files are not deleted.");
		tips.SetToolTip(addButton, "Add multiple PDFs from a folder");
		tips.SetToolTip(browseButton, "Choose one or more PDF files");
		tips.OwnerDraw = true;
		tips.Popup += (object sender, PopupEventArgs e) =>
		{
			Size size = TextRenderer.MeasureText(tips.GetToolTip(e.AssociatedControl), Font, new Size(S(520), 0), TextFormatFlags.WordBreak);
			e.ToolTipSize = new Size(size.Width + S(20), size.Height + S(14));
		};
		tips.Draw += (object sender, DrawToolTipEventArgs e) =>
		{
			e.Graphics.Clear(Color.FromArgb(248, 250, 249));
			using (Pen pen = new Pen(Color.FromArgb(218, 225, 223)))
			{
				e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
			}
			TextRenderer.DrawText(e.Graphics, e.ToolTipText, Font, Rectangle.Inflate(e.Bounds, -S(8), -S(5)), Ink, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
		};
		Canvas = new CropCanvas
		{
			Font = new Font("Consolas", 9f, FontStyle.Regular)
		};
		FileQueue = new ListBox
		{
			BorderStyle = BorderStyle.None,
			DrawMode = DrawMode.OwnerDrawFixed,
			ItemHeight = S(66),
			IntegralHeight = false,
			BackColor = Color.White,
			SelectionMode = SelectionMode.One,
			AccessibleName = "PDF files"
		};
		Controls.AddRange(new Control[17]
		{
			heading, specification, queueHeading, status, note, details, addButton, removeButton, clearButton, autoButton,
			rotateButton, openButton, StartButton, Canvas, FileQueue, browseButton, recycleSource
		});
		Canvas.Controls.AddRange(new Control[3] { browseButton, autoButton, rotateButton });
		addButton.Click += ChooseFiles;
		browseButton.Click += ChooseFiles;
		removeButton.Click += delegate
		{
			RemoveSelected();
		};
		clearButton.Click += delegate
		{
			ClearQueue();
		};
		autoButton.Click += delegate
		{
			ResetCrop();
		};
		rotateButton.Click += delegate
		{
			LabelJob selected = Selected;
			if (selected != null && selected.Page != null && !busy && !selected.Finished)
			{
				selected.Turns = (selected.Turns + 1) % 4;
				Canvas.ShowOutput(selected.Turns);
			}
		};
		openButton.Click += delegate
		{
			OpenFolder();
		};
		StartButton.Click += delegate
		{
			if (busy)
			{
				stopRequested = true;
				StartButton.Text = "Stopping...";
				StartButton.Enabled = false;
			}
			else
			{
				BatchTask = StartProcessingAsync();
			}
		};
		Canvas.CropChanged += delegate
		{
			LabelJob selected = Selected;
			if (selected != null && !busy)
			{
				selected.Crop = Canvas.Crop;
			}
		};
		FileQueue.SelectedIndexChanged += delegate
		{
			PreviewTask = ShowSelectedAsync();
		};
		FileQueue.DrawItem += DrawQueueItem;
		FileQueue.MouseMove += (object sender, MouseEventArgs e) =>
		{
			int num = FileQueue.IndexFromPoint(e.Location);
			if (num >= 0 && num < FileQueue.Items.Count)
			{
				LabelJob labelJob = (LabelJob)FileQueue.Items[num];
				tips.SetToolTip(FileQueue, labelJob.Source + Environment.NewLine + (labelJob.Detail ?? labelJob.Status));
			}
		};
		BindDrop(this);
		Shown += delegate
		{
			Arrange();
			UpdateControls();
		};
		Resize += delegate
		{
			Arrange();
		};
		FormClosing += OnClosing;
		Arrange();
		UpdateControls();
	}

	private int S(int value)
	{
		return (int)Math.Round((float)value * uiScale);
	}

	private Label MakeLabel(string text, float size, Color color)
	{
		return new Label
		{
			Text = text,
			Font = new Font("Consolas", size, FontStyle.Regular),
			ForeColor = color,
			AutoSize = false,
			TextAlign = ContentAlignment.MiddleLeft
		};
	}

	private Button MakeButton(string text, bool primary)
	{
		Button button = new Button();
		button.Text = text;
		button.FlatStyle = FlatStyle.Flat;
		button.Cursor = Cursors.Hand;
		button.Font = new Font("Consolas", 9f, FontStyle.Regular);
		button.ForeColor = (primary ? Color.White : Muted);
		button.BackColor = (primary ? Accent : Color.White);
		button.UseVisualStyleBackColor = false;
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.MouseOverBackColor = (primary ? Color.FromArgb(25, 103, 104) : Color.FromArgb(240, 245, 244));
		button.FlatAppearance.MouseDownBackColor = (primary ? Color.FromArgb(21, 88, 90) : Color.FromArgb(226, 237, 235));
		return button;
	}

	private Button MakeIcon(string glyph, string hint)
	{
		ToolButton toolButton = new ToolButton(glyph)
		{
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand,
			ForeColor = Muted,
			BackColor = Color.White,
			Font = Font,
			AccessibleName = hint
		};
		toolButton.FlatAppearance.BorderSize = 0;
		toolButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(234, 241, 238);
		tips.SetToolTip(toolButton, hint);
		return toolButton;
	}

	private void Arrange()
	{
		if (Canvas != null)
		{
			int num = ClientSize.Width;
			int num2 = ClientSize.Height;
			int num3 = S(32);
			int num4 = S(24);
			int num5 = num - num3 * 2;
			int num6 = S(246);
			int num7 = ((Jobs.Count > 0) ? (num5 - num4 - num6) : num5);
			int num8 = num3 + num7 + num4;
			heading.SetBounds(num3, S(21), num5, S(37));
			specification.SetBounds(num3, S(61), num5, S(23));
			int num9 = S(111);
			int num10 = num2 - S(120);
			Canvas.SetBounds(num3, num9, num7, Math.Max(S(240), num10 - num9));
			autoButton.SetBounds(num7 / 2 - S(89), S(7), S(30), S(34));
			rotateButton.SetBounds(num7 / 2 - S(55), S(7), S(30), S(34));
			queueHeading.SetBounds(num8, num9 + S(6), num6 - S(92), S(32));
			clearButton.SetBounds(num - num3 - S(87), num9 + S(6), S(53), S(32));
			removeButton.SetBounds(num - num3 - S(28), num9 + S(6), S(28), S(32));
			int num11 = num9 + S(52);
			FileQueue.SetBounds(num8, num11, num6, Math.Max(S(130), num10 - num11 - S(88)));
			details.SetBounds(num8 + S(6), num10 - S(78), num6 - S(12), S(36));
			openButton.SetBounds(num8 - S(6), num10 - S(33), S(111), S(33));
			addButton.SetBounds(num - num3 - S(105), num10 - S(33), S(105), S(33));
			browseButton.SetBounds((num7 - S(142)) / 2, Canvas.Height / 2 + S(19), S(142), S(38));
			status.SetBounds(num3, num2 - S(91), num5 - S(200), S(26));
			note.SetBounds(num3, num2 - S(65), num5 - S(200), S(22));
			recycleSource.SetBounds(num3, num2 - S(40), num5 - S(200), S(24));
			StartButton.SetBounds(num - num3 - S(170), num2 - S(77), S(170), S(45));
			Invalidate(invalidateChildren: true);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (Canvas == null)
		{
			return;
		}
		using Pen pen = new Pen(Color.FromArgb(234, 238, 237));
		e.Graphics.DrawLine(pen, S(32), ClientSize.Height - S(103), ClientSize.Width - S(32), ClientSize.Height - S(103));
	}

	private void UpdateControls()
	{
		if (!IsDisposed && !closing)
		{
			LabelJob selected = Selected;
			bool flag = Jobs.Count > 0;
			bool flag2 = selected != null && selected.Page != null && !selected.Page.IsBlank;
			StartButton.Visible = flag;
			bool flag3 = selected != null && selected.Preparation != null && !selected.Preparation.IsCompleted;
			StartButton.Enabled = (busy ? (!stopRequested) : (HasPending && !flag3));
			Button startButton = StartButton;
			string text;
			if (busy)
			{
				text = (stopRequested ? "Stopping..." : "Stop after current");
			}
			else
			{
				text = ((HasPending && Jobs.Where((LabelJob j) => !j.Finished).All((LabelJob j) => j.Failed)) ? "Retry unfinished" : "Process all");
			}
			startButton.Text = text;
			Button button = addButton;
			Button button2 = browseButton;
			bool flag4 = (clearButton.Enabled = !busy);
			bool flag6 = flag4;
			flag4 = (button2.Enabled = flag6);
			bool enabled = flag4;
			button.Enabled = enabled;
			removeButton.Enabled = !busy && selected != null;
			recycleSource.Enabled = !busy;
			Button button3 = autoButton;
			flag4 = (rotateButton.Enabled = flag2 && !busy && !selected.Finished);
			bool enabled2 = flag4;
			button3.Enabled = enabled2;
			Button button4 = autoButton;
			flag4 = (rotateButton.Visible = Canvas.PageImage != null);
			bool visible = flag4;
			button4.Visible = visible;
			browseButton.Visible = !flag;
			Label label = queueHeading;
			ListBox fileQueue = FileQueue;
			Button button5 = clearButton;
			Button button6 = removeButton;
			Button button7 = addButton;
			flag4 = (details.Visible = flag);
			bool flag11 = flag4;
			flag4 = (button7.Visible = flag11);
			bool flag13 = flag4;
			flag4 = (button6.Visible = flag13);
			bool flag15 = flag4;
			flag4 = (button5.Visible = flag15);
			bool flag17 = flag4;
			flag4 = (fileQueue.Visible = flag17);
			bool visible2 = flag4;
			label.Visible = visible2;
			Canvas.Editable = flag2 && !busy && !selected.Finished;
			queueHeading.Text = "FILES (" + Jobs.Count + ")";
			openButton.Visible = flag && selected != null;
			openButton.Enabled = selected != null && Directory.Exists(Path.GetDirectoryName(selected.Source));
			details.Text = ((selected == null) ? "" : (selected.Detail ?? Path.GetDirectoryName(selected.Source)));
			if (selected != null)
			{
				tips.SetToolTip(details, selected.Detail ?? Path.GetDirectoryName(selected.Source));
			}
			FileQueue.Invalidate();
			Canvas.Invalidate();
			Arrange();
		}
	}

	private void DrawQueueItem(object sender, DrawItemEventArgs e)
	{
		if (e.Index < 0 || e.Index >= FileQueue.Items.Count)
		{
			return;
		}
		LabelJob labelJob = (LabelJob)FileQueue.Items[e.Index];
		using (SolidBrush brush = new SolidBrush(((e.State & DrawItemState.Selected) != 0) ? Color.FromArgb(235, 244, 242) : Color.White))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		Rectangle bounds = new Rectangle(e.Bounds.Left + S(12), e.Bounds.Top + S(9), e.Bounds.Width - S(24), S(24));
		TextRenderer.DrawText(e.Graphics, Path.GetFileName(labelJob.Source), Font, bounds, Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		Rectangle rectangle = new Rectangle(bounds.Left, bounds.Bottom + S(2), bounds.Width, S(20));
		using Font font = new Font("Consolas", 8.5f);
		Graphics graphics = e.Graphics;
		string text = labelJob.Status;
		Font font2 = font;
		Rectangle bounds2 = rectangle;
		Color foreColor;
		if (labelJob.Failed)
		{
			foreColor = Color.FromArgb(173, 72, 58);
		}
		else
		{
			foreColor = (labelJob.Finished ? Accent : Muted);
		}
		TextRenderer.DrawText(graphics, text, font2, bounds2, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
	}

	private void ChooseFiles(object sender, EventArgs e)
	{
		if (busy)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "PDF files (*.pdf)|*.pdf",
			Multiselect = true,
			Title = "Add PDF files"
		};
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddFiles(openFileDialog.FileNames);
		}
	}

	internal void AddFiles(IEnumerable<string> paths)
	{
		if (busy || closing)
		{
			return;
		}
		LabelJob labelJob = null;
		foreach (string path in paths)
		{
			if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string full = Path.GetFullPath(path);
			if (!Jobs.Any((LabelJob j) => string.Equals(j.Source, full, StringComparison.OrdinalIgnoreCase)))
			{
				LabelJob labelJob2 = new LabelJob
				{
					Source = full
				};
				Jobs.Add(labelJob2);
				FileQueue.Items.Add(labelJob2);
				if (labelJob == null)
				{
					labelJob = labelJob2;
				}
			}
		}
		if (labelJob != null)
		{
			status.Text = "";
			FileQueue.SelectedItem = labelJob;
		}
		UpdateControls();
	}

	private async Task<PreparedPage> EnsurePageAsync(LabelJob job)
	{
		if (job.Removed || closing) throw new OperationCanceledException();
		if (job.Page != null)
		{
			return job.Page;
		}
		if (job.Preparation == null)
		{
			job.Status = "Reading...";
			job.Preparation = converter.PrepareAsync(job.Source, job.CreatePreparationToken(lifetime.Token));
			activePreparations.Add(job.Preparation);
		}
		Task<PreparedPage> preparation = job.Preparation;
		try
		{
			PreparedPage preparedPage = await preparation;
			if (job.Removed || closing)
			{
				// LabelJob owns cleanup, including successful work that finishes after removal.
				job.Dispose();
				throw new OperationCanceledException();
			}
			if (job.Page == null)
			{
				job.Page = preparedPage;
				job.Crop = preparedPage.AutoCrop;
				job.Turns = ((job.Crop.Width > job.Crop.Height) ? 1 : 0);
				job.Status = (preparedPage.IsBlank ? "Blank page" : "Ready");
				if (preparedPage.IsBlank)
				{
					job.Failed = true;
					job.Detail = "No content on the first page. PDF kept.";
				}
			}
			return preparedPage;
		}
		finally
		{
			activePreparations.Remove(preparation);
		}
	}

	private async Task ShowSelectedAsync()
	{
		int version = ++selectionVersion;
		LabelJob job = Selected;
		Canvas.SetPage(null, Size.Empty, Rectangle.Empty);
		Canvas.EmptyText = ((job == null) ? "Add PDF files" : "Loading preview...");
		UpdateControls();
		if (job == null)
		{
			Canvas.Invalidate();
			return;
		}
		EnsureUiContext();
		try
		{
			await EnsurePageAsync(job);
			if (version == selectionVersion && !closing && !job.Removed)
			{
				Canvas.SetPage(job.Page.LoadThumbnail(), job.Page.PageSize, job.Crop, job.Page.AutoCrop.Size);
				Canvas.EmptyText = "";
				Canvas.ShowOutput(job.Turns);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception error)
		{
			job.Failed = true;
			job.Status = "Preview failed";
			job.Detail = AppError.Describe(error);
			if (version == selectionVersion && !closing)
			{
				Canvas.EmptyText = "Cannot open this PDF";
			}
		}
		finally
		{
			if (version == selectionVersion && !closing)
			{
				UpdateControls();
				Canvas.Invalidate();
			}
		}
	}

	private void ResetCrop()
	{
		LabelJob selected = Selected;
		if (selected != null && selected.Page != null && !busy && !selected.Finished)
		{
			selected.Crop = selected.Page.AutoCrop;
			Canvas.Crop = selected.Crop;
			Canvas.ShowOutput(selected.Turns);
		}
	}

	private void RemoveSelected()
	{
		if (!busy && Selected != null)
		{
			LabelJob selected = Selected;
			int selectedIndex = FileQueue.SelectedIndex;
			Jobs.Remove(selected);
			FileQueue.Items.Remove(selected);
			selected.Dispose();
			if (FileQueue.Items.Count > 0)
			{
				FileQueue.SelectedIndex = Math.Min(selectedIndex, FileQueue.Items.Count - 1);
			}
			status.Text = "";
			UpdateControls();
		}
	}

	private void ClearQueue()
	{
		if (busy)
		{
			return;
		}
		selectionVersion++;
		foreach (LabelJob job in Jobs)
		{
			job.Dispose();
		}
		Jobs.Clear();
		FileQueue.Items.Clear();
		Canvas.SetPage(null, Size.Empty, Rectangle.Empty);
		Canvas.EmptyText = "Add PDF files";
		status.Text = "";
		UpdateControls();
	}

	internal async Task StartProcessingAsync()
	{
		if (busy || !HasPending)
		{
			return;
		}
		busy = true;
		stopRequested = false;
		int successes = 0;
		int failures = 0;
		int warnings = 0;
		bool shouldRecycle = recycleSource.Checked;
		List<LabelJob> batch = Jobs.Where((LabelJob j) => !j.Finished).ToList();
		UpdateControls();
		EnsureUiContext();
		try
		{
			for (int i = 0; i < batch.Count && !stopRequested && !closing; UpdateControls(), i++)
			{
				LabelJob job = batch[i];
				try
				{
					if (job.Preparation != null && job.Preparation.IsFaulted)
					{
						job.Preparation = null;
					}
					job.Failed = false;
					job.Detail = null;
					status.Text = "Processing " + (i + 1) + " / " + batch.Count;
					await EnsurePageAsync(job);
					lifetime.Token.ThrowIfCancellationRequested();
					if (stopRequested)
					{
						break;
					}
					job.Status = "Saving...";
					UpdateControls();
					ConversionResult conversionResult = await Task.Run(() => converter.Convert(job.Source, job.Page, job.Crop, job.Turns, shouldRecycle));
					job.Finished = true;
					job.Output = conversionResult.OutputPath;
					job.Status = (conversionResult.Recycled ? "Done / PDF recycled" : "Saved / PDF kept");
					job.Detail = conversionResult.Warning;
					successes++;
					if (!string.IsNullOrEmpty(conversionResult.Warning))
					{
						warnings++;
					}
					if (job == Selected && !closing)
					{
						Canvas.ShowOutput(job.Turns);
					}
					continue;
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception error)
				{
					failures++;
					job.Failed = true;
					job.Status = "Not processed";
					job.Detail = AppError.Describe(error);
					continue;
				}
				break;
			}
			if (!closing)
			{
				status.Text = (stopRequested ? "Stopped. " : "") + successes + " completed" + ((failures > 0) ? (" / " + failures + " failed") : "") + ((warnings > 0) ? (" / " + warnings + " warnings") : "");
			}
		}
		finally
		{
			busy = false;
			UpdateControls();
			if (closing)
			{
				Close();
			}
		}
	}

	private static void EnsureUiContext()
	{
		if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
		{
			SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
		}
	}

	private void OpenFolder()
	{
		LabelJob selected = Selected;
		if (selected == null)
		{
			return;
		}
		string text = (File.Exists(selected.Output) ? selected.Output : selected.Source);
		try
		{
			Process.Start("explorer.exe", "/select,\"" + text + "\"");
		}
		catch (Exception)
		{
			details.Text = "Cannot open this folder.";
		}
	}

	private void BindDrop(Control target)
	{
		target.AllowDrop = true;
		target.DragEnter += (object sender, DragEventArgs e) =>
		{
			string[] array = e.Data.GetData(DataFormats.FileDrop) as string[];
			e.Effect = ((!busy && array != null && array.Any((string p) => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))) ? DragDropEffects.Copy : DragDropEffects.None);
		};
		target.DragDrop += (object sender, DragEventArgs e) =>
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
			{
				AddFiles(paths);
			}
		};
		foreach (Control control in target.Controls)
		{
			BindDrop(control);
		}
	}

	private void OnClosing(object sender, FormClosingEventArgs e)
	{
		closing = true;
		stopRequested = true;
		lifetime.Cancel();
		if (busy)
		{
			e.Cancel = true;
			return;
		}
		foreach (LabelJob job in Jobs)
		{
			job.Dispose();
		}
		// Keep the UI message loop alive until cancelled renderers have exited.
		// Removed jobs remain tracked here until their preparations finish as well.
		Task[] pending = activePreparations.Where(task => !task.IsCompleted).ToArray();
		if (pending.Length > 0)
		{
			e.Cancel = true;
			if (!waitingForPreviews)
			{
				waitingForPreviews = true;
				EnsureUiContext();
				ShutdownTask = FinishClosingAsync(pending);
			}
		}
	}

	private async Task FinishClosingAsync(Task[] pending)
	{
		// Avoid a reentrant Close inside FormClosing if cancellation completes inline.
		await Task.Yield();
		try { await Task.WhenAll(pending); }
		catch (Exception) { /* Preparation owns its cleanup; cancellation/failure is expected. */ }
		finally
		{
			waitingForPreviews = false;
			if (!IsDisposed) Close();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			closing = true;
			lifetime.Cancel();
			foreach (LabelJob job in Jobs)
			{
				job.Dispose();
			}
			tips.Dispose();
		}
		base.Dispose(disposing);
	}
}
