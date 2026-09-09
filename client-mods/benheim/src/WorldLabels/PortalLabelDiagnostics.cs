using System;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

// Per-portal change detection keeps ordinary play observable without repeating
// unchanged labels on the controller's half-second refresh.
internal sealed class PortalLabelDiagnostics
{
    private string? previousTag;
    private string? previousState;
    private FaceLayout previousFront;
    private FaceLayout previousBack;

    internal void Observe(
        TeleportWorld portal, string tag, string state,
        TextMeshProUGUI? front = null, TextMeshProUGUI? back = null,
        bool refreshMesh = false)
    {
        FaceLayout frontLayout = state == "visible" ? Measure(front, tag, refreshMesh) : default;
        FaceLayout backLayout = state == "visible" ? Measure(back, tag, refreshMesh) : default;
        if (tag == previousTag && state == previousState &&
            frontLayout.Equals(previousFront) && backLayout.Equals(previousBack))
        {
            return;
        }

        string change = previousState != "visible" && state == "visible" ? "created"
            : tag != previousTag ? "tag_changed"
            : state != previousState ? "state_changed" : "layout_changed";
        previousTag = tag;
        previousState = state;
        previousFront = frontLayout;
        previousBack = backLayout;

        DiagnosticEvent record = DiagnosticEvent.Create("WorldLabels", "portal_label")
            .Integer("portal_instance", portal.GetInstanceID())
            .String("portal_prefab", portal.gameObject.name)
            .String("tag", tag)
            .String("change", change)
            .String("state", state);
        if (state == "visible")
        {
            AddFace(record, "front", frontLayout);
            AddFace(record, "back", backLayout);
        }
        Emit(record);
    }

    internal static void Emit(DiagnosticEvent record)
    {
        try
        {
            Diagnostics.Emit(record);
        }
        catch
        {
            // Evidence delivery must not interrupt a native portal or its
            // visual lifecycle when a diagnostic destination is unavailable.
        }
    }

    private static FaceLayout Measure(TextMeshProUGUI? label, string tag, bool refreshMesh)
    {
        if (label == null) return new FaceLayout("missing_text_widget");
        if (label.font == null) return new FaceLayout("missing_font");
        if (label.fontSharedMaterial == null) return new FaceLayout("missing_material");
        if (label.GetComponentInParent<Canvas>() == null) return new FaceLayout("missing_canvas");
        if (label.GetComponent<CanvasRenderer>() == null) return new FaceLayout("missing_canvas_renderer");

        try
        {
            // Measure the actual TMP output after creation/rename. Later
            // refreshes read the normally rendered mesh without forcing work.
            if (refreshMesh) label.ForceMeshUpdate(ignoreActiveState: true);
            if (label.havePropertiesChanged) return new FaceLayout("layout_pending");
            return new FaceLayout(label, tag);
        }
        catch (Exception exception)
        {
            return new FaceLayout("measurement_failed:" + exception.GetType().Name);
        }
    }

    private static void AddFace(DiagnosticEvent record, string face, FaceLayout layout)
    {
        record.String(face + "_outcome", layout.Outcome);
        if (!layout.Measured) return;
        record.Number(face + "_font_size", layout.FontSize)
            .Integer(face + "_characters", layout.Characters)
            .Integer(face + "_visible_characters", layout.VisibleCharacters)
            .Integer(face + "_lines", layout.Lines)
            .Boolean(face + "_tmp_overflow", layout.Overflow)
            .Boolean(face + "_text_matches_tag", layout.TextMatches)
            .Number(face + "_text_left", layout.TextBounds.min.x)
            .Number(face + "_text_right", layout.TextBounds.max.x)
            .Number(face + "_text_bottom", layout.TextBounds.min.y)
            .Number(face + "_text_top", layout.TextBounds.max.y)
            .Number(face + "_fit_left", layout.Rect.xMin + layout.Margin.x)
            .Number(face + "_fit_right", layout.Rect.xMax - layout.Margin.z)
            .Number(face + "_fit_bottom", layout.Rect.yMin + layout.Margin.w)
            .Number(face + "_fit_top", layout.Rect.yMax - layout.Margin.y)
            .Number(face + "_scale_x", layout.Scale.x)
            .Number(face + "_scale_y", layout.Scale.y);
    }

    private readonly struct FaceLayout : IEquatable<FaceLayout>
    {
        internal FaceLayout(string outcome)
        {
            this = default;
            Outcome = outcome;
        }

        internal FaceLayout(TextMeshProUGUI label, string tag)
        {
            Measured = true;
            FontSize = label.fontSize;
            Rect = label.rectTransform.rect;
            Margin = label.margin;
            Scale = label.transform.localScale;
            TextBounds = label.textBounds;
            Characters = label.textInfo.characterCount;
            Lines = label.textInfo.lineCount;
            VisibleCharacters = 0;
            for (int index = 0; index < Characters; index++)
                if (label.textInfo.characterInfo[index].isVisible) VisibleCharacters++;
            Overflow = label.isTextOverflowing;
            TextMatches = label.text == tag;
            // Bounds are in the same local units as the padded TMP rectangle.
            // This proves containment of glyph geometry, not glow appearance
            // or whether the result is pleasant to read in the world.
            const float tolerance = 0.001f;
            bool inside = TextBounds.min.x >= Rect.xMin + Margin.x - tolerance &&
                TextBounds.max.x <= Rect.xMax - Margin.z + tolerance &&
                TextBounds.min.y >= Rect.yMin + Margin.w - tolerance &&
                TextBounds.max.y <= Rect.yMax - Margin.y + tolerance;
            Outcome = !TextMatches ? "text_mismatch"
                : VisibleCharacters == 0 ? "no_visible_glyphs"
                : Overflow || !inside ? "overflow" : "fit";
        }

        internal readonly string? Outcome;
        internal readonly bool Measured;
        internal readonly float FontSize;
        internal readonly Rect Rect;
        internal readonly Vector4 Margin;
        internal readonly Vector3 Scale;
        internal readonly Bounds TextBounds;
        internal readonly int Characters;
        internal readonly int VisibleCharacters;
        internal readonly int Lines;
        internal readonly bool Overflow;
        internal readonly bool TextMatches;

        public bool Equals(FaceLayout other) =>
            (Outcome, Measured, FontSize, Rect, Margin, Scale, TextBounds,
                Characters, VisibleCharacters, Lines, Overflow, TextMatches).Equals(
            (other.Outcome, other.Measured, other.FontSize, other.Rect, other.Margin,
                other.Scale, other.TextBounds, other.Characters, other.VisibleCharacters,
                other.Lines, other.Overflow, other.TextMatches));
    }
}
