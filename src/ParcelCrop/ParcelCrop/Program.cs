// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Windows.Forms;

namespace ParcelCrop;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		using MainForm window = new MainForm();
		if (args.Length > 0)
		{
			window.Shown += (sender, e) => window.AddFiles(args);
		}
		Application.Run(window);
	}
}
