# Greydwarf Resident

A greydwarf friend lives at the base as a cozy, lore-oriented resident.
Actually lounging in the hot tub is essential to the first feature.

This is part of Ben's taming and cozy/cosmetic direction: extend the feeling
of a base inhabited by creatures. The resident is not a taming progression
system or a worker.

## Current Behavior

Ben reports that George works in live Benheim multiplayer after a clean
corrected Windows installation. When Johnny invites George with both players
present, both can see him. Shared speech, the full invitation lifecycle, and
persistence remain unproven.

Ben confirms that live George remarks use his current character's name.

Ben has accepted the resident's hot-tub seating, natural idle variation, look
reaction, and overhead message presentation in the disposable Lab prototype.
The resident uses the seated greydwarf from the Bog Witch's hut. Detailed
acceptance of these behaviors comes from the Lab; the live report does not
establish all of them in multiplayer.

## In Development

Complete the remaining player checks on Ben's shared server. Preserve the
accepted appearance, seating, idle variation, and restrained awareness: notice an approaching
player, acknowledge someone sitting beside him, then settle back into relaxing
without repeatedly greeting a lingering visitor.

A player joining or returning after death must see an already-invited tub's
resident without needing a dismiss/reinvite. Ben reported that Johnny could
not see the resident until they repeated the invitation, and that George
disappeared from Ben's view around respawn. Recovery after joining and respawn
remains unresolved in live play.

The resident reserves his occupied tub seat without offering a sit prompt;
the other seats remain usable. Approach with a clear sightline can trigger
brief native overhead speech. Prove the trigger and display with a fixed line
before connecting an LLM. Seat reservation, prompt suppression, and speech
have developer Lab proof. The speech presentation is accepted; trigger cadence,
seat reservation, and prompt suppression still need Ben's acceptance.

Holding Shift on an existing native tub interaction or seat changes its hover
action to Invite George or Dismiss George; Use performs the shown action.
Follow Benheim's existing Shift interaction convention, with menu discovery.
Ordinary interaction remains native, except the resident's reserved seat has
no Sit prompt. He appears directly; a fog/spawn
effect is not required. An invited tub has one resident. Its native saved
state remembers the invitation across rejoin/restart; dismiss clears it, and
destroying the tub removes the resident. Removing Benheim leaves an ordinary
native tub, with no custom missing-prefab object.

The shared feature requires compatible Benheim clients and updated Benheim
Server Support. Server Support coordinates placement through the tub's native
owner and grants one client a speech encounter. That client calls OpenRouter;
the server validates and shares one result, and clients render the resident
and speech. Players do not manage a separate speech bridge. Missing or
incompatible support must be visible rather than producing inconsistent local
residents. These shared, persistence, and removal behaviors still require
end-to-end proof.

Multiplayer Benheim is the release target. Local single-player support is
only a testing convenience when it comes cheaply; it must not delay shipping
or require a separate local feature path.

Prefer a dedicated Benheim OpenRouter key; Ben authorizes the existing key
for the first private test build, so creating a separate key is not a blocker.
Ben permits the key in the private client setup, as with Axiom. Credentials remain excluded from
public source and debug traces. Model failures leave the resident usable
and produce silence.

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

George can address a visitor naturally without repeating their name in every
remark. Authored relationships remain later choices.

A private preferred-name map uses stable player account identity so George
can recognize the same person across character changes. Unmapped players use
their current character name. Start with Ben's mapping; other players can be
added when their identities and preferred names are known.

Add a native interact prompt on George to request more talking. Improve
dialogue quality and the feeling of responsiveness, including useful loading
feedback. Explore preparing messages ahead of interaction in the local
sandbox; that is an option to compare, not a chosen architecture.
Pair with Ben on what better dialogue means before choosing its voice or
content.

Improve George's awareness of what is happening in game and give him more
activities and states. Nighttime drowsiness, sitting in a throne, and walking
between the throne and tub are wanted directions for the next improvement
pass. Explore these in the disposable sandbox; they have no player acceptance
yet.

## Later

Further emote work is parked for the first release. Preserve the reusable
native-player-emote experiments for later behavior composition; individual
gestures and full-body poses require visual acceptance.

George's personality/backstory and knowledge of Ben, Johnny, and Ozi are wanted,
but additional character work is deferred to ship the current behavior first.
Recent conversation and learned memories remain later possibilities.
