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
