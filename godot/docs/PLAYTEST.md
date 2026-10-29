# Playtesting the whole island

The island is 145 rooms. A person playing it end to end finds the problems a scenario test does not: a door they
cannot get through, an arrow that points at a wall, a room they cannot cross the way the guide sends them. These tools
play the whole game the way a person would and report what goes wrong.

## The sweeps (`godot/src/Playtest.cs`)

Each takes minutes, so they are not part of the default `--test` run; name them:

```sh
$GODOT --headless --path . -- --test pt_doors pt_soak pt_objects pt_bells
```

| Sweep | What it does | What counts as a problem |
| --- | --- | --- |
| `pt_doors` | walks, swims and climbs through every open door on the island with the game's own inputs, hopping on land and stroking up in water when blocked; side doors at Low tide (and at the Ebb where an ebb gate is) | still in the room after four seconds of trying, or in rock on the far side |
| `pt_soak` | leaves the drowned to hunt her for six seconds in every room, late in the game | a body with NaN coordinates, out of its room, or inside a wall |
| `pt_objects` | finds somewhere to stand within reach of every object (beside it, under it, or swimming at it) and uses it | nowhere to stand that shows its prompt |
| `pt_bells` | lights, saves at and reloads in every Bell Room | the bell opens nothing, the save does not set the respawn, or she wakes elsewhere, in rock, or in the air |
| `pt_route` | the autopilot: from each objective to the next, steering only by where the guide's arrow points | no headway for six seconds (mostly the bot's own limits on climbing; see below for the check that does not depend on it) |
| `pt_long` | two minutes in each of five busy rooms under attack, fighting back | entities or particles piling up, or the frame slowing |
| `pt_menus` | the chart in three regions, the satchel's tabs, pause, a Bell Room's menu and the cable page, and the air pointer, for looking at (run rendered with `--out`) | |

## The guide's routes (`tools/guidecheck.js`, `tools/throughs.js`)

The guide finds its way through the rooms by their doors. That is not enough on its own: some rooms only play one way
(a chute nobody climbs back up, a pit she cannot cross from the far side, a shaft she can only drop down), and some
doors are shut until a boss dies.

- `node tools/throughs.js` works out, from Maren's movement (`tools/reach.js`), how every room plays: for each set of
  tools she can have (the Gaff, the Boots, the Jet, the Tide Key, the Seal), each room, and each room she can come in
  from, the rooms she can go on to. It writes `godot/data/through.json`, which the guide reads. Run it again after
  changing a room's map.
- `node tools/guidecheck.js` takes each step of the main mission with the island as it is while that step is the
  objective, walls off every room the guide does not lead through, and checks she can still get there. With `--bells`
  it also starts from every Bell Room of the region, where a player wakes after dying. A failure prints the way that
  does play.

The guide now goes, in order of preference: through doors open now (the tide's own doors count; she can wait for the
tide) and only the ways the rooms play from where she came in; failing that, through doors still shut (the step's own
gate); failing that, any way at all. Rooms can also carry their own rules in the data: `noEntryFrom` (rooms she cannot
come in from) and `noThrough` ([from, to, unless]), where `unless` is a tool or `salt:room` (once that room's salt is
broken).

## What this pass found and fixed

| Found by | Problem | Fix |
| --- | --- | --- |
| `pt_doors` | the Hithe's doors at Low tide: under water a one-tile lip at the room's edge stopped her, and jump did nothing below the surface | under water, pushing into a one-tile ledge along the bottom she rises over it, and jump held swims up as Up does |
| `pt_doors` | the Baptistery's floor onto the Crypt's ladder: standing on a ladder's top under water, Down did nothing | Down on a ladder top climbs on under water, as it does on land |
| `pt_bells` | the Cloister's Bell Room stayed drowned out for the rest of Act III after its breach, and after the Saltworks breach too, though it was never breached | the breach has its own flag; the bell rings again once its two drowned are down, and they wait for her if she leaves before then |
| `guideroutes` | at the Spine, the Organ Loft and the Undertow's lair the arrow pointed through doors the boss still held shut | the guide prefers doors open now |
| `guidecheck` | the way up to the Abbot went up the salt chute; the way back went east to west across the Salt Chapel's altar and the Terraces, and through the Collapse's salt floor; the Choir's Bell Room sent her up the Narthex's shaft | the rooms' own rules, and the table of how every room plays |
| screenshots | the new objective banner sat on a room's title and a region's epigraph | it waits until they have faded |
| screenshots | a room's thought followed her into the next room | it fades when she leaves the room it is about |

## Quality of life

- **Try again from the door.** Killed by a boss in its own hall, the death screen offers to try again from the door she
  came in by, with the boss whole again and her satchel where she fell; or to wake at the bell as before.
- **The bell-cables run under the whole island.** From Act II a lit Bell Room's menu still offers the other in its
  region, and now "Ride the cables to another region...", a page of every lit Bell Room on the island, region by region.
- **Air.** Running low under water (under 45%), the nearest air vent is marked with a ring of bubbles, or an arrow at
  the edge of the view toward it.
- **Unopened boxes on the chart.** Every box she has not opened in a room she has been in is marked, so a room she has
  been through says what it still holds.
