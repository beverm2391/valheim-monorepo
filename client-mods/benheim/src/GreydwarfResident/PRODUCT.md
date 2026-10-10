# Greydwarf Resident

A greydwarf friend lives at the base as a cozy, lore-oriented resident.
Actually lounging in the hot tub is essential to the first feature.

This is part of Ben's taming and cozy/cosmetic direction: extend the feeling
of a base inhabited by creatures. The resident is not a taming progression
system or a worker.

## Current Behavior

Ben has accepted the resident's hot-tub seating, natural idle variation, look
reaction, and overhead message presentation in the disposable Lab prototype. The resident uses the seated
greydwarf from the Bog Witch's hut. These are Lab acceptance judgments, not
proof of an installed or persistent mod feature.

## In Development

Ship the resolved resident behavior through an end-to-end installed-client
check and a bounded test on Ben's shared server. Preserve the accepted appearance,
seating, idle variation, and restrained awareness: notice an approaching
player, acknowledge someone sitting beside him, then settle back into relaxing
without repeatedly greeting a lingering visitor.

The resident reserves his occupied tub seat without offering a sit prompt;
the other seats remain usable. Approach with a clear sightline can trigger
brief native overhead speech. Prove the trigger and display with a fixed line
before connecting an LLM. Seat reservation, prompt suppression, and speech
have developer Lab proof. The speech presentation is accepted; trigger cadence,
seat reservation, and prompt suppression still need Ben's acceptance.

The first placement flow targets an existing tub and offers Invite George and
a matching dismiss action. George may appear directly; a fog/spawn effect is
not required. Use the simplest suitable player control. Establish shared
resident, seating, speech, and placement-lifecycle behavior before calling the
server version ready. The Lab prototype does not prove multiplayer or saved
placement. Keep recovery and removal safe for the native tub and world.

Choose the simplest client/server split that delivers a consistent shared
resident and speech experience without duplicate residents or model chatter.
Players should not manage a separate sandbox bridge. Ben permits an API key
in the private client setup, as with Axiom; key placement is not a reason to
add unnecessary infrastructure. Credentials remain excluded from public
source and debug traces.

Optional OpenRouter reactions are part of the first release, using the newest
Gemini Flash Lite model verified on OpenRouter (currently Gemini 3.5 Flash Lite).
Reuse of the Crow runner is not assumed. Use the
immediate event, character context, and useful queried game state such as
weather to explore occasional speech or silence that makes the resident feel
more alive. Ben has authorized trying ideas freely in the disposable sandbox.
Keep successful experiments recoverable in committed source and promote
accepted behavior into the normal mod incrementally.

LLM playtests need a local debug trace that explains what George was told,
what the model returned, and whether the game displayed it or stayed silent.
Correlate the trigger, prompt/context, response, timing, and final outcome;
make failures and discarded replies distinguishable. Trace content remains
local and excludes credentials.

## Later

Further emote work is parked for the first release. Preserve the reusable
native-player-emote experiments for later behavior composition; individual
gestures and full-body poses require visual acceptance.

George's personality/backstory and knowledge of Ben, Johnny, and Ozi are wanted,
but additional character work is deferred to ship the current behavior first.
Recent conversation and learned memories remain later possibilities.

Drowsiness, especially at night, and activities outside the tub remain ideas
for later behavior.
