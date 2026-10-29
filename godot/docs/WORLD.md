# The full island: world plan

The prototype was the design document's critical path in 26 rooms. This is the plan for the full game the document
describes: seven regions, about 180 rooms, 14 Bell Rooms, 6 bosses, 4 enemy variants, bell-cable lifts, and a
10–12 hour first run. It is built one region at a time, and every phase ends with a playable build.

## The grid

Rooms sit on one world grid of screens (40 × 23 tiles each). The full island is 46 columns × 15 rows of screens.

- **The sea row is row 9.** The tide's water line is one world-space line that moves between Low and High inside
  row 9. Rooms on row 9 are tidal (they become three versions of themselves).
- **Rows 10 and below** are under the sea: always flooded. Plain underwater rooms are swum on suit air, and the
  Ballast Boots walk their floors. Rooms marked `deep` (the Gullet) need the Pressure Seal.
- **Rows 8 and above** are dry, apart from rooms with their own water: the pans, the Sunken Choir's held water,
  and sluice basins worked with the Tide Key.

The existing 26 rooms keep their maps and their links to each other within each cluster. A cluster moves only when
the region that fills the gap behind it is built. Phase 2 moved everything west of the Cloister (the Prior's Walk, the
Choir, the Ribvault, the Gullet and the Skull) 18 screens west as one block; the Choir, the Ribvault, the Gullet and
the Skull reach their full-island cells in later phases:

| Cluster | Rooms | Prototype position | Full island |
| --- | --- | --- | --- |
| Wrackline | reef, breakers, pier, seawall, chapel, tithe, tithe_gallery | rows 3–4, cols 5–9 | rows 8–9, cols 35–39 |
| Saltworks | saltgate, saltbell, kilns, brineshaft | rows 2–3, cols 4–7 | rows 7–8, cols 34–37 |
| The Abbot | abbot | row 1, col 7 | row 2, col 40 (top of the mine) |
| Cloister (east) | cloister, nave, cloisterbell | rows 3–4, cols 3–4 | rows 8–9, cols 33–34 |
| Cloister (west) | priors (with the lift), choirsteps, gulletmouth | rows 3–5, col 2 | rows 8–10, col 14 |
| Sunken Choir | choir, choirbell | row 3, cols 0–1 | row 6, cols 3–4 |
| Ribvault | ribshaft, ribbell, prisms | rows 1–2, cols 1–3 | rows 3–5, cols 6–8 |
| Gullet | lair | row 5, cols 3–4 | row 13, cols 20–21 |
| Lantern Skull | skullstair, lamp | row 0, cols 2–3 | row 0, cols 22–23 |

```
      0         1         2         3         4
      0123456789012345678901234567890123456789012345
 r0   ....................SSSSSSSSS.................   S  Lantern Skull
 r1   ..........RRRRRRRRRRSSSSSSS...................   R  Ribvault
 r2   ......RRRRRRRRRRRRRRRR............aaAaaa......   a/A Saltworks mine, A = the Abbot
 r3   ......RRRRRRRRRRRR...............aaaaaaaa.....
 r4   ...CCCRRRRRR.....................aaaaaaaaa....
 r5   ..CCCCCRRR........................aaaaaaaaa...
 r6   .CCCCCCCC.......................aaaaaaaaaa....   C  Sunken Choir
 r7   CCCCCCCCCC......................aaaaaaaaaa....
 r8   .CCCCCCCCCLLLLLLLLLLLLLLLLLLLLLLLLWWWWWWWW....   L  Lower Cloister
 r9   ..........LLLLLLLLLLLLLLLLLLLLLLLLWWWWWWWWWW..   W  Wrackline (row 9 is the sea row)
 r10  ..........GGGGGGGGGGGGGGGGGGGGGLL..wwwwwww....   w  Wrackline under the sea (Boots, Seal)
 r11  ..........GGGGGGGGGGGGGGGGGGGGG..............   G  The Gullet (always flooded, deep)
 r12  ...........GGGGGGGGGGGGGGGGGGG...............
 r13  ...............GGGGGGGGGGG...................
```

## The critical path and its gates

The order the document gives, with the gate that opens each region:

| Act | Region | Way in | Key unlock | Boss |
| --- | --- | --- | --- | --- |
| I | The Wrackline | the start | Gaff Swing basics, the Sluice Crank | none (the Tithe Hall set piece) |
| I | Saltworks | the Sluice Crank opens the seawall gate | Ballast Boots | **The Abbot of Salt** |
| II | Lower Cloister | the Boots sink the Brine Shaft (or the tithe crypt at Low tide) | Jet Valve | **The Almoner** |
| II | Sunken Choir | the Jet Valve | Tide Key | **The Precentor** (Hesper, if the Choir is drained) |
| II | Ribvault | the Tide Key | Pressure Seal | **The Hanging Mother** |
| III | The Gullet | the Pressure Seal | the Saint's Ear; the Undertow dies | **The Undertow** |
| III | Lantern Skull | opens after the Gullet | the endings | **The Shoal** |

Every ability opens old areas in a new way:

| Ability | Opens, back in older regions |
| --- | --- |
| Ballast Boots | the seabed under the Wrackline (the sunk *Constance*, the Breakers' floor), the pans' sumps |
| Jet Valve | the clifftop ledges of the Wrackline, the Saltworks' high galleries |
| Tide Key | sluice rooms in the Cloister and Saltworks that drain to give up their floors |
| Pressure Seal | the deep sea caves under the Wrackline, deep cisterns under the Saltworks |
| Saltsight Lens | knots in the dark everywhere, and passages behind cracked salt that the light shows |

## Bosses

Six fixed bosses, all built from the same knot rules at a bigger scale, plus the two that depend on choices:

1. **The Abbot of Salt** (Saltworks). A cathedral-sized Saltwright whose plates fall only when he strikes his own
   kilns. Moves from the prototype's kiln level to the top of the salt mine.
2. **The Almoner** (Lower Cloister, the Refectory). The brother who chose which one in ten went to the Saint, and made
   sure it was never him. A bloated Shucker at boss scale, chained by the neck to the Refectory's long table; the
   doors west are chained to him. Two broken limbs open the crown in his chest. Knees first, and he can only crawl,
   but when the tide is in and the hall is flooded he swims at her. Elbows first, and he cannot grab or throw, but he
   heaves the table, which throws anyone on it or on the floor beside it. Out of his chain's reach (the sideboards by
   the walls, the galleries, the reader's pulpit) he throws pewter alms-bowls.
3. **The Precentor** (Sunken Choir, the organ loft). A Bellmouth fused into the organ's pipes. It inhales to scream,
   and the scream wakes the choir. Break its jaw during the inhale, or tune the bells (the Tide Key's water) to the
   pitch that shatters the salt in its throat.
4. **The Hanging Mother** (Ribvault, the spine). A Gullet the size of a chapel hanging from the vertebrae on many
   tendrils, in a room the wind blows through. Shoot the tendrils to drop her onto the rib struts, where her spine
   knots can be reached. Every cut tendril lets a young Gullet fall.
5. **The Undertow** (the Gullet, its lair). The island's stalker, killable only here.
6. **The Shoal** (Lantern Skull, Act III). Five bodies fused into one rolling mass with eight knots.

Conditional fights stay as they are: **Teodor** (Act III, if he was captured) and **Hesper** (if the Choir was drained).

## Enemy variants

The six enemy types, plus four variants that each change the knot puzzle:

| Variant | Of | What changes |
| --- | --- | --- |
| **Braced Shucker** | Shucker | plates over the knot type she breaks most (adaptive encrustation, already in the game) |
| **Bloater** | Shucker | floats up at High tide; its crown is in its belly, and breaking it bursts a Driftling swarm. Burn it instead |
| **Hooded Bellmouth** | Bellmouth | blindfolded with a hymnal page: it ignores the lantern and screams at noise, so shuttering does not help |
| **Gullet Pair** | Gullet | two tendrils: cut one and it swings, and only cutting both drops it |

Beyond the document's six types and four variants, eleven new kinds fill out three tiers of the night: start
(Saltgull, Netter, Kilnman), mid (Bloater, Toller, Cantor, Ossuary, Kite) and end (Drowned Diver, Deepwalker, Wick).
Earlier tiers keep appearing later, and spawns marked `fromAct` bring later kinds into earlier regions on revisits.
See [ENEMIES.md](ENEMIES.md).

## Bell Rooms: 14, two per region

| Region | Bell Rooms |
| --- | --- |
| Wrackline | Seawall Chapel; the Ferry House |
| Saltworks | Saltworks Bell Room; Mine Head |
| Lower Cloister | Cloister Bell Room; the Almonry |
| Sunken Choir | Choir Bell Room; the Cantor's Vestry |
| Ribvault | Ribvault Bell Room; the Sternum Bell |
| The Gullet | the Drowned Bell; the Last Air |
| Lantern Skull | the Keeper's Watch; the Lamp Room (Agathe's) |

**Bell-cable lifts** (from Act II): each lit Bell Room can ride its cable to the other lit Bell Room in its region.

## Collectibles across the island

The counts stay as the document gives them, spread so that thorough exploration finds them:

- **Keeper's Stitches:** 4 (health 5 → 9).
- **Brass Seals:** 6 (Pressure Rating 4 → 10).
- **Agathe's pages:** 9.
- **The Prior's tide tables:** 4.
- **Mere-ivory:** from every boss and from hidden vaults, enough for every weapon's tier 3 with a few to spare.
- **Weapons:** the Rivet Driver at the start, the Harpoon Gun (Saltworks), the Flare Sling (Lower Cloister) and the
  **Saw-Line**, which the prototype never placed (the Saltworks mine).
- **Satchel:** 8 → 12 → 16 through two quest rewards (Odile's, and Hesper's or the Choir's cache).

## Progress

| Phase | Regions | Rooms | State |
| --- | --- | --- | --- |
| 0 | the engine for a large island | | built: the sea row, the chart, save migration, bell-cable lifts, region files |
| 1 | the Wrackline and the Saltworks | 61 in all (35 new) | **built** |
| 2 | Lower Cloister | 86 in all (25 new) | **built** |
| 3 | Sunken Choir | | next |
| 4 | Ribvault | | |
| 5 | The Gullet | | |
| 6 | Lantern Skull | | |

### What the engine does for a large island

- **The sea row** is `Tide.SEA_ROW`; a room is tidal when it spans that row (`RoomDef.Tidal`). The Undertow's route is
  every room marked `sea` in the room data.
- **Region files.** Each region's new rooms live in their own file (`js/data/rooms_<region>.js`), with their carvings;
  `tools/data-files.js` and `index.html` list them in load order.
- **The chart** frames the rooms she has found, so it grows with her. The mouse wheel, the triggers or + and − zoom in
  around her, and the right stick or the arrow keys look about.
- **Saves move with the island.** A save records where every room sat. When rooms move in a later phase, everything
  the save remembers (her respawn, her lost satchel, bodies and loose items) moves with its room. A save from the
  26-room prototype has no record and is moved by the known offset.
- **Bell-cable lifts.** From Act II, each lit Bell Room's menu offers a ride to every other lit Bell Room of its region.
- **The Undertow's way** runs along every room marked `sea` that spans the sea row, room to room however wide each
  room is. It never surfaces in a Bell Room or where a boss still stands.
- **A room's own thought.** A thought in the lore named for a room is Maren's the first time she comes in.
- **Gates by flag.** Gate tiles open on a flag: `K` the seawall gate, `Z` the Skull's hatch, `Q` the Refectory's
  chained doors (`almonerDead`).

### Phase 1 tests

- `node tools/validate.js`: every room's edges meet.
- `node tools/reach.js`: the critical path, every new chest and Bell Room, and the gates:
  - the salt sump's box needs the Boots
  - the Brine Seep's box needs the Boots
  - the cistern's box needs the Seal
  - no reachable cell is a trap
  - `--dump <stage> <room>` prints one room's reachable cells
- `--test bigworld`: every new room can be entered. It also covers the bell-cable in Acts I and II, an old save landing
  on the new grid with its bodies, the Gullet Pair and the Hooded Bellmouth.

### Phase 2 tests

- `node tools/reach.js`: the Cloister's rooms and chests, and its gates:
  - nothing west of the Refectory (the Almonry, the Jet Valve) until the Almoner is dead
  - the Light-Well, the Bell-Cote and the Treasury's strongbox need the Jet Valve
  - the candle-hoop box in the Chapter House is reached by swimming up at High tide
- `--test cloister`: every new room can be entered, the guide's new steps, and the Almoner:
  - he wakes as she comes down the hall, the chain holds him, and out of his reach he throws
  - knees broken, he crawls, and at High water he swims at her
  - elbows broken, he heaves the table
  - his death opens the doors west, and he stays dead
  - the Almonry's bell-cable, the Undertow's way along the lower floor
  - saves from before the move land on the moved Prior's Walk

## Phase 1: the Wrackline and the Saltworks

### The Wrackline (18 rooms)

The opening stays as it is: about 25 minutes from the reef to the seawall gate. The new rooms are east along the
coast and under the sea, explored when Maren first passes and again with each new tool.

| Room | Cell | Role |
| --- | --- | --- |
| The Reef, the Breakers, the Long Pier, the Seawall, the Seawall Chapel, the Tithe Hall, the Tithe Gallery | rows 8–9 | the opening (unchanged) |
| **Hult Cottage** | 38,8 | east of the chapel on the clifftop: the house Maren grew up in. Empty, and the shelf where the music box stood |
| **Wreckers' Lookout** | 39,8 | the old false-light post on the cliff; down to the Wreck Field |
| **The Ferry House** | 41,8 | the Wrackline's second Bell Room, above the ferry |
| **Cliff Path** | 40,8 | between the lookout and the ferry house, along the cliff |
| **The Wreck Field** | 40,9 | east of the Breakers: hulls on the rocks, walked at Low tide, swum at High |
| **Rope Ferry Landing** | 41,9 | the ferry winch to the mainland (the Keeper's Dark ending leaves from here) |
| **Drowned Hithe** | 42,9 | a fishing hamlet on stilts, half under the sea |
| **The Tide Pools** | 43,9 | rock pools and a seal in a sea cave |
| **The Sea Arch** | 44,9 | the island's east end |
| **The Constance, Sunk** | 37,10 | the brig's flooded hold below the reef (Ballast Boots) |
| **The Breakers' Floor** | 38–39,10 | the seabed under the Breakers (Ballast Boots) |
| **Smugglers' Cut** | 40,10 | a sea cave under the wrecks into deep water (Pressure Seal) |

### The Saltworks (26 rooms)

The prototype went pans, kilns, Abbot. Now the kilns lead up into the salt mine, and the Abbot waits at the top.

| Room | Cell | Role |
| --- | --- | --- |
| Saltworks Bell Room, Evaporation Pans, the Kilns, Brine Shaft | rows 7–8 | as before |
| **Drying Sheds** | 38,7 | east of the kilns: racks of salt and hanging sacks |
| **Cart Yard** | 39,7 | the mine carts' yard |
| **The Trestle** | 40,7 | the cart line on timber over the cliff, in the wind |
| **Salt House** | 41,7 | a warehouse of sacks in the dark |
| **Upper Kilns** | 37,6 | above the kilns, where the Abbot was |
| **Furnace Stair** | 38,6 | up through the flues |
| **Mine Head** | 39,6 | the pithead and its winding gear |
| **Mine Head Bell Room** | 40,6 | the Saltworks' second Bell Room |
| **Brine Cisterns** | 35,6 | above the Bell Room: tanks of brine (Pressure Seal later) |
| **Pan Terraces** | 36,6 | above the pans |
| **The Adit** | 39,5 | the mine's mouth |
| **Pick Gallery** | 40,5 | Saltwright workings; the Saw-Line |
| **Brine Seep** | 41,5 | salt water seeping through the Saint's flank |
| **Crystal Drift** | 42,5 | living salt, crystal walls |
| **The Winze** | 43,4–5 | a two-storey shaft |
| **Collapsed Drift** | 40,4 | a caved-in drift |
| **Pillar Hall** | 41,4 | salt left standing as pillars |
| **The Ivory Seam** | 42,4 | the Saint's own bone, exposed: a Mere-ivory vault |
| **Salt Chapel** | 38,3 | the miners' chapel, carved from the salt |
| **Cloister of Salt** | 39,3 | the Abbot's brothers, standing in rows |
| **Abbot's Stair** | 40,3 | up to the cathedral |
| **Cathedral of Salt** | 40,2 | the Abbot |
| **The Salt Chute** | 36,3–5 | after the Abbot: a long drop back to the Pan Terraces |

## Phase 2: the Lower Cloister (25 rooms)

The flooded monastery between the Bell-Rope Nave and the Prior's Walk, 18 screens long. It has two floors. The upper
floor (row 8) is dry. The lower floor is on the sea row: the tide fills it to the waist, and at High water the Undertow
walks it. Both floors meet at the Refectory, where the Almoner keeps the way west.

The way through, from the Nave: west into the Lavatorium; then along the lower floor or up the Night Stair to the upper
floor; then the Almoner; then the Almonry (the second Bell Room) and on west to the Prior's Walk and its Jet Valve. With
the Jet Valve, the Cloister opens again: a hatch in the Library to the Light-Well, the Chantry's bell-cote, and the
Treasury's strongbox.

| Room | Cell | Role |
| --- | --- | --- |
| Cloister Walk, Bell-Rope Nave, Cloister Bell Room | 33–34, 8–9 | as before |
| **The Lavatorium** | 32,9 | the washing trough, always full; a Bloater |
| **The Chapter House** | 30–31,9 | tiers of seated brothers and a Toller; a box in the candle-hoop, reached by swimming up at High tide |
| **The Slype** | 29,9 | a low passage that floods at High tide; a Gullet Pair in its light-well |
| **The Warming Room** | 28,9 | the one hearth, and a Kilnman by it |
| **The Undercroft** | 26–27,9 | cellars, a drain channel, an Ossuary |
| **The Refectory** | 24–25, 8–9 | the Almoner; its west doors are chained to him |
| **The Kitchens** | 22–23,9 | ovens, a well, a Netter; a Drowned Diver in Act III |
| **The Infirmary** | 21,9 | a Cantor singing to the beds |
| **The Charnel House** | 19–20,9 | two Ossuaries and a bone pit |
| **The Tithe Cells** | 17–18,9 | a gallery of cells; the end cells open only at Low tide |
| **The Prior's Garth** | 15–16,9 | the Prior's garden and fishpond, east of the Prior's Walk |
| **The Night Stair** | 32,8 | up from the Lavatorium; its east door is the Bell Room's, opened when the Choir is drained |
| **The Dorter** | 30–31,8 | rows of cots, a Hooded Bellmouth |
| **The Scriptorium** | 29,8 | desks, and a Bellmouth on the reader's desk |
| **The Library** | 28,8 | the stacks; a hatch in the vault to the Light-Well (Jet Valve) |
| **The Counting House** | 26–27,8 | the Wheel of Lots, a Cantor and a Hooded Bellmouth |
| **The Almonry** | 23,8 | the Cloister's second Bell Room |
| **The Dole Hall** | 22,8 | where the Hithe was given bread; a Netter |
| **The Chantry** | 21,8 | a Toller at the altar; a Wick in Act III; the bell-cote above (Jet Valve) |
| **The Sacristy** | 20,8 | vestment presses and a Saltwright |
| **The Treasury** | 19,8 | its strongbox ledge needs the Jet Valve |
| **The Pulpitum** | 17–18,8 | the stone screen before the Choir, with a box on its rood loft |
| **The Prior's Lodging** | 15–16,8 | his study and the hymnal's hint for his door; on west to the Choir Steps |
| **The Light-Well** | 28–29,7 | the Cloister's only sky: wind, Kites and Saltgulls (Jet Valve) |
| **The Bell-Cote** | 21,7 | the chantry's bell (Jet Valve) |

Later phases are planned in the same way when they are built: their region's rooms, the gap their clusters move
across, their second Bell Room and their boss.
