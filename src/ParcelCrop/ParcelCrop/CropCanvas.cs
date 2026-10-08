// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ParcelCrop;

internal sealed class CropCanvas : Control
{
	internal Bitmap PageImage;

	internal Size PageSize;

	internal Rectangle Crop;

	internal bool Editable;

	internal string EmptyText = "Add PDF files";

	private Bitmap outputImage;

	private RectangleF pageDisplay;

	private PointF anchor;

	private Rectangle initial;

	private Size ratio;

	private int dragMode;

	private bool dragging;

	private int turns;

	private readonly Timer previewTimer = new Timer
	{
		Interval = 40
	};

	private float DeviceScale
	{
		get
		{
			using Graphics graphics = CreateGraphics();
			return graphics.DpiX / 96f;
		}
	}

	internal event EventHandler CropChanged;

	internal CropCanvas()
	{
		DoubleBuffered = true;
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		BackColor = Color.FromArgb(247, 249, 248);
		TabStop = false;
		AccessibleName = "Full page crop and output preview";
		Timer timer = previewTimer;
		EventHandler value = delegate
		{
			previewTimer.Stop();
			RenderOutput();
		};
		timer.Tick += value;
	}

	internal void SetPage(Bitmap image, Size size, Rectangle crop, Size? aspect = null)
	{
		previewTimer.Stop();
		dragging = false;
		Capture = false;
		if (PageImage != null)
		{
			PageImage.Dispose();
		}
		PageImage = image;
		PageSize = size;
		Crop = crop;
		ratio = aspect ?? crop.Size;
		ClearOutput();
		Invalidate();
	}

	internal void ClearOutput()
	{
		if (outputImage != null)
		{
			outputImage.Dispose();
		}
		outputImage = null;
	}

	internal void ShowOutput(int rotation)
	{
		turns = rotation;
		previewTimer.Stop();
		RenderOutput();
	}

	private void RenderOutput()
	{
		ClearOutput();
		if (PageImage != null && Crop.Width > 0)
		{
			double num = (double)PageImage.Width / (double)PageSize.Width;
			double num2 = (double)PageImage.Height / (double)PageSize.Height;
			Rectangle crop = Rectangle.FromLTRB((int)((double)Crop.Left * num), (int)((double)Crop.Top * num2), Math.Min(PageImage.Width, (int)Math.Ceiling((double)Crop.Right * num)), Math.Min(PageImage.Height, (int)Math.Ceiling((double)Crop.Bottom * num2)));
			// A valid full-resolution crop can round down to one pixel in the thumbnail.
			// Skip that preview instead of throwing from the UI timer.
			if (crop.Width >= 2 && crop.Height >= 2)
			{
				outputImage = PdfConverter.Compose(PageImage, crop, turns, 640, 960);
			}
		}
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		float num = e.Graphics.DpiX / 96f;
		if (PageImage == null)
		{
			Rectangle bounds = new Rectangle(16, Height / 2 - (int)(66f * num), Width - 32, (int)(32f * num));
			using (Font font = new Font(Font.FontFamily, 16f, FontStyle.Regular))
			{
				TextRenderer.DrawText(e.Graphics, EmptyText, font, bounds, Color.FromArgb(74, 87, 87), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			if (EmptyText == "Add PDF files")
			{
				TextRenderer.DrawText(bounds: new Rectangle(16, Height / 2 - (int)(26f * num), Width - 32, (int)(28f * num)), dc: e.Graphics, text: "Drop PDFs here, or browse a folder.", font: Font, foreColor: Color.FromArgb(114, 125, 127), flags: TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			return;
		}
		e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		int num2 = (int)(22f * num);
		int num3 = Width / 2;
		int num4 = (int)(52f * num);
		TextRenderer.DrawText(e.Graphics, "CROP", Font, new Rectangle(num2, (int)(12f * num), num3 - num2, (int)(24f * num)), Color.FromArgb(79, 96, 95), TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(e.Graphics, "OUTPUT", Font, new Rectangle(num3 + num2, (int)(12f * num), num3 - num2, (int)(24f * num)), Color.FromArgb(79, 96, 95), TextFormatFlags.VerticalCenter);
		RectangleF area = new RectangleF(num2, num4, num3 - 2 * num2, Height - num4 - num2);
		RectangleF rectangleF = new RectangleF(num3 + num2, num4, Width - num3 - 2 * num2, Height - num4 - num2);
		pageDisplay = Fit(PageImage.Size, area);
		DrawPaper(e.Graphics, PageImage, pageDisplay);
		if (outputImage != null)
		{
			DrawPaper(e.Graphics, outputImage, Fit(outputImage.Size, rectangleF));
		}
		else
		{
			TextRenderer.DrawText(e.Graphics, "No label detected", Font, Rectangle.Round(rectangleF), Color.FromArgb(126, 134, 137), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
		if (Crop.IsEmpty)
		{
			return;
		}
		RectangleF rectangleF2 = ToDisplay(Crop);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, 75, 85, 84)))
		{
			e.Graphics.FillRectangle(brush, pageDisplay.Left, pageDisplay.Top, pageDisplay.Width, Math.Max(0f, rectangleF2.Top - pageDisplay.Top));
			e.Graphics.FillRectangle(brush, pageDisplay.Left, rectangleF2.Bottom, pageDisplay.Width, Math.Max(0f, pageDisplay.Bottom - rectangleF2.Bottom));
			e.Graphics.FillRectangle(brush, pageDisplay.Left, rectangleF2.Top, Math.Max(0f, rectangleF2.Left - pageDisplay.Left), rectangleF2.Height);
			e.Graphics.FillRectangle(brush, rectangleF2.Right, rectangleF2.Top, Math.Max(0f, pageDisplay.Right - rectangleF2.Right), rectangleF2.Height);
		}
		using (Pen pen = new Pen(Color.FromArgb(38, 123, 124), num))
		{
			e.Graphics.DrawRectangle(pen, rectangleF2.X, rectangleF2.Y, rectangleF2.Width, rectangleF2.Height);
		}
		if (!Editable)
		{
			return;
		}
		float num5 = 5f * num;
		PointF[] array = new PointF[4]
		{
			new PointF(rectangleF2.Left, rectangleF2.Top),
			new PointF(rectangleF2.Right, rectangleF2.Top),
			new PointF(rectangleF2.Left, rectangleF2.Bottom),
			new PointF(rectangleF2.Right, rectangleF2.Bottom)
		};
		for (int i = 0; i < array.Length; i++)
		{
			PointF pointF = array[i];
			e.Graphics.FillRectangle(Brushes.White, pointF.X - num5 / 2f, pointF.Y - num5 / 2f, num5, num5);
			using Pen pen2 = new Pen(Color.FromArgb(38, 123, 124), num);
			e.Graphics.DrawRectangle(pen2, pointF.X - num5 / 2f, pointF.Y - num5 / 2f, num5, num5);
		}
	}

	private static RectangleF Fit(Size image, RectangleF area)
	{
		float num = Math.Max(0.01f, Math.Min(area.Width / (float)image.Width, area.Height / (float)image.Height));
		return new RectangleF(area.Left + (area.Width - (float)image.Width * num) / 2f, area.Top + (area.Height - (float)image.Height * num) / 2f, (float)image.Width * num, (float)image.Height * num);
	}

	private static void DrawPaper(Graphics g, Bitmap image, RectangleF bounds)
	{
		g.DrawImage(image, bounds);
		using Pen pen = new Pen(Color.FromArgb(218, 225, 223));
		g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
	}

	private RectangleF ToDisplay(Rectangle rect)
	{
		return new RectangleF(pageDisplay.Left + (float)rect.X * pageDisplay.Width / (float)PageSize.Width, pageDisplay.Top + (float)rect.Y * pageDisplay.Height / (float)PageSize.Height, (float)rect.Width * pageDisplay.Width / (float)PageSize.Width, (float)rect.Height * pageDisplay.Height / (float)PageSize.Height);
	}

	private PointF ToPage(Point p)
	{
		return new PointF(Math.Max(0f, Math.Min(PageSize.Width, ((float)p.X - pageDisplay.Left) * (float)PageSize.Width / pageDisplay.Width)), Math.Max(0f, Math.Min(PageSize.Height, ((float)p.Y - pageDisplay.Top) * (float)PageSize.Height / pageDisplay.Height)));
	}

	private int Hit(Point p)
	{
		RectangleF rectangleF = ToDisplay(Crop);
		float num = 9f * DeviceScale;
		if (Math.Abs((float)p.X - rectangleF.Left) < num && Math.Abs((float)p.Y - rectangleF.Top) < num)
		{
			return 5;
		}
		if (Math.Abs((float)p.X - rectangleF.Right) < num && Math.Abs((float)p.Y - rectangleF.Top) < num)
		{
			return 6;
		}
		if (Math.Abs((float)p.X - rectangleF.Left) < num && Math.Abs((float)p.Y - rectangleF.Bottom) < num)
		{
			return 9;
		}
		if (Math.Abs((float)p.X - rectangleF.Right) < num && Math.Abs((float)p.Y - rectangleF.Bottom) < num)
		{
			return 10;
		}
		if (!rectangleF.Contains(p))
		{
			return 0;
		}
		return 16;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (Editable && PageImage != null && !Crop.IsEmpty && e.Button == MouseButtons.Left)
		{
			dragMode = Hit(e.Location);
			if (dragMode != 0)
			{
				initial = Crop;
				anchor = ToPage(e.Location);
				dragging = true;
				Capture = true;
			}
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (!Editable || PageImage == null || Crop.IsEmpty)
		{
			Cursor = Cursors.Default;
			return;
		}
		if (!dragging)
		{
			Cursor cursor;
			switch (Hit(e.Location))
			{
			default:
				cursor = Cursors.Default;
				break;
			case 6:
			case 9:
				cursor = Cursors.SizeNESW;
				break;
			case 5:
			case 10:
				cursor = Cursors.SizeNWSE;
				break;
			case 16:
				cursor = Cursors.SizeAll;
				break;
			}
			Cursor = cursor;
			return;
		}
		PointF pointF = ToPage(e.Location);
		int num = (int)(pointF.X - anchor.X);
		int num2 = (int)(pointF.Y - anchor.Y);
		Rectangle rectangle;
		if (dragMode == 16)
		{
			rectangle = new Rectangle(Math.Max(0, Math.Min(PageSize.Width - initial.Width, initial.Left + num)), Math.Max(0, Math.Min(PageSize.Height - initial.Height, initial.Top + num2)), initial.Width, initial.Height);
		}
		else
		{
			bool flag = (dragMode & 1) != 0;
			bool flag2 = (dragMode & 4) != 0;
			int num3 = (flag ? initial.Right : initial.Left);
			int num4 = (flag2 ? initial.Bottom : initial.Top);
			double num5 = initial.Width + (flag ? (-num) : num);
			double num6 = initial.Height + (flag2 ? (-num2) : num2);
			double val = (num5 * (double)ratio.Width + num6 * (double)ratio.Height) / ((double)ratio.Width * (double)ratio.Width + (double)ratio.Height * (double)ratio.Height);
			double val2 = Math.Min((double)(flag ? num3 : (PageSize.Width - num3)) / (double)ratio.Width, (double)(flag2 ? num4 : (PageSize.Height - num4)) / (double)ratio.Height);
			val = Math.Max(Math.Min(val2, Math.Max(12.0 / (double)ratio.Width, 12.0 / (double)ratio.Height)), Math.Min(val2, val));
			int num7 = Math.Max(2, (int)Math.Round((double)ratio.Width * val));
			int num8 = Math.Max(2, (int)Math.Round((double)ratio.Height * val));
			rectangle = new Rectangle(flag ? (num3 - num7) : num3, flag2 ? (num4 - num8) : num4, num7, num8);
		}
		if (rectangle != Crop)
		{
			Crop = rectangle;
			Invalidate();
			if (!previewTimer.Enabled)
			{
				previewTimer.Start();
			}
			if (CropChanged != null)
			{
				CropChanged(this, EventArgs.Empty);
			}
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (dragging)
		{
			dragging = false;
			Capture = false;
			previewTimer.Stop();
			RenderOutput();
		}
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		base.OnMouseCaptureChanged(e);
		if (!Capture && dragging)
		{
			// Alt+Tab or another window can consume mouse-up after taking capture.
			// End the gesture here so later mouse movement cannot silently edit a crop.
			dragging = false;
			previewTimer.Stop();
			RenderOutput();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			dragging = false;
			Capture = false;
			previewTimer.Dispose();
			if (PageImage != null)
			{
				PageImage.Dispose();
				PageImage = null;
			}
			ClearOutput();
		}
		base.Dispose(disposing);
	}
}
