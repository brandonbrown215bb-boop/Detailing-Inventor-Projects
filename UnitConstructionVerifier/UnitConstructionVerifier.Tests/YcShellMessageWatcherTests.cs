using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// The watcher against real Win32 message boxes, shown from a worker thread as YCShell does. Needs a desktop
    /// session.
    /// </summary>
    [TestFixture, NonParallelizable]
    public class YcShellMessageWatcherTests
    {
        private const uint MbOk = 0x0, MbYesNo = 0x4;
        private const int IdNo = 7;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowW(string? className, string caption);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static Thread ShowBox(string text, string caption, uint type, Action<int> result)
        {
            var thread = new Thread(() => result(MessageBoxW(IntPtr.Zero, text, caption, type))) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return thread;
        }

        [Test]
        public void Closes_The_Completion_Box_And_Records_It()
        {
            using var watcher = new YcShellMessageWatcher { AutoClose = true };
            watcher.Begin("0055.iam");

            int answer = 0;
            Thread box = ShowBox("\n4 Channel Features have been recreated/replaced\n\n", "Wall : 391Z010068-0055 (UCV test)", MbOk, r => answer = r);

            bool closed = box.Join(TimeSpan.FromSeconds(5));
            var all = watcher.End();
            if (!closed)
            {
                PostMessage(FindWindowW("#32770", "Wall : 391Z010068-0055 (UCV test)"), 0x0111, new IntPtr(1), IntPtr.Zero);
                box.Join(TimeSpan.FromSeconds(5));
            }
            Assert.IsTrue(closed, "the watcher should have closed the box; saw: " +
                string.Join(" | ", all.Select(m => $"{m.Kind} buttons=[{string.Join(",", m.Buttons)}] text=[{m.Text}] closed={m.ClosedByUcv}")));
            YcShellMessage seen = all.Single();
            Assert.AreEqual(YcShellMessageKind.Completion, seen.Kind);
            Assert.IsTrue(seen.ClosedByUcv);
            Assert.AreEqual(1, answer, "closed with OK");
        }

        [Test]
        public void Leaves_Questions_For_The_Detailer()
        {
            const string caption = "Wall : 391Z010068-0074 (UCV test question)";
            using var watcher = new YcShellMessageWatcher { AutoClose = true };
            watcher.Begin("0074.iam");

            int answer = 0;
            Thread box = ShowBox("Snaplock hole regeneration will NOT occur\n\nRegenerate foam holes only?", caption, MbYesNo, r => answer = r);
            try
            {
                Assert.IsFalse(box.Join(TimeSpan.FromSeconds(1.5)), "a Yes/No question must stay open");
            }
            finally
            {
                // Answer it as the detailer would.
                IntPtr hwnd = FindWindowW("#32770", caption);
                PostMessage(hwnd, 0x0111, new IntPtr(IdNo), IntPtr.Zero);
                box.Join(TimeSpan.FromSeconds(5));
            }

            YcShellMessage seen = watcher.End().Single();
            Assert.AreEqual(YcShellMessageKind.Question, seen.Kind);
            Assert.IsFalse(seen.ClosedByUcv);
            Assert.AreEqual(IdNo, answer);
        }

        [Test]
        public void With_Auto_Close_Off_Nothing_Is_Closed()
        {
            const string caption = "Wall : 391Z010068-0003 (UCV test no auto close)";
            using var watcher = new YcShellMessageWatcher { AutoClose = false };
            watcher.Begin("0003.iam");

            Thread box = ShowBox("2 Channel Features have been recreated/replaced", caption, MbOk, _ => { });
            try
            {
                Assert.IsFalse(box.Join(TimeSpan.FromSeconds(1.5)));
            }
            finally
            {
                PostMessage(FindWindowW("#32770", caption), 0x0111, new IntPtr(1), IntPtr.Zero);
                box.Join(TimeSpan.FromSeconds(5));
            }

            Assert.IsFalse(watcher.End().Single().ClosedByUcv);
        }
    }
}
