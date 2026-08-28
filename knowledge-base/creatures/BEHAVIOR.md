# Creature Behavior

Behavior decides what a creature notices, where it moves, how long it pursues,
and when it creates space or attacks. Native level does not select different
behavior. The evidence baseline and ownership rules live in
[Creature Mechanics](CREATURE-MECHANICS.md).

## Sensing And Navigation

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| View range and angle, hearing range, mist vision | How early stealth, noise, facing, and cover matter | Generic sight and hearing tests use prefab values plus target stealth and noise. Native level does not alter them. | The creature owner senses targets. Deterministic variants need compatible owners. |
| Path-agent type and walk, swim, and water rules | Which terrain and passages the creature can traverse | Generic pathfinding consumes a prefab-selected agent family and movement capabilities. | Test each creature with its collider and authored environment. Changing the path-agent type does not change the creature's body or animation. |
| Obstacle avoidance, move angle, smooth or serpent movement, stuck recovery | Cornering, local avoidance, and apparent navigation competence | Shared movement code combines prefab toggles with character radius and speed. | Owner-side and transient. Scale or radius changes can invalidate avoidance. |
| Random movement, jump, flight altitude, and takeoff | Idle movement and traversal style | Generic capabilities are enabled and tuned per prefab. | Animator and body compatibility are required. Native level adds no variation. |

## Combat Decisions

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Alert range and state, hunt-player, target memory | Awareness, stealth pressure, and whether pursuit begins or continues | Generic logic uses prefab ranges and selected ZDO-backed flags. | Only the current owner selects targets. |
| Maximum chase distance and unreachable-target timers | How far and how stubbornly a creature pursues | Prefab values feed shared timeout and spawn-point rules. | A larger chase budget without navigation proof can create stuck or kited enemies. |
| Interception time | Whether pursuit leads a moving target | Generic velocity prediction uses a prefab range sampled at startup. | This is owner-side transient state. Stable variants need a deterministic derivation. |
| Circling and circulate-while-charging | Repositioning instead of running directly at the target | Shared states are enabled and timed per prefab, with separate flying support. | Movement, attack range, body shape, and animation constrain useful values. |
| Retreat and flee rules | Self-preservation, regrouping, and encounter rhythm | Shared logic responds when the creature is hurt, has low health, is on fire or in lava, detects pheromone, or cannot reach its target. | The owner runs this logic. Native level does not select new thresholds. |
| Minimum attack interval and charge or wait behavior | Overall spacing between eligible attacks | The creature-wide `MonsterAI` interval and each attack item's interval both limit when the next attack can begin. | The owner decides when the next attack may begin. |

Valheim exposes path-agent choice, obstacle avoidance, movement modes, stuck
recovery, and pursuit rules. These are useful configuration seams. They are not
a general high-level combat planner. Custom pathfinding or defensive reactions
would add a new decision algorithm and should begin with one specific fight.

## Reproduction Map

- Sensing and target checks: `BaseAI.CanHearTarget()`, `CanSeeTarget()`,
  `IsEnemy()`; `MonsterAI.UpdateTarget()`.
- Navigation and avoidance: `BaseAI.MoveTo()`, `MoveAndAvoid()`;
  `Pathfinding.HavePath()`, `GetPath()`.
- Pursuit and positioning: `MonsterAI.UpdateAI()` and its chase, interception,
  circle, and flee branches.
- Attack spacing: `MonsterAI.SelectBestAttack()`, `DoAttack()`;
  `BaseAI.CanUseAttack()`.
