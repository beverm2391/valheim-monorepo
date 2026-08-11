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

Develop tier behavior from the least invasive change to the most invasive:

1. Test the changed star distribution by itself.
2. Tune Valheim's existing movement, perception, pursuit, attack spacing,
   resistance, and stagger controls by tier.
3. For a named creature, change how it selects attacks that it already owns.
4. Consider custom pathfinding or reuse of a compatible boss or miniboss attack
   only when a specific fight justifies the added algorithm, prefab, animation,
   and networking cost.

Make a small playable change at each layer before adopting a more invasive
layer. Do not build a generic pathfinding or boss-attack system until a
specific creature's fight proves that its gameplay value justifies the
complexity.

Biome and distance from the world center are separate inputs to an ordinary
wilderness spawn's chance of gaining a star level. Each biome sets a minimum
and maximum chance. Harder biomes use higher minimum and maximum chances.
Within a biome, its base chance increases for spawns farther from the world
center.

An independent world-distance term adds from `0` percentage points at the
world center to `10` percentage points at Valheim's `10,000`-meter world edge.
Benheim adds this term to the biome chance. It does not compound the two inputs
or apply a hard cap. The approved inputs construct a final range from `10%` to
`40%`.

The biome-specific chance ranges can create a sawtooth tendency, but the
combined result does not need to form a perfect sawtooth. A biome transition
can raise, lower, or preserve the final chance. Do not tune either input solely
to force a perfect sawtooth.

The first playable tuning uses these per-step biome base chances before it
adds the world-distance term:

| Biome | Base chance at world center | Base chance at world edge |
| --- | ---: | ---: |
| Meadows | 10% | 12% |
| Black Forest | 10% | 18% |
| Swamp | 12% | 22% |
| Mountain | 14% | 24% |
| Plains | 16% | 27% |
| Mistlands | 18% | 30% |

Benheim samples the biome at each spawn point. It linearly interpolates that
biome's approved base-chance range by normalized absolute distance from the
world center: `0` at the center and `1` at Valheim's `10,000`-meter edge. This
calculation does not inspect or mirror procedural biome-generation boundaries.
Ocean, Ashlands, Deep North, and every other unlisted biome keep native star
chances in this first tuning pass. These values are starter balance for
gameplay testing, not permanent balance.

Player progression and first visits do not change ordinary wilderness star
chances. Shared world progression can control separate encounters and rewards
when their product design needs it.

Specific, authored encounters can later add recognizable danger. Do not add
danger merely because a player is near a generic structure. Time should enable
recognizable special encounters instead of invisibly strengthening every
creature at night.

Players read ordinary wilderness danger from the world map instead of reading
the underlying percentages. The large map adds an explored-only hover label to
the biome name. The label uses the same final per-step ordinary-wilderness
chance that spawning uses. Benheim computes it only for the explored point
under the cursor. It does not precompute or tint the world map.

The category appears alone on a second line beneath the native biome name.
Color and weight show the danger level: `SAFE` uses calm green, `SKETCHY` uses
warning gold, `DANGEROUS` uses bold orange, and `DEADLY` uses bold red.

The labels split the constructed `10%` to `40%` range into four equal fixed
bands:

- **Safe:** below `17.5%`;
- **Sketchy:** from `17.5%` to below `25%`;
- **Dangerous:** from `25%` to below `32.5%`; and
- **Deadly:** `32.5%` or higher.

The label does not reveal unexplored areas. It covers ordinary wilderness
only. It does not estimate danger for dungeons, events, Alphas, or other
authored encounters.

Creature exceptions remain open.

## Planned Alpha Variants

An Alpha is a separate Benheim enemy variant. It is neither a native star level
nor Valheim's internal level-four creature. It keeps the creature's existing
prefab, animations, attacks, and baseline AI. Its Alpha identity must survive
ownership changes, reloads, and updates to compatible Benheim versions.
Benheim stores that identity. It derives the current tuning from the identity
instead of storing copies of its multipliers.

The first Alpha slice replaces an eligible ordinary hostile wilderness spawn
at night. It does not add another creature to the population. The initial
tuning candidate gives each eligible spawn a `5%` Alpha chance when no Alpha is
already alive nearby in the current biome's active spawn area. This is a local
encounter limit, not one Alpha per biome across the entire world. Exact
eligibility and the local range remain open pending native-source research and
gameplay testing.

The shared Alpha profile gives the creature an immediately menacing appearance
and faster movement. For an Alpha, size can increase when the change does not
break the creature's physical behavior. Health, damage, stagger resistance,
and rewards use explicit Alpha tuning. The first slice does not speed attack
animations or invent attacks. Later creature-specific rules can change
behavior through the creature's existing attacks and animations.

An Alpha keeps the level-aware drops of a native two-star creature. Drops that
opt into level scaling use up to a `4x` multiplier. An Alpha never inherits
internal level four's `8x` drop multiplier. Each Alpha also grants one bonus
reward roll from a curated pool of valuable existing resources. The group's
shared world progression gates that pool. Candidate rewards include Fine Wood,
Surtling Cores, Scrap Iron, strong food, feasts, and mead. World progression
controls the reward pool, not the discoveries of the player who kills the
Alpha. Exact rewards, weights, and quantities remain open.

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
