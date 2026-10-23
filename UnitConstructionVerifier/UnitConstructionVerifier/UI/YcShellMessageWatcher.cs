using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Watches for the Win32 message boxes YCShell shows (from its worker thread) while a surface regenerates. Every box
    /// is recorded for the summary. Known OK-only notices, such as the completion message, are closed when
    /// <see cref="AutoClose"/> is on. Yes/No questions and unknown wording are never touched.
    /// </summary>
    public sealed class YcShellMessageWatcher : IYcShellMessageSource, IDisposable
    {
        private const string DialogClass = "#32770";
        private const uint WmCommand = 0x0111;
        private const int IdOk = 1;
        private const int PollMilliseconds = 250;

        private readonly object _gate = new object();
        private readonly uint _processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        private readonly List<YcShellMessage> _seen = new List<YcShellMessage>();
        private readonly Dictionary<IntPtr, YcShellMessage> _closing = new Dictionary<IntPtr, YcShellMessage>();
        private HashSet<IntPtr> _present = new HashSet<IntPtr>();
        private Timer? _timer;
        private int _polling;

        public bool AutoClose { get; set; } = true;

        public void Begin(string surfaceIamPath)
        {
            lock (_gate)
            {
                _seen.Clear();
                _closing.Clear();
                _present = new HashSet<IntPtr>(FindDialogs());   // boxes already open belong to someone else
            }

            _timer = new Timer(_ => Poll(), null, PollMilliseconds, PollMilliseconds);
        }

        public IReadOnlyList<YcShellMessage> End()
        {
            _timer?.Dispose();
            _timer = null;
            lock (_gate)
            {
                List<IntPtr> open = FindDialogs();
                foreach (KeyValuePair<IntPtr, YcShellMessage> pressed in _closing)
                {
                    if (!open.Contains(pressed.Key)) pressed.Value.ClosedByUcv = true;
                }

                _closing.Clear();
                return _seen.ToArray();
            }
        }

        public void Dispose() => End();

        private void Poll()
        {
            if (Interlocked.Exchange(ref _polling, 1) == 1) return;
            try
            {
                List<IntPtr> dialogs = FindDialogs();
                lock (_gate)
                {
                    // A box UCV pressed OK on and that has gone was closed by UCV.
                    foreach (IntPtr gone in _closing.Keys.Where(h => !dialogs.Contains(h)).ToList())
                    {
                        _closing[gone].ClosedByUcv = true;
                        _closing.Remove(gone);
                    }

                    foreach (IntPtr hwnd in dialogs)
                    {
                        if (_closing.TryGetValue(hwnd, out _))
                        {
                            PressOk(hwnd);   // not gone yet: press again (a box can ignore input while it initializes)
                            continue;
                        }

                        if (_present.Contains(hwnd)) continue;

                        var message = Read(hwnd);
                        _seen.Add(message);
                        if (AutoClose && message.CanCloseAutomatically)
                        {
                            // Pressed on the next poll, once the box is fully up.
                            _closing[hwnd] = message;
                        }
                    }

                    // A window handle can be reused by the next box once this one closes.
                    _present = new HashSet<IntPtr>(dialogs);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "YcShellMessageWatcher.Poll");
            }
            finally
            {
                Interlocked.Exchange(ref _polling, 0);
            }
        }

        /// <summary>Clicks the box's OK button the way the button itself reports a click to its dialog.</summary>
        private static void PressOk(IntPtr dialog)
        {
            IntPtr ok = IntPtr.Zero;
            EnumChildWindows(dialog, (child, _) =>
            {
                if (ClassOf(child) == "Button" && string.Equals(TextOf(child).Replace("&", "").Trim(), "OK", StringComparison.OrdinalIgnoreCase))
                {
                    ok = child;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            int id = ok != IntPtr.Zero ? GetDlgCtrlID(ok) : IdOk;
            PostMessage(dialog, WmCommand, new IntPtr(id & 0xFFFF), ok);   // HIWORD 0 = BN_CLICKED
        }

        private List<IntPtr> FindDialogs()
        {
            var found = new List<IntPtr>();
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == _processId && IsWindowVisible(hwnd) && ClassOf(hwnd) == DialogClass) found.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static YcShellMessage Read(IntPtr hwnd)
        {
            var texts = new List<string>();
            var buttons = new List<string>();
            EnumChildWindows(hwnd, (child, _) =>
            {
                string cls = ClassOf(child);
                string text = TextOf(child);
                if (text.Length == 0) return true;
                if (cls == "Button") buttons.Add(text);
                else if (cls == "Static") texts.Add(text);
                return true;
            }, IntPtr.Zero);

            return new YcShellMessage(TextOf(hwnd), string.Join(Environment.NewLine, texts), buttons);
        }

        private static string ClassOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(64);
            return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        }

        private static string TextOf(IntPtr hwnd)
        {
            int length = GetWindowTextLength(hwnd);
            if (length <= 0) return string.Empty;
            var sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hwnd);
    }
}
