using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenheimQoL.Grappling;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static Skills NewSkills()
{
    var skills = (Skills)RuntimeHelpers.GetUninitializedObject(typeof(Skills));
    skills.m_skills = new List<Skills.SkillDef>();
    typeof(Skills).GetField("m_skillData", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(skills, new Dictionary<Skills.SkillType, Skills.Skill>());
    return skills;
}

int skillId = (int)GrapplingSkill.Type;
Require(skillId > (int)Skills.SkillType.All, "custom skill ID must remain outside the native enum");
Require(skillId == 65007061, "saved Grappling ID must stay stable across releases");
Game.m_skillGainRate = 1f;

Skills source = NewSkills();
source.m_skills.Add(new Skills.SkillDef { m_skill = Skills.SkillType.Jump });
GrapplingSkill.Register(source);
source.RaiseSkill(GrapplingSkill.Type, 0.25f);
Skills.Skill earned = source.GetSkillList().Find(value => value.m_info.m_skill == GrapplingSkill.Type)
    ?? throw new InvalidOperationException("native RaiseSkill did not create Grappling data");
Require(earned.m_level == 0f && Math.Abs(earned.m_accumulator - 0.25f) < 0.0001f,
    "native Skills must hold Grappling XP progress before saving");
earned.m_level = 7f; // Preserve existing levels while keeping a partial next-level accumulator.

var package = new ZPackage();
source.Save(package);
byte[] savedSkills = package.GetArray();
Require(savedSkills.Length > 0, "native Skills.Save must write a payload");
var saved = new ZPackage(savedSkills);
Require(saved.ReadInt() == 2, "native save payload version");
Require(saved.ReadInt() == 1, "native save contains the Grappling skill");
Require(saved.ReadInt() == skillId, "native save uses the pinned Grappling skill ID");
Require(Math.Abs(saved.ReadSingle() - 7f) < 0.0001f, "native save preserves learned levels");
Require(Math.Abs(saved.ReadSingle() - 0.25f) < 0.0001f, "native save preserves fractional progress");

// Exercise the actual production load-prefix and validity postfix methods.
// The installed native Skills.Load -> IsSkillValid seam is separately checked
// against this assembly's decompiled source by grappling-test.sh; the game
// Harmony patcher itself cannot initialize under this standalone CoreCLR host.
Skills restored = NewSkills();
restored.m_skills.Add(new Skills.SkillDef { m_skill = Skills.SkillType.Jump });
typeof(GrapplingSkillLoadPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, new object[] { restored });
Require(restored.m_skills.Exists(definition => definition.m_skill == GrapplingSkill.Type),
    "production Load prefix must register the custom definition before native Load resolves it");
object?[] validityArguments = { GrapplingSkill.Type, false };
typeof(GrapplingSkillValidationPatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, validityArguments);
Require((bool)validityArguments[1]!, "production validity postfix accepts the custom saved ID");

System.Console.WriteLine($"Installed Skills.Save payload and Grappling load hooks passed for ID {skillId}; native Load wiring source-checked");
