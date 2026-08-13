# Game design source library

This is a small library for asking better combat-design questions before mapping
an idea onto Valheim. It does not approve a Benheim mechanic, tune a value, or
choose an implementation. Feature `PRODUCT.md` files remain the product source
of truth; native-source research remains the source for technical fit.

## Start with the player-facing principle

Start with the player-facing principle, not the borrowed mechanic. State what a
player should notice, learn, choose, or overcome. Then test whether Valheim can
express that principle through its native actions, states, camera, co-op,
networking, and compatibility boundaries. A resource can frame an experiment.
It never becomes a Benheim decision automatically.

## Enemy pressure and encounter cadence

### [Evolving Combat in *God of War* for a New Perspective](https://www.gdcvault.com/play/1026423/Evolving-Combat-in-God-of)

- **Source type:** GDC developer talk by Mihir Sheth, Lead Combat Designer at Sony Santa Monica Studio.
- **Useful principle:** Regulate simultaneous enemy pressure with readable state transitions. A player interruption can create a short, earned opening across the encounter instead of only stopping one enemy.
- **Can inform:** How many enemies may actively threaten a player, which enemy roles consume the pressure budget, and what a stagger or interruption should visibly buy in co-op.
- **Evidence limit:** The full GDC deck describes an aggression-token system for a close third-person game with bespoke targeting and animation. Do not import its token counts, indicators, or hit-assist behavior.

### [Embracing Push Forward Combat in *DOOM*](https://www.youtube.com/watch?v=2KQNpQD8Ayo)

- **Source type:** GDC developer talk by id Software's Kurt Loudy and Jake Campbell.
- **Useful principle:** A combat loop's resources and enemy rules must reinforce its intended tempo instead of rewarding the opposite behavior.
- **Can inform:** Whether Benheim should reward pressing an earned opening, creating space, changing targets, or resetting after risk.
- **Evidence limit:** The published description establishes the talk's push-forward premise, not a transferable Valheim economy. *DOOM* is a fast FPS power fantasy, so copying its resource loop would be a category error.

## Telegraphs and readable enemy roles

### [Enemy Attacks and Telegraphing](https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing)

- **Source type:** Practitioner analysis by Mike Stout.
- **Useful principle:** If an attack requires avoidance, it must communicate both the danger and the possible response in time. Animation, sound, voice, visual effects (VFX), and force feedback can reinforce one another.
- **Can inform:** For every damaging move: what players notice, which response they infer, how long they have, and which signals remain clear in chaotic co-op.
- **Evidence limit:** This is practitioner guidance, not a shipped-game postmortem. Verify readability with actual Benheim play, camera distance, latency, and player footage.

### [Animation Bootcamp: Bringing Life to the Machines of *Horizon Zero Dawn*](https://www.youtube.com/watch?v=50mIKB-NACU)

- **Source type:** GDC developer talk by Guerrilla Games' Richard Oud.
- **Useful principle:** Creature readability is an integrated animation, AI, and polish problem rather than a stat block added after the enemy is complete.
- **Can inform:** What a compact enemy-role brief must establish before scaling a variant: role, silhouette, movement intent, attack tell, state change, and counterplay.
- **Evidence limit:** The official description confirms the talk's end-to-end scope. Watch the talk before attributing a particular telegraph technique or production method to Guerrilla.

## Weaknesses and loadout choices

### [Horizon: Zero Dawn Design Analysis](https://www.gamedeveloper.com/design/horizon-zero-dawn-design-analysis)

- **Source type:** Serious secondary analysis by Stanislav Costiuc for Game Developer.
- **Useful principle:** Teach a role's counterplay in its simplest readable form, then vary that role through protection or context while preserving recognition.
- **Can inform:** Whether a basic enemy can introduce one visible weakness, while an elite protects, repositions, or combines that same decision instead of only gaining health and damage.
- **Evidence limit:** This is an interpretation of the shipped game, not Guerrilla's stated intent. Check play footage or primary material before treating its detailed reading as fact.

### [Designing Zelda: *Breath of the Wild*'s Unconventional Mechanics](https://www.youtube.com/watch?v=QyMsF31NdNc)

- **Source type:** GDC developer talk by Nintendo's Hidemaro Fujibayashi, Satoru Takizawa, and Takuhiro Dohta.
- **Useful principle:** Reusable interactions among a small set of world properties can create more tactical choice than one-off encounter scripts.
- **Can inform:** Which existing Valheim properties an enemy can participate in without a parallel system. These include wetness, burning, poison, terrain, knockback, weather, and destructibles.
- **Evidence limit:** This is a whole-world design philosophy, not proof that any candidate interaction fits Valheim, its compatibility promise, or its technical boundaries.

## Movement, timing, and follow-up expression

### [Sakurai interview on custom moves, version differences, and balance](https://sourcegaming.info/2016/03/31/dengeki2015/)

- **Source type:** Primary designer interview with Masahiro Sakurai, presented in an unofficial English translation by Source Gaming.
- **Useful principle:** Contact can change the next decision. Follow-ups become situational when the combat state changes. Players then have more than one meaningful response.
- **Can inform:** Which branches should open after a hit, displacement, stagger, or recovery: press, use a heavy attack, reposition, change targets, or use terrain.
- **Evidence limit:** This is neither a *Melee*-specific design breakdown nor an official translation. Do not infer intent for wavedashing, L-cancelling, or other emergent techniques from it.

## Difficulty, variants, and escalation

### [Making difficulty curves in games](https://www.davetech.co.uk/difficultycurves)

- **Source type:** Independent developer essay by David Strachan.
- **Useful principle:** Teach each new mechanic through a repeating cycle: safe introduction, manageable danger, combination with prior lessons, pressure, then an optional mastery test. Smooth accidental spikes, retain recovery and agency, and use play evidence to tune.
- **Can inform:** Whether an Enemy Tiers behavior experiment is a learnable escalation rather than an opaque spike, and which player observations distinguish a learnable escalation from an opaque spike.
- **Evidence limit:** The 2018 essay uses mainly platformer and puzzle examples. It does not establish multiplayer balance, open-world survival tuning, Valheim AI behavior, or a statistical method.

### [An interview with FromSoftware's Hidetaka Miyazaki](https://blog.playstation.com/?p=359935)

- **Source type:** Official developer interview on PlayStation Blog.
- **Useful principle:** High challenge should be learnable and recoverable. Players need more than one valid approach. They can prepare, take alternate routes, return later, or get co-op help.
- **Can inform:** Whether a severe enemy has readable interruption or resistance states and several viable answers through gear, terrain, group help, disengagement, or preparation.
- **Evidence limit:** The interview does not support claims about delayed attacks, exact recovery timing, or a combat implementation. Elden Ring's open-world freedom is broader than a Valheim encounter.

### [DOOM Eternal Is a “Fantasy Combat Puzzle,” But What Does That Mean?](https://www.gamespot.com/articles/doom-eternal-is-a-fantasy-combat-puzzle-but-what-d/1100-6467505/)

- **Source type:** Developer interview with Hugo Martin and Marty Stratton.
- **Useful principle:** A difficult encounter should leave a player able to diagnose the failed decision and try something different next time, not suffer opaque attrition.
- **Can inform:** Whether each new enemy or elite presents a clear problem and several counters, rather than requiring only more damage, food, or gear score.
- **Evidence limit:** Requiring a specific weapon as the counter can make a shared Valheim kit restrictive. Keep several viable responses and verify that failure is visible in the actual game.
