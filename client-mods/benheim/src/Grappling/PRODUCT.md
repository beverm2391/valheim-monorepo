# Grappling

Make the native grappling hook a practiced traversal skill. As the skill
levels, grapples reach farther and reload time shortens.
The skill uses the native grappling-hook item's icon in the Skills menu.

## In Development

The skill is saved with the player. It rewards successful traversal: a small
base XP award once a grapple moves the player a meaningful distance, more XP
for greater displacement from where the hook was fired to where it is
released, and a modest bonus for height gained. Each grapple has an XP cap.

Missing, firing into something beside the player, and hanging motionless do
not earn XP. Height lost does not receive the uphill bonus. Low health does
not increase XP.

Reach increases must work as usable grappling distance, including the rope's
break distance. The starting tuning scales linearly from 40 to 100 meters of
usable reach and from 2 to 0.2 seconds of reload time across skill levels 0
to 100:

| Skill level | Reach | Reload time |
| --- | ---: | ---: |
| 0 | 40 m | 2.00 s |
| 25 | 55 m | 1.55 s |
| 50 | 70 m | 1.10 s |
| 75 | 85 m | 0.65 s |
| 100 | 100 m | 0.20 s |

These values, exact XP amounts, and leveling pace are first-pass tuning to
feel in play.

The first pass uses distance and uphill gain. More elaborate skillful-use
scoring is not part of this pass. Gameplay and multiplayer behavior have no
player acceptance yet.
