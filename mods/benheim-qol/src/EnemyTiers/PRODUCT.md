# Enemy Tiers

Enemy Tiers turns Valheim's stars into a clear warning that a familiar enemy
will be more lethal, more durable, and more demanding to fight. Benheim keeps
the native tier signal, then adds shared tier rules and creature-specific
behavior that players can learn and counter.

## Current Behavior

Current Benheim does not change native stars. For an eligible ordinary spawn,
Valheim `0.221.12` typically uses this baseline:

| Effect | No stars | One star | Two stars |
| --- | ---: | ---: | ---: |
| Typical eligible spawn rate | 90% | 9% | 1% |
| Maximum health | 1x | 2x | 3x |
| Attack damage | 1x | 1.5x | 2x |
| Level-aware loot | 1x | 2x | 4x |
| Absolute stagger threshold | 1x | 2x | 3x |
| Resistance profile | Native | Native | Native |
| AI and attacks | Native | Native | Native |

Some creatures and spawn entries cannot gain stars or use different chances.
Only loot that opts into level scaling receives the listed multiplier.

Valheim shows stars in the enemy HUD. Each creature prefab can also change its
size, color, emission, or visible features by level. Valheim does not apply one
universal visual style to every starred creature.

[`RESEARCH.md`](RESEARCH.md) owns the code evidence, multiplayer authority,
extension seams, and Valheim `1.0` revalidation gate behind this baseline.

## In Development

Enemy Tiers has two layers. Global tier rules give every affected enemy a
consistent difficulty and a clear visual signal. Creature-specific rules deepen
the identity of each enemy instead of drawing unrelated powers from a generic
pool.

Higher tiers should remain durable and able to kill an unprepared player
quickly. Players should overcome them through skill, preparation, builds,
equipment, tools, and knowledge.

Players must recognize a tier immediately. Benheim can use a consistent color
or emission progression with the native HUD stars. Size can change for selected
creatures when the change remains visually clear and does not break the
creature's physical behavior.

Creature-specific tier rules can change AI, attack patterns, and resistance
profiles. Each change must extend that creature's existing combat identity and
give the player a readable reason to change tactics.

Benheim is likely to change how often eligible variants spawn. Exact rates,
progression gates, and creature exceptions remain open.

Future alpha monsters can feel like rare minibosses. No alpha rules, first
creature, exact visual treatment, spawn distribution, or tier mechanic is
decided yet.
