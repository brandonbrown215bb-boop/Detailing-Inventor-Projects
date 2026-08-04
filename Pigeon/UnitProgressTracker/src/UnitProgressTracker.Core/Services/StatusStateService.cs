using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

/// <summary>
/// Domain service for validating, managing, and resolving StatusState configurations.
/// </summary>
public static class StatusStateService
{
    private static readonly Regex HexColorRegex = new(
        @"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        RegexOptions.Compiled);

    /// <summary>
    /// Default status state fallback ID when a state is deleted or unknown.
    /// </summary>
    public const string DefaultFallbackStateId = "current";

    /// <summary>
    /// Returns a fresh list of the 7 predefined default status states.
    /// </summary>
    public static List<StatusState> GetDefaultStates()
    {
        return StatusState.DefaultStates
            .Select(s => new StatusState(s.Id, s.Name, s.ColorHex, s.FillType))
            .ToList();
    }

    /// <summary>
    /// Checks if a state ID matches one of the 7 built-in default states.
    /// </summary>
    public static bool IsDefaultState(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return StatusState.DefaultStates.Any(d => string.Equals(d.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates whether a hex color string is valid (#RGB, #RRGGBB, #AARRGGBB or without '#').
    /// </summary>
    public static bool IsValidHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        return HexColorRegex.IsMatch(hex.Trim());
    }

    /// <summary>
    /// Normalizes a color string into standard uppercase '#RRGGBB' or '#AARRGGBB' format.
    /// If invalid, returns the fallback color.
    /// </summary>
    public static string NormalizeHexColor(string? hex, string fallbackHex = "#94A3B8")
    {
        if (!IsValidHexColor(hex)) return fallbackHex.ToUpperInvariant();
        string clean = hex!.Trim();
        if (!clean.StartsWith('#')) clean = "#" + clean;
        
        // Expand #RGB -> #RRGGBB
        if (clean.Length == 4)
        {
            clean = $"#{clean[1]}{clean[1]}{clean[2]}{clean[2]}{clean[3]}{clean[3]}";
        }
        return clean.ToUpperInvariant();
    }

    /// <summary>
    /// Validates fill type string (case-insensitive "solid" or "wireframe").
    /// </summary>
    public static bool IsValidFillType(string? fillType)
    {
        if (string.IsNullOrWhiteSpace(fillType)) return false;
        string f = fillType.Trim();
        return string.Equals(f, "solid", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(f, "wireframe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes fill type to lower-case "solid" or "wireframe".
    /// </summary>
    public static string NormalizeFillType(string? fillType)
    {
        if (IsValidFillType(fillType) && string.Equals(fillType!.Trim(), "wireframe", StringComparison.OrdinalIgnoreCase))
        {
            return "wireframe";
        }
        return "solid";
    }

    /// <summary>
    /// Resolves a StatusState by ID from a given list, returning a fallback state if not found.
    /// </summary>
    public static StatusState GetStateOrDefault(IEnumerable<StatusState>? states, string? id)
    {
        if (states == null) return StatusState.DefaultStates[0];
        
        if (!string.IsNullOrWhiteSpace(id))
        {
            var match = states.FirstOrDefault(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return states.FirstOrDefault(s => string.Equals(s.Id, DefaultFallbackStateId, StringComparison.OrdinalIgnoreCase))
            ?? states.FirstOrDefault()
            ?? StatusState.DefaultStates[0];
    }

    /// <summary>
    /// Validates and adds a new custom StatusState to a list.
    /// </summary>
    public static StatusState AddCustomState(
        List<StatusState> states,
        string id,
        string name,
        string colorHex,
        string fillType = "solid")
    {
        if (states == null) throw new ArgumentNullException(nameof(states));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("State ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("State name cannot be empty.", nameof(name));

        string cleanId = id.Trim().ToLowerInvariant();
        if (states.Any(s => string.Equals(s.Id, cleanId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Status state with ID '{cleanId}' already exists.");
        }

        string normColor = NormalizeHexColor(colorHex);
        string normFill = NormalizeFillType(fillType);

        var newState = new StatusState(cleanId, name.Trim(), normColor, normFill);
        states.Add(newState);
        return newState;
    }

    /// <summary>
    /// Updates an existing StatusState's properties.
    /// </summary>
    public static bool UpdateState(
        IEnumerable<StatusState>? states,
        string id,
        string? newName = null,
        string? newColorHex = null,
        string? newFillType = null)
    {
        if (states == null || string.IsNullOrWhiteSpace(id)) return false;

        var target = states.FirstOrDefault(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target == null) return false;

        if (!string.IsNullOrWhiteSpace(newName))
        {
            target.Name = newName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(newColorHex) && IsValidHexColor(newColorHex))
        {
            target.ColorHex = NormalizeHexColor(newColorHex);
        }

        if (!string.IsNullOrWhiteSpace(newFillType) && IsValidFillType(newFillType))
        {
            target.FillType = NormalizeFillType(newFillType);
        }

        return true;
    }

    /// <summary>
    /// Deletes a StatusState from the list and determines the fallback state for orphaned surfaces.
    /// </summary>
    public static bool DeleteState(
        List<StatusState> states,
        string id,
        out string fallbackStateId,
        string requestedFallbackId = DefaultFallbackStateId)
    {
        fallbackStateId = DefaultFallbackStateId;
        if (states == null || string.IsNullOrWhiteSpace(id)) return false;

        var target = states.FirstOrDefault(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target == null) return false;

        states.Remove(target);

        if (states.Any(s => string.Equals(s.Id, requestedFallbackId, StringComparison.OrdinalIgnoreCase)))
        {
            fallbackStateId = requestedFallbackId;
        }
        else if (states.Count > 0)
        {
            fallbackStateId = states[0].Id;
        }
        else
        {
            states.AddRange(GetDefaultStates());
            fallbackStateId = DefaultFallbackStateId;
        }

        return true;
    }
}
