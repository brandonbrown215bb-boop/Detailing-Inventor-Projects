using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class StatusStateManagerTests
{
    [Fact]
    public void DefaultStates_ContainsSevenCoreStates_WithExpectedColorsNamesAndFillTypes()
    {
        var manager = new StatusStateManager();
        Assert.Equal(7, manager.States.Count);
        
        var defaultMap = new Dictionary<string, (string Name, string ColorHex, string FillType)>
        {
            ["current"] = ("Current", "#94A3B8", "solid"),
            ["corrected"] = ("Corrected", "#F59E0B", "solid"),
            ["built"] = ("Built", "#3B82F6", "solid"),
            ["associated"] = ("Associated", "#8B5CF6", "solid"),
            ["paperwork-corrected"] = ("Paperwork Corrected", "#06B6D4", "solid"),
            ["paperwork-uploaded"] = ("Paperwork Uploaded", "#10B981", "solid"),
            ["done"] = ("Done", "#22C55E", "solid")
        };

        foreach (var kvp in defaultMap)
        {
            var state = manager.GetState(kvp.Key);
            Assert.NotNull(state);
            Assert.Equal(kvp.Value.Name, state.Name);
            Assert.Equal(kvp.Value.ColorHex, state.ColorHex, ignoreCase: true);
            Assert.Equal(kvp.Value.FillType, state.FillType, ignoreCase: true);
        }
    }

    [Fact]
    public void AddState_ValidCustomState_AddsSuccessfullyToCollection()
    {
        var manager = new StatusStateManager();
        var newState = new StatusState("custom-qc-hold", "QC Hold", "#ef4444", "wireframe");

        bool result = manager.AddState(newState);

        Assert.True(result);
        Assert.Equal(8, manager.States.Count);
        var added = manager.GetState("custom-qc-hold");
        Assert.NotNull(added);
        Assert.Equal("QC Hold", added.Name);
        Assert.Equal("#EF4444", added.ColorHex, ignoreCase: true);
        Assert.Equal("wireframe", added.FillType, ignoreCase: true);
    }

    [Theory]
    [InlineData("3b82f6", "#3B82F6")]
    [InlineData("#FF0000", "#FF0000")]
    [InlineData("invalid-color", "#94A3B8")]
    public void AddOrUpdateState_NormalizesHexColor(string inputColor, string expectedColor)
    {
        var manager = new StatusStateManager();
        var state = new StatusState("test-color", "Test Color", inputColor, "solid");
        
        bool added = manager.AddState(state);
        Assert.True(added);

        var retrieved = manager.GetState("test-color");
        Assert.NotNull(retrieved);
        Assert.Equal(expectedColor, retrieved.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void UpdateState_ExistingState_UpdatesColorHexAndFillTypeCorrectly()
    {
        var manager = new StatusStateManager();
        
        bool updated = manager.UpdateState("corrected", "Needs Correction", "#d97706", "wireframe");

        Assert.True(updated);
        var state = manager.GetState("corrected");
        Assert.NotNull(state);
        Assert.Equal("Needs Correction", state.Name);
        Assert.Equal("#D97706", state.ColorHex, ignoreCase: true);
        Assert.Equal("wireframe", state.FillType, ignoreCase: true);

        Assert.Equal("Built", manager.GetState("built")?.Name);
    }

    [Fact]
    public void DeleteState_CustomState_DeletesSuccessfully_BuiltInState_ProtectsOrFails()
    {
        var manager = new StatusStateManager();
        manager.AddState(new StatusState("temp-state", "Temporary", "#123456", "solid"));

        Assert.NotNull(manager.GetState("temp-state"));
        bool deleteCustomResult = manager.DeleteState("temp-state");
        Assert.True(deleteCustomResult);
        Assert.Null(manager.GetState("temp-state"));

        bool deleteBuiltInResult = manager.DeleteState("current");
        Assert.False(deleteBuiltInResult);
        Assert.NotNull(manager.GetState("current"));
    }

    [Fact]
    public void StatePersistence_SerializeAndDeserialize_PreservesCustomStatesAndFillTypes()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"state_persistence_{Guid.NewGuid():N}.json");
        try
        {
            var states = StatusStateService.GetDefaultStates();
            states.Add(new StatusState("qc-hold", "QC Hold", "#ef4444", "wireframe"));
            states.Add(new StatusState("in-review", "In Review", "#a855f7", "solid"));

            ProjectSerializer.SaveAtomic(tempFile, states);
            var reloaded = ProjectSerializer.Load<List<StatusState>>(tempFile);

            Assert.NotNull(reloaded);
            Assert.Equal(9, reloaded.Count);
            var qcHold = reloaded.FirstOrDefault(s => s.Id == "qc-hold");
            Assert.NotNull(qcHold);
            Assert.Equal("QC Hold", qcHold.Name);
            Assert.Equal("#EF4444", qcHold.ColorHex, ignoreCase: true);
            Assert.Equal("wireframe", qcHold.FillType, ignoreCase: true);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AddState_DuplicateId_ThrowsOrReturnsFalse()
    {
        var manager = new StatusStateManager();
        var duplicate = new StatusState("done", "Duplicate Done", "#000000", "solid");

        bool result = manager.AddState(duplicate);
        Assert.False(result);
    }
}
