#!/usr/bin/env bash
set -euo pipefail

tests_dir="$(dirname "${BASH_SOURCE[0]}")"
root="$tests_dir/.."
feature="$root/src/Grappling"
native_tree="$("$root/scripts/ensure-valheim-source.sh")"

# Keep the focused lifecycle fixture tied to native contracts that determine
# when its production hooks run and whether the postfix can still see ownership.
rg -Fq 'public void Break(bool early)' "$native_tree/GrapplingPoint.cs"
rg -Fq 'm_localGrappler.Break(early: true);' "$native_tree/GrapplingPoint.cs"
rg -Fq 'Object.Instantiate(m_spawnOnDone, base.transform.position, base.transform.rotation).GetComponent<GrapplingPoint>()?.Activate(m_character);' "$native_tree/GrapplingPoint.cs"
native_break="$(sed -n '/public void Break(bool early)/,$p' "$native_tree/GrapplingPoint.cs")"
before_handoff="${native_break%%'Activate(m_character)'*}"
after_handoff="${native_break#*'Activate(m_character)'}"
[[ "$before_handoff" != "$native_break" && "$after_handoff" == *'m_jumpOnDone'* ]]
rg -Fq 'm_ttl -= Time.fixedDeltaTime;' "$native_tree/Projectile.cs"
rg -Fq 'if (m_ttl > 0f)' "$native_tree/Projectile.cs"
rg -Fq 'component.ResetZDO();' "$native_tree/ZNetScene.cs"
rg -Fq 'm_zdo = null;' "$native_tree/ZNetView.cs"

# Native Skills writes ID, level, and accumulator, then gates loading through
# IsSkillValid. The focused managed fixture runs the real installed Save and
# ZPackage methods; Harmony's game-runtime load interception is source-checked.
rg -Fq 'pkg.Write(m_skillData.Count);' "$native_tree/Skills.cs"
rg -Fq 'pkg.Write((int)skillDatum.Value.m_info.m_skill);' "$native_tree/Skills.cs"
rg -Fq 'pkg.Write(skillDatum.Value.m_level);' "$native_tree/Skills.cs"
rg -Fq 'pkg.Write(skillDatum.Value.m_accumulator);' "$native_tree/Skills.cs"
rg -Fq 'if (IsSkillValid(skillType))' "$native_tree/Skills.cs"
rg -Fq 'return Enum.IsDefined(typeof(SkillType), type);' "$native_tree/Skills.cs"
native_load="$(sed -n '/public void Load(ZPackage pkg)/,/^[[:space:]]*}/p' "$native_tree/Skills.cs")"
load_gate="${native_load%%'if (IsSkillValid(skillType))'*}"
load_resolve="${native_load#*'if (IsSkillValid(skillType))'}"
[[ "$load_gate" != "$native_load" && "$load_resolve" == *'Skill skill = GetSkill(skillType);'* ]]
rg -Fq '"com.benheim.qol.Grappling".GetStableHashCode()' "$feature/GrapplingSkill.cs"
rg -Fq '[HarmonyPatch(typeof(Skills), "IsSkillValid")]' "$feature/GrapplingSkill.cs"
rg -Fq 'if (type == GrapplingSkill.Type) __result = true;' "$feature/GrapplingSkill.cs"
rg -Fq '[HarmonyPatch(typeof(Skills), nameof(Skills.Load))]' "$feature/GrapplingSkill.cs"
rg -Fq 'private static void Prefix(Skills __instance) => GrapplingSkill.Register(__instance);' "$feature/GrapplingSkill.cs"

dotnet run --project "$tests_dir/grappling/GrapplingTests.csproj"
dotnet run --project "$tests_dir/grappling/persistence/SkillPersistenceTests.csproj"
printf 'Grappling fixtures and installed-native source seams passed; gameplay runtime remains unproven\n'
