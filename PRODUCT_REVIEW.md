# Product Review

This is the live acceptance queue for Benheim behavior under test. The owning
`PRODUCT.md` defines canonical behavior. A behavior is canonical only when that
document lists it under **Current Behavior**.

> **Tomorrow:** Talk through the operator-config papercuts from the `0.1.66`
> integration. The secret check first used the wrong scope instead of Doppler
> `valheim`/`prd`, and server status first selected the ignored `server.env`
> that the password guard rejects. This is only a reminder. Do not start cleanup
> before Ben and the project lead discuss it.

## Current candidate

- Accepted client baseline: `0.1.52`
- Installed private-test client on Ben's Mac: `0.1.72`
- Deployed Server Support: `0.1.6`
- Deployed Test Commands: `0.1.2`
- Required Server Support for this pass: `0.1.6`
- Station Coverage and the sailing gauge are client-only; the server is
  unchanged.

## Test now on installed `0.1.72`

- **Station Coverage:** with isolated level-1 Workbench and Stonecutter controls,
  place a station-required piece at about 22 m and 38 m, then confirm placement
  fails beyond 40 m. Ordinary station interaction, crafting, repair, and
  upgrades must remain native.
- **Sailing gauge:** while steering, confirm the compact speed readout follows
  actual ship motion, `SPRINT` follows the accepted forward Run request, reverse
  never shows `SPRINT`, and the whole readout disappears immediately on helm
  exit.

- **Perfect defense:** one qualifying parry gives one confirmation, adrenaline
  award, and UNTOUCHABLE point; food normalization preserves the streak while
  accepted damage and intentional health costs reset it.
- **Earned combat states:** below 30 health, a perfect parry or dodge activates
  CLUTCH with its title, icon, and charm cue. Six qualifying kills activate
  BERSERKER; twelve within 30 seconds replace it with SLAUGHTERHOUSE. Later
  kills refresh the state, and a 30-second gap expires it. Mix confirmed kills
  and perfect defenses through each UNTOUCHABLE tier; transitions and measured
  payloads must match the owning combat products.
- **Capability warning:** join through Kill Attribution V3 discovery; the
  warning must clear after the matching response.
- **Non-Cooking craft:** craft one ordinary station item; native bonus behavior
  remains and the exclusion outcome is recorded.
- **Diagnostics sharing:** confirm sharing starts on, then turn it off. Remote
  forwarding stops while local logs continue, and a shared event remains
  queryable through the existing provider path.
- **Developer tools:** exercise the ready/not-ready runtime catalogs and
  collider-off cleanup; failures must be visible and catalog data must remain
  local.
- **Henge persistence:** after the accepted on/off commands, reconnect; no henge
  pins may persist.
- **Headshots and shake:** hit the refined head/body boundary and compare the
  headshot, Cleave, and mining AOE shake with ordinary impacts. Accept if the
  boundary is coherent and each shake feels distinct.
- **Grouped receipts:** overlap Put Away, Mass Repair, pocketing, and a native
  top-left message; every result must remain readable and distinct.
- **Remote-owner Put Away:** with two clients, split a partial-capacity deposit
  through a remote-owned chest, then deposit disjoint items whose owner results
  arrive out of order; both clients must converge on the same chest and
  inventory contents.
- **Native boundaries:** exercise DANGEROUS/DEADLY FX gating, the 20 m comfort
  boundary across rooms and floors, empty/nearly-full remote Windmills and
  Shield Generators, and Stone Oven timing. Accept if native gating, range,
  timing, and capacity remain unchanged.
- **Input and repair denials:** press `/` during chat/menu input and exercise
  Mass Repair denial and exhaustion cases. Accept if each invalid action is
  rejected visibly without opening the console or changing native behavior.

- **Menu:** open `Left Shift + B`; confirm the organized, detailed catalog adds
  manual submerged-Tar collection, Ship Sprint, and Perfect Impact, says
  qualifying kills advance UNTOUCHABLE, and names Perfect Impact under Combat
  Shake.
- **Tar:** manually collect submerged small native Tar and loose Tar. Native
  manual interaction remains unchanged; non-Tar stays stuck, and Tar never
  auto-picks up.
- **Ship Sprint:** hold Run at paddle, half sail, and full sail for `3x` native
  thrust; release, helm exit, and reverse return immediately to native behavior.
  With two clients, verify non-owner control and owner handoff apply one boost
  only from the current physics owner.
- **Put Away timing:** deposit one inventory across several chests. It should
  feel meaningfully faster than `0.1.69` with no loss, duplication, or stuck
  batch; inspect existing timing diagnostics only if it still feels slow.
- **Perfect Impact:** repeat a qualifying Lox contact with FX on and off. One
  `PERFECT IMPACT` appears through native world text at the struck character and
  contact point; non-qualifying contacts stay native, and only the optional
  shake follows Combat Shake/FX settings.
- **Mass planting:** preview and place the centered 9x9 grid. Placement remains
  native, and each successful plant costs half the native stamina.

## Needs bounded probe or code later

- **Starred Boars:** add one command-armed, time-bounded session for nearby
  zero-, one-, and two-star Boars. Compare first-alert distance/time, pursuit,
  charge movement/turning, and routine-hit, heavy-hit, Boar-shove, and Perfect
  Impact displacement. Record completion or an explicit incomplete reason for
  gates, slopes, water, or lost paths. Emit one terminal summary per Boar with
  no per-frame events, clear observers on exit, and have Ben judge gameplay feel
  and whether skilled counters remain useful.
- **Leech opportunity:** use a bounded simulation or command-armed window to
  prove the three-times-as-frequent opportunity rate from eligible checks,
  owners, rolls, and outcomes; emit one summary instead of permanent logging.
