# Greydwarf Resident

A greydwarf friend lives at the base as a cozy, lore-oriented resident.
Actually lounging in the hot tub is essential to the first feature.

This is part of Ben's taming and cozy/cosmetic direction: extend the feeling
of a base inhabited by creatures. The resident is not a taming progression
system or a worker.

## Current Behavior

Ben confirms that live George remarks use his current character's name.
Ben reports that Talk worked in the Lab and wants it shipped. This accepts
the Lab interaction; shared-server Talk remains unproven.

Ben has accepted the resident's hot-tub seating, natural idle variation, look
reaction, and overhead message presentation in the disposable Lab prototype.
The resident uses the seated greydwarf from the Bog Witch's hut. Detailed
acceptance of these behaviors comes from the Lab; the live report does not
establish all of them in multiplayer.

## In Development

Live multiplayer visibility remains unresolved. Ben reports that he could not
see George at all during his latest play session, while the tub's Shift
interaction offered Dismiss George. In an earlier session after a corrected
Windows installation, Johnny invited George with both players
present and both could see him. That earlier success does not establish
reliable visibility, the full invitation lifecycle, or persistence.

Complete the remaining player checks on Ben's shared server. Preserve the
accepted appearance, seating, idle variation, and restrained awareness: notice an approaching
player, acknowledge someone sitting beside him, then settle back into relaxing
without repeatedly greeting a lingering visitor.

A player joining or returning after death must see an already-invited tub's
resident without needing a dismiss/reinvite. Ben reported that Johnny could
not see the resident until they repeated the invitation, and that George
disappeared from Ben's view around respawn. Recovery after joining and respawn
remains unresolved in live play.

Ben reports that George disappears for Johnny when Ben joins, with a large
"waiting for compatible players" warning. Developer logs show matching
clients temporarily lose readiness while another player loads, removing the
resident until that player's capability handshake completes. Correct this
ordinary-join behavior without conflating pending support with confirmed
incompatibility. Shared speech has developer evidence of one result displayed
on both live clients; player acceptance remains open.

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

The server retains a private speech trace of what George was told, what the
model returned, and whether clients displayed it or stayed silent. Correlate
the trigger, full sanitized prompt/context, response, timing, and final
outcome; distinguish failures and discarded replies. Client-local traces
remain useful, but are not the only transcript. Credentials are excluded.

George has one conversation history shared across players. Changing the
visitor or the client making the model request must not give him a separate
memory. Use the conversation context rather than only the last three remarks;
compact older history when needed while retaining original turns in the
server trace. This is conversation continuity, not a new long-term memory
system.

George can address a visitor naturally without repeating their name in every
remark. Authored relationships remain later choices.

A private player-name and lore map uses stable account identity so George can
recognize the same person across character changes. Preferred names, known
gender/pronouns, and player lore belong to those profiles, authored with Ben,
rather than George's personality seed. Ben's current group is all men.
Unmapped players use their current character name. Start with Ben's mapping;
other players can be added when their identities and preferred names are known.
Ben permits native account IDs and current character names in private Axiom
diagnostics. Account IDs remain excluded from public source and provider
context. Developer evidence confirms receipt of the native account/name
records in Axiom.

George offers a native Use prompt to Talk, with Thinking feedback while a
reply is pending. Ship Talk with the agreed personality and
account/current-character logging, including the compatible server update
needed for the shared interaction. Keep other unstable or untested experiments
out of this release. Retain the existing 140-character reply limit. Let the model vary reply
length naturally within that limit. Longer replies up to 400 characters,
with enough display time to read them, remain a Lab experiment rather than
part of this release. Explore preparing messages ahead of interaction in the
local sandbox; that is an option to compare, not a chosen architecture.

Ben's character direction is method acting from a compact seed, without
overprompting scripted behavior.
George has the demeanor of an old white guy
sitting naked in a hot tub who has long since stopped caring what anyone
thinks. He is casually racist, sexist, and prone to unsolicited, inappropriate
comments; these come out as ordinary conversation. His age, nudity, setting,
and lack of embarrassment give him that particular flavor. He is his own
character, with Pierce from Community as a reference rather than a complete
personality to copy. He is omniscient as an in-game trope and inexplicably
knows the players' business.

George can also message global chat while players are away from him. The mod
owns when he speaks and whether delivery is a nearby bubble or server-wide
chat; the personality prompt does not own routing. Pass the audience and
delivery context to the model. Global-chat behavior remains unproven.

Improve George's awareness of what is happening in game and give him more
activities and states. Nighttime drowsiness, sitting in a throne, and walking
between the throne and tub are wanted directions for the next improvement
pass. Explore these in the disposable sandbox; they have no player acceptance
yet.

## Later

Further emote work is parked for the first release. Preserve the reusable
native-player-emote experiments for later behavior composition; individual
gestures and full-body poses require visual acceptance.

Learned memories beyond the shared conversation remain later possibilities.
