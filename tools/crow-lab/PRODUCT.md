# Crow

Crow is a private fourth friend for Ben, Johnny, and Ozi. A native-style crow
appears near the group, watches what happens, and occasionally roasts the
players with the timing and specificity of someone who knows them.

The first product is commentary only. Later gameplay effects may build on a
companion that proves fun, but they are not part of this slice.

## One companion in the game and in chat

The crow can appear and disappear instead of remaining a permanent pet. While
present, it must stay usable around the base, indoors, during portal travel, and
while sailing. The group should not need to protect it, transport it manually,
or recover it after ordinary travel.

One Crow message appears both above the bird and in global chat, like player
speech. The two surfaces carry the same message. Multiple players do not create
multiple competing crows or duplicate messages.

## Events provide the joke setup

Crow is event-driven, not agentic. It does not query logs, answer open-ended
questions, choose objectives, or decide what game state to inspect.

The server selects a small Crow-specific subset from the shared typed gameplay
events. It combines the current event with timestamps, recent global chat,
recent Crow messages, player-name mappings, and private player lore and examples.
An OpenRouter model returns exactly one `{speak, text}` result. A result with
`speak: false` represents silence and is preferred when the event does not
support a specific joke.

Early event candidates include repeated deaths to the same enemy, escape from
critical health, one surviving player after the rest die, meaningful boss or
miniboss kills, raids, sailing, and unusually funny enemy encounters. The model
interprets the supplied context. Benheim should not encode a brittle joke tree.

## Private context stays private

Ben owns the player lore and examples. They remain in ignored local files and
never enter the public repository, packages, logs, test fixtures, or generated
results. Public scenarios use synthetic names and invented events.

The OpenRouter key enters only through the scoped secret workflow. The game
must keep working when the model request fails, times out, or returns invalid
JSON. A failed request produces silence, not a gameplay interruption.

## Two experiments before one mod

The physical companion and the writer are separate proof boundaries:

1. Prove that one crow can appear, linger, follow the group's normal travel,
   speak above its head, mirror that speech into global chat, and disappear
   cleanly.
2. Prove that selected events, recent conversation, and Ben-authored context
   produce funny optional messages at an acceptable cadence and cost.

The local lab owns the second experiment. It assembles the public base prompt,
optional private context, and one timestamped scenario. It validates the exact
response object. Its raw comparator is only a model-selection tool and does not
define Crow's voice or product behavior.

## Status

The local writer's-room lab is implemented. Synthetic scenarios cover repeated
death, critical-health escape, a lone survivor, a meaningful kill, a raid, and
silence. The public runner and strict response validation work with OpenRouter.

The physical crow, server event selection, recent-chat feed, in-game speech,
global-chat mirroring, cooldowns, failure handling, and multiplayer lifecycle
are not implemented. Crow is not part of Benheim's installed client or the
deployed server stack, so it does not belong in `PRODUCT_REVIEW.md`.
