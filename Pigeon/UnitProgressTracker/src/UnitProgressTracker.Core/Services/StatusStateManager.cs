using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

/// <summary>
/// Stateful manager wrapping a collection of StatusStates.
/// </summary>
public class StatusStateManager
{
    private readonly List<StatusState> _states = new();

    public StatusStateManager(IEnumerable<StatusState>? initialStates = null)
    {
        if (initialStates != null && initialStates.Any())
        {
            foreach (var st in initialStates)
            {
                st.ColorHex = StatusStateService.NormalizeHexColor(st.ColorHex);
                st.FillType = StatusStateService.NormalizeFillType(st.FillType);
                _states.Add(st);
            }
        }
        else
        {
            ResetToDefaults();
        }
    }

    public IReadOnlyList<StatusState> States => _states.AsReadOnly();

    public void ResetToDefaults()
    {
        _states.Clear();
        _states.AddRange(StatusStateService.GetDefaultStates());
    }

    public StatusState? GetState(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _states.FirstOrDefault(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public StatusState GetStateOrDefault(string? id)
    {
        return StatusStateService.GetStateOrDefault(_states, id);
    }

    public bool AddState(StatusState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.Id) || string.IsNullOrWhiteSpace(state.Name))
            return false;

        string cleanId = state.Id.Trim().ToLowerInvariant();
        if (_states.Any(s => string.Equals(s.Id, cleanId, StringComparison.OrdinalIgnoreCase)))
            return false;

        state.Id = cleanId;
        state.Name = state.Name.Trim();
        state.ColorHex = StatusStateService.NormalizeHexColor(state.ColorHex);
        state.FillType = StatusStateService.NormalizeFillType(state.FillType);

        _states.Add(state);
        return true;
    }

    public StatusState? AddState(string id, string name, string colorHex, string fillType = "solid")
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return null;

        string cleanId = id.Trim().ToLowerInvariant();
        if (_states.Any(s => string.Equals(s.Id, cleanId, StringComparison.OrdinalIgnoreCase)))
            return null;

        return StatusStateService.AddCustomState(_states, cleanId, name, colorHex, fillType);
    }

    public bool UpdateState(string id, string? name, string? colorHex, string? fillType)
    {
        return StatusStateService.UpdateState(_states, id, name, colorHex, fillType);
    }

    public bool DeleteState(string id)
    {
        return DeleteState(id, out _);
    }

    public bool DeleteState(string id, out string fallbackId, string requestedFallbackId = StatusStateService.DefaultFallbackStateId)
    {
        fallbackId = StatusStateService.DefaultFallbackStateId;
        if (string.IsNullOrWhiteSpace(id)) return false;

        // Protect built-in default states from deletion
        if (StatusStateService.IsDefaultState(id))
        {
            return false;
        }

        return StatusStateService.DeleteState(_states, id, out fallbackId, requestedFallbackId);
    }
}
