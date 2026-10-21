using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Grappling;

internal static class GrapplingSkill
{
    // Save identity, following Jotunn's MIT custom-skill registration pattern.
    // Never rename this identifier: native Skills.Save keys progress by its ID.
    internal static readonly Skills.SkillType Type =
        (Skills.SkillType)Math.Abs("com.benheim.qol.Grappling".GetStableHashCode());
    internal static readonly string LocalizationKey = "skill_" + (int)Type;
    internal const string Description =
        "Traverse with the grappling hook to gain experience. Longer travel and uphill gains earn more. " +
        "Higher levels extend reach and shorten reload time.";

    internal static void Register(Skills skills)
    {
        if ((int)Type <= (int)Skills.SkillType.All || Enum.IsDefined(typeof(Skills.SkillType), Type))
        {
            throw new InvalidOperationException("Grappling skill ID collides with a native skill.");
        }
        GameObject? prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("GrapplingHook") : null;
        ItemDrop? hook = prefab ? prefab.GetComponent<ItemDrop>() : null;
        Sprite? icon = hook ? hook.m_itemData.GetIcon() : null;
        Skills.SkillDef? existing = skills.m_skills.Find(definition => definition.m_skill == Type);
        if (existing != null)
        {
            // Skills can awaken before ObjectDB's item sprites are ready.
            // Load calls Register again; refresh the same definition so saved
            // skill data and the native Skills UI keep one shared identity.
            if (icon && existing.m_icon != icon)
            {
                existing.m_icon = icon;
                Diagnostics.Emit(DiagnosticEvent.Create("Grappling", "skill_icon_ready")
                    .Integer("skill_id", (int)Type));
            }
            return;
        }

        skills.m_skills.Add(new Skills.SkillDef
        {
            m_skill = Type,
            m_description = Description,
            m_icon = icon,
            m_increseStep = 1f
        });
        Diagnostics.Emit(DiagnosticEvent.Create("Grappling", "skill_registered")
            .Integer("skill_id", (int)Type).Boolean("icon_ready", icon));
    }

    internal static void ObservePersistence(Skills skills, string action)
    {
        Skills.Skill? skill = skills.GetSkillList().Find(value => value.m_info.m_skill == Type);
        Diagnostics.Emit(DiagnosticEvent.Create("Grappling", action)
            .Integer("skill_id", (int)Type).Boolean("present", skill != null)
            .Number("level", skill?.m_level ?? 0f).Number("accumulator", skill?.m_accumulator ?? 0f));
    }
}

[HarmonyPatch(typeof(Skills), nameof(Skills.Awake))]
internal static class GrapplingSkillRegistrationPatch
{
    private static void Postfix(Skills __instance) => GrapplingSkill.Register(__instance);
}

[HarmonyPatch(typeof(Skills), "IsSkillValid")]
internal static class GrapplingSkillValidationPatch
{
    private static void Postfix(Skills.SkillType type, ref bool __result)
    {
        if (type == GrapplingSkill.Type) __result = true;
    }
}

[HarmonyPatch(typeof(Skills), nameof(Skills.Load))]
internal static class GrapplingSkillLoadPatch
{
    // Ensure the definition exists before native Load resolves the saved ID.
    private static void Prefix(Skills __instance) => GrapplingSkill.Register(__instance);
    private static void Postfix(Skills __instance) => GrapplingSkill.ObservePersistence(__instance, "skill_loaded");
}

[HarmonyPatch(typeof(Skills), nameof(Skills.Save))]
internal static class GrapplingSkillSavePatch
{
    private static void Postfix(Skills __instance) => GrapplingSkill.ObservePersistence(__instance, "skill_saved");
}

[HarmonyPatch(typeof(Localization), "Translate")]
internal static class GrapplingSkillNamePatch
{
    // Native skill UI and level-up messages both build $skill_<numeric ID>.
    // Resolve that one token across language changes, without cached startup
    // translations or a second skill UI.
    private static bool Prefix(string word, ref string __result)
    {
        if (word != GrapplingSkill.LocalizationKey) return true;
        __result = "Grappling";
        return false;
    }
}
