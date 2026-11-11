using NUnit.Framework;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class UcvThemeTests
    {
        [TestCase(ThemeChoice.System, true, false)]
        [TestCase(ThemeChoice.System, false, true)]
        [TestCase(ThemeChoice.Light, false, false)]
        [TestCase(ThemeChoice.Dark, true, true)]
        public void Resolve_FollowsWindowsOnlyForSystem(ThemeChoice choice, bool windowsLight, bool expectDark)
            => Assert.That(UcvTheme.Resolve(choice, windowsLight), Is.EqualTo(expectDark));

        [Test]
        public void Next_CyclesSystemLightDark()
        {
            Assert.That(UcvTheme.Next(ThemeChoice.System), Is.EqualTo(ThemeChoice.Light));
            Assert.That(UcvTheme.Next(ThemeChoice.Light), Is.EqualTo(ThemeChoice.Dark));
            Assert.That(UcvTheme.Next(ThemeChoice.Dark), Is.EqualTo(ThemeChoice.System));
        }

        [TestCase("Dark", ThemeChoice.Dark)]
        [TestCase(" light\r\n", ThemeChoice.Light)]
        [TestCase("", ThemeChoice.System)]
        [TestCase(null, ThemeChoice.System)]
        [TestCase("7", ThemeChoice.System)]
        [TestCase("purple", ThemeChoice.System)]
        public void Parse_FallsBackToSystem(string? saved, ThemeChoice expected)
            => Assert.That(UcvTheme.Parse(saved), Is.EqualTo(expected));
    }
}
