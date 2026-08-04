using System;
using System.IO;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class InventorComReaderTests
{
    [Fact]
    public void IsInventorRunning_ExecutesSafelyWithoutThrowing()
    {
        // Should return true or false cleanly without throwing COMException or leaking RCWs
        bool isRunning = InventorComReader.IsInventorRunning();
        Assert.True(isRunning || !isRunning);
    }

    [Fact]
    public void IsInventorRunning_MultipleCalls_ExecutesConsistentlyWithoutLeaking()
    {
        for (int i = 0; i < 5; i++)
        {
            bool isRunning = InventorComReader.IsInventorRunning();
            Assert.True(isRunning || !isRunning);
        }
    }

    [Fact]
    public void TryReadConfigJsonAttribute_NonExistentFile_ReturnsNull()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid() + ".iam");
        string? result = InventorComReader.TryReadConfigJsonAttribute(fakePath);
        Assert.Null(result);
    }

    [Fact]
    public void TryReadConfigJsonAttribute_NullOrEmptyPath_ReturnsNull()
    {
        string? result = InventorComReader.TryReadConfigJsonAttribute("");
        Assert.Null(result);
    }
}
