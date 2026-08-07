using System.Collections.Generic;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class BulkheadChannelCalculatorTests
{
    [Fact]
    public void CalculateChannels_EncompassesMultipleHolePatternsInLine()
    {
        var patterns = new List<BulkheadHolePatternModel>
        {
            new BulkheadHolePatternModel(
                SegmentType: "RF",
                BulkheadPartNumber: "391-60231-913",
                BulkheadDescription: "FLTR TYPE8 75 X 62",
                UnitSide: "Top",
                Index: 1,
                DoaOffset: 0.5,
                WidthOffset: 1.655,
                WidthQty: 1.0,
                WidthSpacing: 0,
                HoleDiameter: 0.152
            ),
            new BulkheadHolePatternModel(
                SegmentType: "RF",
                BulkheadPartNumber: "391-60231-913",
                BulkheadDescription: "FLTR TYPE8 75 X 62",
                UnitSide: "Top",
                Index: 2,
                DoaOffset: 0.5,
                WidthOffset: 5.0,
                WidthQty: 6.0,
                WidthSpacing: 10.4,
                HoleDiameter: 0.152
            ),
            new BulkheadHolePatternModel(
                SegmentType: "RF",
                BulkheadPartNumber: "391-60231-913",
                BulkheadDescription: "FLTR TYPE8 75 X 62",
                UnitSide: "Top",
                Index: 10,
                DoaOffset: 0.5,
                WidthOffset: 60.345,
                WidthQty: 1.0,
                WidthSpacing: 0,
                HoleDiameter: 0.152
            )
        };

        var surfaceBoxes = new List<GeometryBox>
        {
            new GeometryBox(0, 75, 0, 140, 2, 80)
        };

        var channels = BulkheadChannelCalculator.CalculateChannels(patterns, surfaceBoxes, "Top");

        // Should produce ONE encompassing channel for the Top surface
        Assert.Single(channels);
        var chan = channels[0];
        Assert.Equal(1.5, chan.XLength);
        Assert.Equal(1.5, chan.YLength);
        Assert.True(chan.ZLength > 55.0); // Encompasses from 1.655 to 60.345 + 1.5
    }

    [Fact]
    public void CalculateChannels_FiltersNonFanBottomBulkheadChannels()
    {
        var patterns = new List<BulkheadHolePatternModel>
        {
            new BulkheadHolePatternModel(
                SegmentType: "RF",
                BulkheadPartNumber: "391-60231-913",
                BulkheadDescription: "FLTR TYPE8 75 X 62",
                UnitSide: "Bottom",
                Index: 2,
                DoaOffset: 0.5,
                WidthOffset: 5.0,
                WidthQty: 6.0,
                WidthSpacing: 10.4,
                HoleDiameter: 0.152
            )
        };

        var surfaceBoxes = new List<GeometryBox>
        {
            new GeometryBox(0, 0, 0, 140, 2, 80)
        };

        // Non-fan bottom surface should return 0 bulkhead channels
        var channels = BulkheadChannelCalculator.CalculateChannels(patterns, surfaceBoxes, "Bottom");
        Assert.Empty(channels);
    }
}
