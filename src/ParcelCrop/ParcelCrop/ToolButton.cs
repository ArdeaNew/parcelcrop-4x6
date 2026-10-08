// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ParcelCrop;

internal sealed class ToolButton : Button
{
	private readonly string glyph;

	internal ToolButton(string name)
	{
		glyph = name;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		float num = e.Graphics.DpiX / 96f;
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.TranslateTransform(((float)Width - 18f * num) / 2f, ((float)Height - 18f * num) / 2f);
		e.Graphics.ScaleTransform(num, num);
		using (Pen pen = new Pen(Enabled ? ForeColor : Color.FromArgb(188, 196, 194), 1.2f))
		{
			if (glyph == "Remove")
			{
				e.Graphics.DrawLine(pen, 5, 5, 13, 13);
				e.Graphics.DrawLine(pen, 13, 5, 5, 13);
			}
			else if (glyph == "Rotate")
			{
				e.Graphics.DrawArc(pen, 3, 3, 12, 12, -65, 300);
				e.Graphics.DrawLines(pen, new Point[3]
				{
					new Point(9, 1),
					new Point(12, 3),
					new Point(9, 6)
				});
			}
			else
			{
				e.Graphics.DrawLines(pen, new Point[3]
				{
					new Point(2, 7),
					new Point(2, 2),
					new Point(7, 2)
				});
				e.Graphics.DrawLines(pen, new Point[3]
				{
					new Point(11, 2),
					new Point(16, 2),
					new Point(16, 7)
				});
				e.Graphics.DrawLines(pen, new Point[3]
				{
					new Point(2, 11),
					new Point(2, 16),
					new Point(7, 16)
				});
				e.Graphics.DrawLines(pen, new Point[3]
				{
					new Point(11, 16),
					new Point(16, 16),
					new Point(16, 11)
				});
				e.Graphics.DrawRectangle(pen, 6, 6, 6, 6);
			}
		}
		e.Graphics.ResetTransform();
	}
}
