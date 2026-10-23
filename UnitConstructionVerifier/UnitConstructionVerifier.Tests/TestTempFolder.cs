using System;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Points the test run's temp folder somewhere of its own before any test runs. UCV's debug log lives under
/// %TEMP%\UnitConstructionVerifier, and tests that exercise error paths would otherwise write fake errors into the
/// detailer's real log.
/// </summary>
[SetUpFixture]
public sealed class TestTempFolder
{
    private string? _folder;
    private string? _previousTmp;
    private string? _previousTemp;

    [OneTimeSetUp]
    public void UseOwnTempFolder()
    {
        _previousTmp = Environment.GetEnvironmentVariable("TMP");
        _previousTemp = Environment.GetEnvironmentVariable("TEMP");
        _folder = Path.Combine(Path.GetTempPath(), "ucv_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        Environment.SetEnvironmentVariable("TMP", _folder);
        Environment.SetEnvironmentVariable("TEMP", _folder);
    }

    [OneTimeTearDown]
    public void Restore()
    {
        Environment.SetEnvironmentVariable("TMP", _previousTmp);
        Environment.SetEnvironmentVariable("TEMP", _previousTemp);
        try { if (_folder is not null) Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }
}
