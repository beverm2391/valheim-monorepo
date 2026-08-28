# Game design resources

Use this shelf to retrieve source material. [`NOTES.md`](NOTES.md) keeps the
notes and open questions drawn from it. Neither file makes a Benheim
product decision or proves a Valheim implementation.

## Enemy AI and coordination

- https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf - *Beyond the Kung-Fu Circle*, Michael Dawe / Game AI Pro: a shipped *Kingdoms of Amalur* attack manager with approach slots, attack capacity, and rotation. Return for an explicit multi-enemy pressure budget.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter16_Open-world_Enemy_AI_in_Mafia_III.pdf - *Open-world Enemy AI in Mafia III*, Jiri Holba and Gael Huber / Game AI Pro: data-driven archetypes and variations, utility position selection, decision-tree actions, and position-score stability. Return for elite configuration and decisive-looking movement.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter12_Squad_Coordination_in_Days_Gone.pdf - *Squad Coordination in Days Gone*, Tobias Karlsson / Game AI Pro: group goals, roles, confidence, and press/hold/retreat behavior. Return for a coordination layer above individual creatures.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter05_Taming_Spatial_Queries_Tips_for_Natural_Position_Selection.pdf - *Taming Spatial Queries*, Eric Johnson / Game AI Pro: side bias and hysteresis for stable flanking, circling, retreat, and attack setup. Return when position scoring causes orbit jitter.
- https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter06_Flooding_the_Influence_Map_for_Chase_in_Dishonored_2.pdf - *Flooding the Influence Map for Chase in Dishonored 2*, Laurent Couvidou / Game AI Pro: pursuit from an uncertain player-location hypothesis, then investigate, give up, and search. Return for believable chase recovery without omniscience.
- https://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter31_Behavior_Decision_System_Dragon_Age_Inquisition%E2%80%99s_Utility_Scoring_Architecture.pdf - *Dragon Age: Inquisition's Utility Scoring Architecture*, Sebastian Hanlon and Cody Watts / Game AI Pro: legal action gating, target/weakness scoring, separate action execution, and debug-visible results. Return when comparing random attack selection with a scored system.

## Combat timing, hit contact, and state

- https://www.gdcvault.com/play/1026423/Evolving-Combat-in-God-of - *Evolving Combat in God of War for a New Perspective*, Mihir Sheth / Sony Santa Monica Studio: aggression tokens, threat readability, interruption, and multi-enemy pressure in a close third-person game.
- https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing - *Enemy Attacks and Telegraphing*, Mike Stout / Game Developer: practitioner treatment of what an avoidable attack must communicate through animation, sound, visual effects, and other channels.
- https://www.ubisoft.com/en-au/game/for-honor/news-updates/66WvNXYWFKSpanlTv5rXBQ/testing-grounds-fight-system-improvements - *For Honor Testing Grounds: Fight System Improvements*, Ubisoft: concrete timing, recovery, damage, stamina, indicator, and commitment changes with rationale.
- https://www.ubisoft.com/en-us/game/for-honor/news-updates/5UUQbycvJ4CTmPoNcWTzBK/release-notes-for-v105 - *For Honor v1.05 release notes*, Ubisoft: input buffering, recovery-to-chain branches, armor, interruption, and animation/strike-zone synchronization as shipped combat data.
- https://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter11_A_Character_Decision-Making_System_for_FINAL_FANTASY_XV_by_Combining_Behavior_Trees_and_State_Machines.pdf - *A Character Decision-Making System for FINAL FANTASY XV*, Square Enix / Game AI Pro: behavior trees plus body state, animation, interruption, attack volumes, and monster-specific overrides.
- https://www.gamedeveloper.com/design/designing-great-hitboxes-for-a-2d-beat--em-up-with-swords-and-shields - *Designing great hitboxes for Wulverblade*, Christian Nutt / Game Developer: designer-tuned hurt zones and swept attack queries that stay legible against animation.
- https://sourcegaming.info/2016/03/31/dengeki2015/ - *Sakurai interview on custom moves, version differences, and balance*, Masahiro Sakurai / Source Gaming: hit state and percent damage as sources of situational follow-ups, not one prescribed combo. Unofficial translation.

## Difficulty, enemy roles, and progression

- https://www.davetech.co.uk/difficultycurves - *Making difficulty curves in games*, David Strachan: rise-and-recovery lessons, accidental spike smoothing, observation, and optional mastery challenges.
- https://blog.playstation.com/?p=359935 - *An interview with FromSoftware's Hidetaka Miyazaki*, Hidetaka Miyazaki / PlayStation Blog: high challenge, stance breaks, study, alternate routes, returning later, and multiplayer help without one required playstyle.
- https://www.gamespot.com/articles/doom-eternal-is-a-fantasy-combat-puzzle-but-what-d/1100-6467505/ - *DOOM Eternal Is a Fantasy Combat Puzzle*, Hugo Martin and Marty Stratton / GameSpot: fair failure states, available tools, and encounters that teach a changed next attempt.
- https://www.gamedeveloper.com/design/horizon-zero-dawn-design-analysis - *Horizon: Zero Dawn Design Analysis*, Stanislav Costiuc / Game Developer: secondary analysis of exposed weak points, armor, and escalation through familiar counterplay.
- https://www.youtube.com/watch?v=50mIKB-NACU - *Animation Bootcamp: Bringing Life to the Machines of Horizon Zero Dawn*, Richard Oud / Guerrilla Games: primary GDC follow-up on reference, animation, AI behavior, and polish for creature work.

## Systemic interactions

- https://www.youtube.com/watch?v=QyMsF31NdNc - *Designing Zelda: Breath of the Wild's Unconventional Mechanics*, Nintendo: primary GDC talk on a world where interactions among objects, systems, and players create chemistry.

## Ongoing practitioner publications and archives

- https://www.gameaipro.com/ - *Game AI Pro*, Game AI Pro: publisher-authorized technical anthology/archive with shipped-game chapters, pseudocode, data schemas, debugging, and production constraints. Browse chapter by chapter because genre and engine context matter.
- https://www.aiandgames.com/ - *AI and Games*, Tommy Thompson: ongoing expert analysis of AI research and shipped-game applications. Good for vocabulary and hypotheses to verify against primary material.
- https://www.gamedeveloper.com/design - *Game Developer: Design*: developer interviews, postmortems, GDC reprints, and commentary. Prefer named practitioners and primary links over general advice.
- https://www.gamedeveloper.com/programming - *Game Developer: Programming*: technical interviews and implementation writeups. Useful for finding practitioner accounts of combat, animation, and tooling work.
- https://www.gdcvault.com/ - *GDC Vault*: conference archive for original developer talks. Use talk pages and accompanying slides or transcripts to verify claims.
- https://www.ubisoft.com/en-us/game/for-honor/news-updates - *For Honor* news and updates, Ubisoft: a live-system archive for exact timing, recovery, chain, hit-reaction, and input-window changes with rationale.
