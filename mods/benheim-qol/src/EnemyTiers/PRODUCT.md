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

Players must recognize a tier immediately. Native stars and existing prefab
styling are sufficient for now. Defer added color, emission, or size changes
until gameplay shows that this signal is insufficient.

Creature-specific tier rules can change AI, attack patterns, and resistance
profiles. Each change must extend that creature's existing combat identity and
give the player a readable reason to change tactics.

Location should be the main input for tier distribution. Biome, distance from
the world center, and encounter locations such as structures or dungeons should
create stable areas of danger. Time should enable recognizable special
encounters instead of invisibly strengthening every creature at night.

Exact location rules, night encounters, spawn rates, and creature exceptions
remain open pending research into the location and time information Valheim
makes available.

## Idea: Species Retaliation

Killing many creatures of one species within a short period could provoke a
temporary response from that species. Benheim would show the group a visible
warning before it triggered one temporary response:

- starred hunters;
- an elite pack;
- an alpha candidate; or
- a native-style raid.

Species Retaliation would make repeated kills of one species matter without
permanently scaling that species. The response would end, then Species
Retaliation would enter a cooldown.

The following details remain undecided:

- the kill threshold and time window;
- the exact warning, response, and reward; and
- which client or server controls the event.

Future alpha monsters can feel like rare minibosses. No alpha rules, first
creature, exact visual treatment, spawn distribution, or tier mechanic is
decided yet.
