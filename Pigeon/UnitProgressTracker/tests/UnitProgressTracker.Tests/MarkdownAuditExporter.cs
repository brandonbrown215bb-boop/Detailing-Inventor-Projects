using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Tests;

public static class MarkdownAuditExporter
{
    public static string GenerateAuditReport(IEnumerable<SurfaceModel> surfaces, IEnumerable<StatusState> states)
    {
        var surfaceList = surfaces.ToList();
        var stateMap = states.ToDictionary(s => s.Id, s => s.Name);
        var sb = new StringBuilder();

        sb.AppendLine("# Unit Progress Tracker - Surface Audit Report");
        sb.AppendLine();
        sb.AppendLine($"Total Surfaces: {surfaceList.Count}");
        sb.AppendLine($"Active (Visible): {surfaceList.Count(s => !s.IsHidden)}");
        sb.AppendLine($"Hidden: {surfaceList.Count(s => s.IsHidden)}");
        sb.AppendLine();

        sb.AppendLine("## Status Breakdown");
        sb.AppendLine("| Status | Count | Percentage |");
        sb.AppendLine("| --- | --- | --- |");

        var statusCounts = surfaceList
            .GroupBy(s => s.StateId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var state in states)
        {
            int count = statusCounts.GetValueOrDefault(state.Id, 0);
            double pct = surfaceList.Count > 0 ? (double)count / surfaceList.Count * 100 : 0;
            sb.AppendLine($"| {state.Name} | {count} | {pct:F1}% |");
        }

        sb.AppendLine();
        sb.AppendLine("## Surface Details");
        sb.AppendLine("| Surface # | Part # | Status | Hidden | Checklist Progress | Notes |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (var surf in surfaceList)
        {
            string statusName = stateMap.GetValueOrDefault(surf.StateId, surf.StateId);
            int totalCheck = surf.Checklist.Count;
            int doneCheck = surf.Checklist.Values.Count(v => v);
            string progress = totalCheck > 0 ? $"{doneCheck}/{totalCheck}" : "N/A";
            string notes = string.IsNullOrWhiteSpace(surf.Notes) ? "-" : surf.Notes.Replace("\n", " ");

            sb.AppendLine($"| {surf.SurfaceNumber} | {surf.PartNumber} | {statusName} | {surf.IsHidden} | {progress} | {notes} |");
        }

        return sb.ToString();
    }
}
