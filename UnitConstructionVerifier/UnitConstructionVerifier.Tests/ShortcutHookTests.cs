using System;
using System.Threading;
using NUnit.Framework;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class ShortcutHookTests
    {
        private const int F8 = 0x77;

        [TestCase('C', VerifierShortcut.Copy)]
        [TestCase('V', VerifierShortcut.Paste)]
        public void Ctrl_Letter_Is_A_Shortcut(char key, VerifierShortcut expected)
        {
            Assert.AreEqual(expected, ShortcutHook.Classify(key, ctrl: true, shift: false, alt: false,
                keyUp: false, repeat: false, out bool runs));
            Assert.IsTrue(runs);
        }

        [Test]
        public void F8_Alone_Is_Next_Issue()
        {
            Assert.AreEqual(VerifierShortcut.NextIssue,
                ShortcutHook.Classify(F8, false, false, false, false, false, out bool runs));
            Assert.IsTrue(runs);
        }

        [Test]
        public void Key_Up_And_Repeat_Are_Taken_But_Do_Not_Run_Again()
        {
            // The whole press must be removed so Inventor never sees half of it, but the action runs once.
            Assert.AreEqual(VerifierShortcut.Copy, ShortcutHook.Classify('C', true, false, false, keyUp: true, repeat: false, out bool upRuns));
            Assert.IsFalse(upRuns);
            Assert.AreEqual(VerifierShortcut.NextIssue, ShortcutHook.Classify(F8, false, false, false, keyUp: false, repeat: true, out bool repeatRuns));
            Assert.IsFalse(repeatRuns);
        }

        [TestCase('C', false, false, false)] // plain C is typing
        [TestCase('C', true, true, false)]   // Ctrl+Shift+C stays Inventor's
        [TestCase('V', true, false, true)]   // Ctrl+Alt+V
        [TestCase('X', true, false, false)]  // not taken (yet)
        [TestCase(F8, true, false, false)]   // Ctrl+F8
        public void Other_Keys_Pass_Through(int key, bool ctrl, bool shift, bool alt)
        {
            Assert.AreEqual(VerifierShortcut.None, ShortcutHook.Classify(key, ctrl, shift, alt, false, false, out bool runs));
            Assert.IsFalse(runs);
        }

        [Test]
        public void Hook_Installs_And_Removes_On_The_Current_Thread()
        {
            using (var hook = new ShortcutHook(() => IntPtr.Zero, _ => { }))
            {
                Assert.IsTrue(hook.Install());
                Assert.IsTrue(hook.Install(), "a second Install is a no-op");
            }
        }
    }
}
