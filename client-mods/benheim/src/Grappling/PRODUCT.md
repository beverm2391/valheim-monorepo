# Grappling

Make the native grappling hook a practiced traversal skill. As the skill
levels, grapples reach farther and reload time shortens.

## In Development

The skill is saved with the player. It rewards successful traversal: a small
base XP award once a grapple moves the player a meaningful distance, more XP
for greater displacement from where the hook was fired to where it is
released, and a modest bonus for height gained. Each grapple has an XP cap.

Missing, firing into something beside the player, and hanging motionless do
not earn XP. Height lost does not receive the uphill bonus. Low health does
not increase XP.

Reach increases must work as usable grappling distance, including the rope's
break distance. Exact XP amounts, leveling pace, reach, and reload values are
first-pass tuning to feel in play.

The first pass uses distance and uphill gain. More elaborate skillful-use
scoring is not part of this pass. Gameplay and multiplayer behavior have no
player acceptance yet.
