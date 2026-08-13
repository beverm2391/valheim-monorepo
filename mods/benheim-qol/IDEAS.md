# Benheim Ideas

This is a curated notebook for ideas Ben wants to keep exploring. It is not a
product promise, implementation plan, or proof record. When an idea enters
development, its behavior and status move into the owning `PRODUCT.md`.

## Stealth and Archery

- Grow a long-term stealth-and-sniping play style around global arrow headshots,
  distance, and Valheim's native unaware/backstab behavior.
- **The Grassy Knoll:** a dedicated scoped bow, kitbashed from a native longbow
  with a crystal and greydwarf-eye optic plus a custom icon. Holding secondary
  attack gives roughly 3x zoom and shows distance and the possible headshot
  multiplier, without aim assist or hitscan. Balance it with reduced movement
  and peripheral vision plus higher stamina cost or slower draw. Bow skill can
  reduce scope sway and stamina cost. It should work with special arrows.
  Tooltip: “From this angle, there may have been a second archer.”
- Special arrows should be tactical toys that combine with one another. Unlock
  recipes through discovering native materials; let Bow skill and headshots
  improve their mechanics instead of creating another mastery currency or tree.
  - **Fuck-Off Arrow:** nearly no damage, obscene direct knockback, and a smaller
    radial shockwave. A headshot launches the target absurdly far.
  - **Discord Arrow:** the target temporarily attacks every faction. A headshot
    can spread the effect to its nearest ally.
  - **Tar Arrow:** coats the target and leaves a slowing puddle that also affects
    players who enter it. A headshot strengthens the impairment.
  - **Chain Arrow:** ricochets between targets with native chain VFX. Bow skill
    adds jumps, damage falls off, and later hits add knockback.
  - **Anchor / Codependency Arrow:** grounds one target or tethers two together.
    Do not require chitin initially because it is not yet unlocked.
  - **Neckromancy Arrow:** visually turns the target into a neck while preserving
    its original stats, attacks, and practical hit behavior. Spiritually
    essential, technically harder.

## Enemy Variants

- **Sniper:** a starred ranged enemy with much greater perception and attack
  range. Candidates include Skeleton Archers and Draugr Archers. A higher tier
  could also use a creature-specific native projectile, such as a fire or
  poison arrow. The creature, tier, projectile, accuracy, cadence, telegraph,
  and counterplay remain open pending prefab research and gameplay testing.

## World Fuckery

- **Berserk Ooze Bomb:** a heavy bomb with a short throw range that temporarily
  makes every affected creature ignore its normal faction and fight any nearby
  character, including the player and one another. It should turn a village or
  crowded fight into dangerous monster civil war, not safe mind control. Its
  cost, radius, duration, and eligible targets remain open. If the bomb proves
  fun, a later sling or launcher can extend its range while reusing the same
  bomb payload. Technical faction evidence is in
  [Creature Factions](src/CreatureMechanics/FACTIONS.md).
- **Diddy Party:** a raid announced as “Diddy party!” containing exactly one
  huge, glossy tar-dark greydwarf brute. It runs terrifyingly fast, strongly
  resists stagger, and has enough health to create a chase without one-shotting
  players. It drops mead, tar, and coins; resin may be a visual joke.
- **Prophet Boar / Boartholomew the Knowing:** one shared world friend, using a
  native boar reshaped into an implausibly broad, way-too-wide silhouette with a
  strange luminous color. He remains friendly rather than grotesque. Support
  tame/follow/stay behavior, make him extremely difficult or impossible to lose
  permanently, and occasionally show useful advice mixed with deranged
  philosophy above his head. A strong arrival is simply finding him seated by
  the main hearth one day, but the arrival mechanism is still open.
- **Inspector Neck:** an invulnerable neck follows players around their base,
  producing contextual overhead roasts based on real smoke, structural damage,
  support, comfort, storage, and similar conditions, then leaves with a final
  inspection verdict.
- **The Latter-Day Draugr:** exactly two friendly, invulnerable draugr
  missionaries arrive at the base and politely deliver overhead conversion
  dialogue about the Allfather. They leave when ignored.
- **Escalating death disappointment:** repeated deaths by the same player within
  a rolling window produce increasingly disappointed group-visible messages.
  Staying alive long enough resets the escalation.
- **Sleepwalking dreams:** sleeping can rarely show a dream message and wake the
  player a medium distance away. This needs conservative safe-destination logic.
- **Suspicious food:** rare `+1` or otherwise suspicious food variants trigger a
  bespoke altered game state rather than merely changing stats. Custom embedded
  icons are fair game.
- Keep character-to-neck hallucinations or transformations and occasional freak
  physics in the toy box. They are promising, but should not all become events
  or repeat the same creature-gag structure.
