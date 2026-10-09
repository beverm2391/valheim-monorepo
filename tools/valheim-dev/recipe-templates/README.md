# Recipe Templates

These files seed new local recipes. Live recipe edits belong under
`VALHEIM_DEV_ROOT/registry`; they are runtime data, not source changes.

The `greydwarf-resident*` templates preserve the disposable resident prototype.
Run `greydwarf-resident`, `greydwarf-resident-seat`,
`greydwarf-resident-speech`, then `greydwarf-resident-variation`. The spawn
recipe places a fueled native tub relative to the local player. The other
recipes require that resident. Remove them in reverse order before removing
or replacing the spawn recipe; spawn cleanup destroys its tub and resident.
These templates do not establish persistence or multiplayer behavior.

After the baseline, install `greydwarf-resident-emotes` and use the
`greydwarf-resident-emote` command recipe. Its presets cover all 25 native
player emotes; the final preset stops the current gesture. Ad hoc inputs accept
`emote`, `mode` (`upper` or `full`), `seconds` (up to 60), `speed` (.1–3), and
optional `speech`. Empty inputs return the catalog and current state.

`upper` transfers arm and head motion over George's seated native loop. `full`
previews the whole pose at the same root transform; standing, kneeling, and
lying poses can intersect the tub. Native commands are available, but that
does not mean every gesture reads cleanly on George: his rig lacks thumb bones
and some finger joints. Remove the emote palette before the baseline recipes.
