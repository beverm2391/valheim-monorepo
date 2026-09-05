using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

// Polling follows the native picker lifecycle without changing Hud or input.
// Buttons call Select directly; number keys retain their native hotbar path.
internal static class FarmingGridPicker
{
    private static FarmingGridPickerView? view;
    private static Player? owner;
    private static Hud? hud;
    private static string lastState = string.Empty;
    private static float nextCreateTime;
    private static int viewGeneration;

    internal static void Update()
    {
        try
        {
            string? blocked = UnavailableReason();
            if (blocked != null)
            {
                EndSession(blocked);
                return;
            }

            if (owner != Player.m_localPlayer || hud != Hud.instance)
            {
                EndSession("owner_changed");
                owner = Player.m_localPlayer;
                hud = Hud.instance;
            }
            if (FarmingGridSelection.UpdatePickerSession(pickerOpen: true))
            {
                PlantingPreview.DestroyGhosts();
                State("opened", "default_size");
            }

            if (view != null && view.IsAlive) return;
            if (Time.realtimeSinceStartup < nextCreateTime) return;
            nextCreateTime = Time.realtimeSinceStartup + 1f;
            view?.Destroy();
            int generation = ++viewGeneration;
            view = FarmingGridPickerView.TryCreate(hud!, size => Select(size, generation), out string failure);
            if (view == null)
            {
                State("unavailable", failure);
                return;
            }
            view.Highlight(FarmingGridSelection.CurrentSize);
            State("shown", "native_donors");
        }
        catch (Exception exception)
        {
            DestroyView();
            State("failed", exception.GetType().Name);
        }
    }

    internal static void Reset()
    {
        EndSession("reset");
        FarmingGridSelection.Reset();
    }

    private static string? UnavailableReason()
    {
        if (!HealthReporting.GameplayActionsEnabled) return "gameplay_disabled";
        Player? player = Player.m_localPlayer;
        if (player == null) return "player_unavailable";
        if (Hud.instance == null) return "hud_unavailable";
        ItemDrop.ItemData? tool = player.RightItem;
        if (tool?.m_dropPrefab == null || tool.m_dropPrefab.name != "Cultivator") return "other_tool";
        if (tool.m_shared.m_buildPieces == null || !player.InPlaceMode()) return "not_place_mode";
        if (!Hud.IsPieceSelectionVisible()) return "picker_closed";
        return null;
    }

    private static void Select(int size, int generation)
    {
        // Recheck at the callback, since a close, equipment swap, or logout may
        // occur after Update. A stale button must never change the next session.
        Emit(DiagnosticEvent.Create("Farming", "plant_grid_choice_attempt")
            .Integer("requested_size", size)
            .Integer("selected_size", FarmingGridSelection.CurrentSize));
        try
        {
            string? reason = UnavailableReason();
            if (reason == null && (generation != viewGeneration || owner != Player.m_localPlayer || hud != Hud.instance || view == null || !view.IsAlive))
                reason = "stale_picker";
            if (reason == null && InputState.IsTextEntryActive()) reason = "text_entry";
            if (reason == null && !FarmingGridSelection.IsAllowed(size)) reason = "unsupported_size";
            if (reason != null)
            {
                ChoiceResult(size, "blocked", reason);
                return;
            }

            FarmingGridSelection.TrySelect(size);
            PlantingPreview.DestroyGhosts();
            view!.Highlight(size);
            ChoiceResult(size, "selected", "button_click");
        }
        catch (Exception exception)
        {
            ChoiceResult(size, "failed", exception.GetType().Name);
        }
    }

    private static void EndSession(string reason)
    {
        bool hadSession = owner != null || hud != null || view != null;
        DestroyView();
        owner = null;
        hud = null;
        nextCreateTime = 0;
        FarmingGridSelection.UpdatePickerSession(pickerOpen: false);
        if (hadSession) State("closed", reason);
        lastState = string.Empty;
    }

    private static void DestroyView()
    {
        view?.Destroy();
        view = null;
    }

    private static void State(string result, string reason)
    {
        string state = result + ":" + reason;
        if (state == lastState) return;
        lastState = state;
        Emit(DiagnosticEvent.Create("Farming", "plant_grid_picker")
            .String("result", result).String("reason", reason)
            .Integer("selected_size", FarmingGridSelection.CurrentSize)
            .Boolean("row_visible", view != null && view.IsAlive));
    }

    private static void ChoiceResult(int requested, string result, string reason)
    {
        Emit(DiagnosticEvent.Create("Farming", "plant_grid_choice_result")
            .String("result", result).String("reason", reason)
            .Integer("requested_size", requested)
            .Integer("selected_size", FarmingGridSelection.CurrentSize)
            .Integer("highlighted_size", view?.HighlightedSize ?? 0)
            .Boolean("picker_visible", Hud.IsPieceSelectionVisible())
            .Boolean("row_visible", view != null && view.IsAlive));
    }

    private static void Emit(DiagnosticEvent record)
    {
        // Logging failures must not escape a Unity button callback or interrupt
        // preview invalidation, selection, and picker cleanup.
        try { Diagnostics.Emit(record); }
        catch (Exception) { }
    }
}
