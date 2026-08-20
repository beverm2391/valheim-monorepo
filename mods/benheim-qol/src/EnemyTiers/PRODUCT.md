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

Ben confirmed that the `0.1.56` large-map presentation looks good in gameplay.
Its native biome name remains on the first line, and the category text appears
alone on a second line. The category matches the native biome label's white
text treatment.

Ben confirmed in gameplay that the `0.1.61` candidate's one-star `1.4x` and
two-star `1.7x` Boars are visibly larger than ordinary Boars. This proves only
the visible size change. Physical coherence among the visible body, collision
capsule, and player contact felt slightly off. It remains unproven and in
development.

Ben saw the cyan collider overlay on live starred Boars in `0.1.66` and said it
looked decent. Ben accepted the overlay's presentation. He did not accept the
Boar profile's physical coherence or tuning.

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

Players must recognize a tier immediately. Native stars and prefab styling are
the baseline. Test any stronger physical identity on one named creature before
generalizing it.

### Boar physical tier experiment

The `0.1.61` candidate makes native starred Boars physically larger. A one-star
Boar is `1.4x` ordinary size, and a two-star Boar is `1.7x` ordinary size. The
visible body and collision capsule grow together. Both starred tiers use
Valheim's closest existing larger-creature navigation profile. An ordinary
zero-star Boar keeps its native presentation and physical behavior.

Wild starred Boars should detect players earlier, pursue them longer, and close
faster along a more committed path that turns less cleanly. One-star Boars
should feel territorial; two-star Boars should feel more relentless. Starred
Boars should resist routine displacement and may shove players more strongly,
but neither tier should be immovable. Players should counter a hostile Boar
with lateral movement, dodging, heavy attacks, or Perfect Impact.

The physical profile must derive only from the native Boar prefab and native
star level. It applies equally to wild and tamed Boars. It must restore after
spawn, reload, ownership migration, breeding, and growth without custom saved
state. Every peer that can own a Boar must apply the same physical profile.
Benheim supplies that behavior on clients, and Benheim Test Commands supplies
it while the dedicated server owns a spawned test Boar. Lifecycle restoration
and multiplayer ownership remain unproven.

For physical inspection, `bh debug colliders on|off` locally shows a thin
wireframe around the actual active collision capsule of each nearby non-player
Character. The transient overlay follows the live collider as it moves or
changes, allowing the visible body and head area of a starred Boar to be judged
against its physics shape. It does not change physics, networking, or saved
state. Characters without a supported active capsule are not drawn. The
Ben accepted how the overlay looked on live starred Boars. It remains unproven
that every overlay disappears when the player turns the overlay off or during a
world transition.

This experiment changes Boar physical identity, force exchange, perception,
pursuit, charge speed, and turning by native star level. The exact behavior and
numeric tuning are experimental and remain open. It does not add resistances,
elemental effects, or new attacks; speed up attack animations; or add damage
beyond Valheim's native star scaling. It also does not retune mass, swim depth,
numeric attack reach, breeding rules, or spawning rules. The complete fight,
larger collision, pen and gate navigation, slopes, water behavior, and
practical bite reach remain gameplay-unproven.

For this experiment, a native administrator using Benheim `0.1.66` can request
an ordinary Boar as a control or request a one-star or two-star native Boar.
This requires Benheim Test Commands `0.1.1` on the dedicated server. The Boar
spawn command remains gameplay-unproven.
[Benheim Test Commands](../../../../server-mods/benheim-test-commands/PRODUCT.md)
owns the exact allowlist, admin validation, spawn authority, and result
behavior.

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

The labels split the constructed `10%` to `40%` range into four equal fixed
bands:

- **Safe:** below `17.5%`;
- **Sketchy:** from `17.5%` to below `25%`;
- **Dangerous:** from `25%` to below `32.5%`; and
- **Deadly:** `32.5%` or higher.

The label does not reveal unexplored areas. It covers ordinary wilderness
only. It does not estimate danger for dungeons, events, Alphas, or other
authored encounters.

Ben accepted the small minimap's white native-styled presentation in `0.1.65`.
The native biome label stays unchanged. The title-case category `Safe`,
`Sketchy`, `Dangerous`, or `Deadly` sits directly below it on the same native
right edge. Both lines come from the same current player-area sample, which
Benheim takes about every `0.25` seconds. The minimap publishes the factual
category without applying the arrival logic's stability delay or boundary
hysteresis. The accepted large-map categories and the danger words in arrival
messages remain uppercase.

An unlisted biome keeps the native biome name without a Benheim category.
During a portal or loading transition, the map labels never expose an
unresolved localization token: the minimap keeps its last valid native biome
name without a category, while the large-map label remains empty. The `0.1.59`
candidate waits until Valheim reports the first valid destination area. On the
first `0.25`-second sample after that report, the minimap should update its biome
and category together. The arrival banner and effects continue to use the
existing stability delay and hysteresis. This change does not affect large-map
behavior.

When the current category stably rises to `DANGEROUS` or `DEADLY`, Benheim
shows `Entering a DANGEROUS area...` or `Entering a DEADLY area...`.
Each exact arrival sentence stays on one line in Valheim's biome-discovery
presentation, with its native ornament separate from the text. The presentation
also uses Valheim's one-shot stinger and native damage flash. The damage flash
remains gameplay-unproven until the next visual retest.

Turning off Danger Arrival FX through Benheim's FX settings suppresses only
future arrival messages, stingers, and flashes. It does not hide either map
label, change danger classification or spawning, or stop a one-shot that has
already begun.

After login or respawn, the dramatic arrival logic records the initial category
without showing a message. This logic pauses during teleporting, cutscenes, and
sleep. It waits for a stable category, ignores brief crossings near a category
boundary, and uses a shared arrival cooldown. These rules prevent repeated
dramatic messages while the player remains in one category or moves near a
category boundary.

This presentation does not control music or weather. It does not create an
event, spawn an object, damage the player, or change the world.

Creature exceptions remain open.

## Future Ideas

After the current one-shot arrival experiment, Benheim may add richer local
ambience to make dangerous areas feel more distinct. This idea does not promise
control of music, weather, events, or world state.

### Candidate: Methsquito

Methsquito could become a named two-star Deathsquito behavior variant. It is a
candidate, not approved behavior, and Valheim feasibility is unproven.

Once it becomes aggressive toward a player, Methsquito would behave like a
flying fuse. A clear spatial sound sequence would escalate toward an
unmistakable final cue. It would then commit to a kamikaze dive, explode, and
die in the attempt.

The player could time a native dodge roll to avoid the detonation or block and
survive it at a large stamina and knockback cost. A future perfect-parry
redirect remains conditional. Technical research must first prove it feasible,
and gameplay testing must prove it readable and reliable. The explosion would
also damage nearby enemies. Skilled players could lead the Methsquito into a
group and deal enough damage to make turning the threat into a weapon worth the
risk.

The goal is one terrifying, learnable interaction with several answers and an
emergent offensive use. It is not a health sponge or a generic stat increase.
This candidate applies Benheim's existing challenge-and-counter principle; the
root [`PRODUCT.md`](../../PRODUCT.md) owns that principle.

The timer, sound and visual effects, targeting and redirect rules, exact damage
and radius, any damage against friendly targets or the world, block and parry
outcomes, spawn eligibility, rewards, and final tier placement remain open.

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
