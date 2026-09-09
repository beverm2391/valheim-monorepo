# Adrenaline research

This note owns the ways Benheim can change the native meter and charm. [Player
Combat research](PLAYER-COMBAT.md) owns the skill and risk signals
that can feed the meter or create an earned combat state.

## Evidence baseline

This note uses Valheim source evidence from version `0.221.12`. The inspected
`assembly_valheim.dll` had SHA-256:

```text
ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48
```

[`PROMPT.md`](../../PROMPT.md) owns the source-inspection commands. A
different assembly hash requires revalidation before implementation.

## Base-system levers

`Player.AddAdrenaline()` is Valheim's final entry point for adrenaline changes.
The current Benheim prefix, a hook that runs before this method, doubles every
positive grant before Valheim applies its rate, current-fill curve, status
modifiers, and meter cap. Negative changes remain native.

Benheim can tune ordinary gains, gains from specific actions, the delay before
decay, and decay speed. Meter capacity, spending, and full-meter behavior are
also possible product changes, but the code paths needed to change them have not
been audited for this redesign. They should remain native unless gain and decay
tuning fail to make the resource useful.

## Source-specific gains

Benheim already recognizes perfect parries and perfect dodges. Valheim also
awards adrenaline for routine hits, blocks, staggering an enemy, and Guardian
Power. The current hook at `Player.AddAdrenaline()` receives the grant amount
but not its source. Different rewards therefore need an explicit marker at each
chosen action that identifies the source.

## Full-meter charm output

An equipped item can assign a status effect that activates when adrenaline
fills. Valheim empties the meter after activation and refreshes an already active
effect when the meter fills again.

Refreshing an `SE_Stats` effect repeats its up-front health, stamina, eitr, and
adrenaline grants. A refreshable charm effect should use ongoing modifiers
unless repeated up-front grants are an explicit product decision.
