# Game design research notes

This notebook preserves what was interesting in the sources, including open
questions. It does not decide a Benheim feature or establish that Valheim can
express a mechanism. [`RESOURCES.md`](RESOURCES.md) is the short retrieval
shelf.

## Coordinating more than one enemy

### Pressure budgets and turns

- https://www.gdcvault.com/play/1026423/Evolving-Combat-in-God-of - Mihir Sheth's *God of War* deck treats aggression as a shared encounter state. An enemy in hit reaction cannot become aggressive. Interrupting an aggressive enemy retains its token briefly, so the interruption creates an encounter-wide opening rather than immediately handing aggression to another enemy.
- https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf - Michael Dawe makes that shared state explicit with a stage manager. NPCs first request a grid slot near the player; specific attacks then spend a separate attack capacity. Heavy attacks can cost more capacity. Waiting NPCs remain outside the threat circle or at unoccupied slots, and an attacker releases its slot after attacking.
  - “Grid capacity and attack capacity work together to limit both the number and types…”
  - The interesting separation is approach permission versus permission for a particular move. This is more textured than merely capping the number of nearby enemies.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter12_Squad_Coordination_in_Days_Gone.pdf - *Days Gone* puts goals, roles, and role locations above individual AI. Confidence uses perceived strength, losses, weapons, armor, wounds, and suppression to choose press, hold, or retreat. Members may preempt their role for a higher-priority response.

Questions we have not answered:

- What is the player-facing benefit of an interruption in a Valheim group fight: breathing room, a clean heavy attack, a route to disengage, or permission to focus one target?
- Does pressure belong to a named encounter, a local group of creatures, or each creature's native behavior? Do not assume a central manager is necessary.

### Pursuit, circling, and retreat

- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter05_Taming_Spatial_Queries_Tips_for_Natural_Position_Selection.pdf - Eric Johnson documents how symmetrical flank choices create left-right weaving. Side bias, preference for the last winning position, and hysteresis prevent an NPC from replacing a destination unless the alternative is materially better. The chapter illustrates a 20% threshold and warns that too much hysteresis retains stale positions.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter16_Open-world_Enemy_AI_in_Mafia_III.pdf - When a *Mafia III* NPC's position score falls sharply, it stops and uses an open-space default instead of continuously hunting for a replacement while moving. “Stopping and shooting from open space makes the AI look more decisive.”
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter06_Flooding_the_Influence_Map_for_Chase_in_Dishonored_2.pdf - *Dishonored 2* chases a hypothesis about where the player went after perception is lost. The earlier linear extrapolation failed at local navigation boundaries and gave up too early. The resulting ladder has an explicit failed-search transition into slower search rather than quiet omniscience.

These are different problems: position thrash, visibly decisive fallback, and believable uncertainty. An influence map is likely excessive for ordinary melee pursuit; the useful comparison is the explicit pursue → investigate → give up → search shape.

## A move is a question, timing is the grammar

- https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing - Mike Stout reduces avoidance combat to two questions: can the player avoid damage, and can they still hit the enemy? Telegraphing may combine animation, sound, voice, visual effects (VFX), and force feedback. The source is practitioner analysis, so readability still needs player observation.
- https://www.ubisoft.com/en-au/game/for-honor/news-updates/66WvNXYWFKSpanlTv5rXBQ/testing-grounds-fight-system-improvements - In one *For Honor* experiment, Ubisoft hid the first 100 ms of animation and indicators consistently, normalized some light/heavy-finisher recoveries to 700 ms, and changed damage, stamina, and recovery together. “We want to move the game towards Read-based defense instead of Reaction-based defense.”
- https://www.ubisoft.com/en-us/game/for-honor/news-updates/5UUQbycvJ4CTmPoNcWTzBK/release-notes-for-v105 - The v1.05 notes give concrete input and response examples: 200 ms counter-guardbreak and recovery-to-chain branches, armor and guard-break vulnerability, and strike zones adjusted to match animation. One recovery-to-chain branch widened from 100 to 200 ms “to make it less challenging input-wise.”
- https://www.gamedeveloper.com/design/designing-great-hitboxes-for-a-2d-beat--em-up-with-swords-and-shields - *Wulverblade* used character-specific damage zones plus width-adjustable sphere queries along an attack path. “We didn’t want it to be too disconnected from the visuals…” The result separates visual silhouette, vulnerable area, and swept contact query while treating their player-facing coherence as one problem.

One useful review frame for an attack is not a new feature checklist: visible start, active contact, resource commitment, interruption, recovery, and what becomes legal next. A delayed move is only interesting when it changes the read instead of asking for the same response later. The For Honor numbers are competitive-PvP values, not targets.

## States, follow-ups, weaknesses, and variants

### State before architecture

- https://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter11_A_Character_Decision-Making_System_for_FINAL_FANTASY_XV_by_Combining_Behavior_Trees_and_State_Machines.pdf - Square Enix separates intelligence, a body finite state machine, and animation. Body state restricts legal actions and reports damage or external-force reactions upward. The team measured swept attack regions with spheres, simplified them into volumes, and assigned reach and angle to attack nodes. The chapter is valuable as evidence that commitment, active hit, reaction, and recovery can be made inspectable; it is not an engine-neutral recipe.
- https://sourcegaming.info/2016/03/31/dengeki2015/ - In an unofficial translation, Sakurai explains that percent damage changes an opponent's response after contact, which weakens fixed combo routes.

Question to keep open: Which branches remain available after a hit, displacement, stagger, or recovery: press, use a heavy attack, reposition, switch targets, or use terrain?

### Variants that change the decision

- https://www.gamedeveloper.com/design/horizon-zero-dawn-design-analysis - This serious secondary analysis reads early *Horizon* machines as teaching exposed weak points, then using armor and added behavior to protect or complicate that familiar counter. Its detailed interpretation should not be treated as Guerrilla's stated intent.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter16_Open-world_Enemy_AI_in_Mafia_III.pdf - *Mafia III* reports 18 base archetypes and 120 variations. Most variations override only a few settings, such as a Molotov or grenade choice. That gives a concrete alternative to a fully forked elite system.
- https://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter31_Behavior_Decision_System_Dragon_Age_Inquisition%E2%80%99s_Utility_Scoring_Architecture.pdf - BioWare enumerates legal action snippets, scores them with context-sensitive evaluation trees, selects one, then runs a separate execution tree. Target evaluation can exclude fire-immune targets and favor fire-vulnerable targets. Debug tables retain the scores. “Start by identifying the set of actions which it can legally take.”

The open design tension is not whether more variants are good. It is whether an elite preserves the creature's identity while changing a readable decision. A small moveset may not need a utility system, and a highest-score selector needs safeguards against constant switching or repeated choices.

### Systemic interactions, not a separate element layer

- https://www.youtube.com/watch?v=QyMsF31NdNc - Nintendo's *Breath of the Wild* GDC talk describes a world where objects, systems, and the player create interactions, often called chemistry.
- Our useful contrast is bespoke encounter scripting versus a small property that matters in several places. Possible Valheim-facing questions include wetness, burning, poison, terrain, knockback, weather, and destructibles, but no source here proves a particular interaction is compatible or worthwhile.

## Escalation and fair difficulty

- https://www.davetech.co.uk/difficultycurves - David Strachan rejects one monotonically rising curve. A new mechanic follows a rise-and-recovery cycle: safe introduction, manageable danger, combination with an earlier lesson, pressure, then an optional show-off challenge. He also recommends smoothing accidental spikes and inspecting death clusters, completion drop-offs, first play, and tester questions. The examples are mostly platformer and puzzle work.
- https://blog.playstation.com/?p=359935 - Miyazaki describes Elden Ring's stance break as similar to Sekiro posture and frames challenge as something players study, learn from, and overcome. Alternate routes, returning later, stealth, multiplayer help, and player-selected progression make more than one approach possible. The interview does not explain delayed attacks or exact recovery timing.
- https://www.gamespot.com/articles/doom-eternal-is-a-fantasy-combat-puzzle-but-what-d/1100-6467505/ - Hugo Martin calls an encounter a problem solved through aggression, skill, and available tools. Martin describes the desired failure state as a player understanding what to try differently after death. He calls unavoidable hitscan damage unfair. This is a useful test for opaque attrition, not a reason to adopt *DOOM*'s FPS resource loop or nonstop tempo.

Questions to carry into a playtest:

- Was a death legible enough that the player can name a missed tell, wrong position, unaddressed role, or different tool?
- Does the next attempt offer a changed decision, or only more health, food, or damage?
- Does a difficult sequence have a recovery beat, and is an optional mastery challenge actually optional?

## Practitioner trailheads

- https://www.gameaipro.com/ - *Game AI Pro* is the concrete technical archive for data schemas, pseudocode, debug tools, positioning, group tactics, and production constraints. Its chapters, not the archive label, are the evidence.
- https://www.aiandgames.com/ - Tommy Thompson's *AI and Games* is good for conceptual vocabulary and reverse-engineering hypotheses. Treat it as secondary analysis, not internal developer documentation.
- https://www.gamedeveloper.com/design and https://www.gamedeveloper.com/programming - *Game Developer* is useful for discovering named practitioners, postmortems, GDC reprints, and implementation interviews. The byline and primary links determine evidentiary weight.
- https://www.gdcvault.com/ - GDC's archive is the place to retrieve developer talks. A title or official description establishes scope; it does not verify unviewed talk details.
- https://www.ubisoft.com/en-us/game/for-honor/news-updates - *For Honor*'s update archive is a concrete record of live combat-system iteration. Individual notes can be superseded.
