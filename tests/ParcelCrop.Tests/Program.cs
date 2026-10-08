// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace ParcelCrop.Tests
{
    internal static class Program
    {
        private sealed class Test
        {
            internal string Name;
            internal Action Run;
        }

        internal static string SelfPath { get { return typeof(Program).Assembly.Location; } }

        [STAThread]
        private static int Main(string[] args)
        {
            // The runner doubles as a deterministic child-process renderer for process tests.
            if (args.Length > 0 && (args[0] == "show" || args[0] == "draw")) return FakeRenderer(args);
            string renderer = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--renderer" && i + 1 < args.Length)
                {
                    renderer = Path.GetFullPath(args[++i]);
                    if (!File.Exists(renderer))
                    {
                        Console.Error.WriteLine("Renderer does not exist: " + renderer);
                        return 2;
                    }
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    Console.WriteLine("ParcelCrop.Tests.exe [--renderer <absolute-path-to-mutool.exe>]");
                    Console.WriteLine("No argument: image, conversion and fake-renderer process regression tests.");
                    Console.WriteLine("--renderer: additionally test generated single-page, multi-page, blank and damaged PDFs with real MuPDF.");
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Unknown or incomplete argument: " + args[i]);
                    return 2;
                }
            }

            List<Test> tests = new List<Test>();
            Action<string, Action> add = (name, action) => tests.Add(new Test { Name = name, Run = action });
            ImageTests.Register(add);
            ConversionTests.Register(add);
            RendererTests.Register(add);
            UiLifecycleTests.Register(add);
            if (renderer != null) RendererTests.RegisterIntegration(add, renderer);

            int passed = 0, failed = 0;
            Stopwatch elapsed = Stopwatch.StartNew();
            foreach (Test test in tests)
            {
                try
                {
                    test.Run();
                    passed++;
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL " + test.Name);
                    Console.Error.WriteLine(error);
                }
            }
            if (renderer == null) Console.WriteLine("SKIP real MuPDF integration (supply --renderer to enable; 4 tests)");
            Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed, " +
                              (renderer == null ? "4" : "0") + " skipped; " + elapsed.Elapsed.TotalSeconds.ToString("F2") + "s");
            return failed == 0 ? 0 : 1;
        }

        private static int FakeRenderer(string[] args)
        {
            try
            {
                string input = null;
                foreach (string arg in args)
                    if (arg.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(arg)) input = arg;
                if (input == null) return 70;
                string[] fixture = File.ReadAllText(input).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string mode = fixture[0].Replace("PARCELCROP-TEST:", "");
                if (mode == "missing-dll") return unchecked((int)0xC0000135);
                if (mode == "bad-image") return unchecked((int)0xC000007B);
                if (mode == "hang")
                {
                    if (fixture.Length > 1) File.WriteAllText(fixture[1], Process.GetCurrentProcess().Id.ToString());
                    Thread.Sleep(10000);
                    return 71;
                }
                if (mode == "damaged")
                {
                    Console.Error.WriteLine("Synthetic renderer failure: damaged PDF");
                    return 1;
                }
                if (args[0] == "show")
                {
                    if (mode == "invalid-count") Console.WriteLine("not-a-page-count");
                    else Console.WriteLine(mode == "multi" ? "2" : "1");
                    return 0;
                }
                string destination = null;
                for (int i = 1; i < args.Length - 1; i++) if (args[i] == "-o") destination = args[i + 1];
                if (destination == null) return 72;
                if (mode == "no-raster") return 0;
                using (Bitmap bitmap = Images.White(200, 300))
                {
                    if (mode != "blank") Images.Fill(bitmap, new Rectangle(40, 60, 100, 160), Color.Black);
                    bitmap.Save(destination, ImageFormat.Png);
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 73;
            }
        }
    }
}
