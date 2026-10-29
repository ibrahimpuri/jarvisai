# The drowned: every kind, in three tiers

The document's six enemy types are joined by four variants and eleven new kinds. Each follows the knot rules: a
body held up by salt knots, which goes down when its crown breaks. Each one changes which knot to break first, or
what to do instead.

The night has three tiers. Each tier's kinds appear where that part of the island begins. Earlier kinds keep turning
up later, so the end of the game mixes all three.

| Tier | Acts and regions | Kinds |
| --- | --- | --- |
| **Start** | Act I: the Wrackline, the Saltworks | Shucker, Gullet, Gullet Pair, Bellmouth, Hooded Bellmouth, **Saltgull**, **Netter**, **Kilnman** |
| **Mid** | Act II: the Lower Cloister, the Sunken Choir, the Ribvault | Saltwright, Driftling, Braced Shucker, **Bloater**, **Toller**, **Cantor**, **Ossuary**, **Kite** (and the Undertow) |
| **End** | Act III: the Gullet, the Lantern Skull | Grafted, **Drowned Diver**, **Deepwalker**, **Wick** (and the Shoal) |

## The new kinds

| Kind | Tier | What it does | Knots | How to put it down |
| --- | --- | --- | --- | --- |
| Saltgull | start | gulls drowned in the Brine; a roost lifts together at a noise, a lamp on it, or someone underneath, then swoops | one, at the wing | any hit brings one down; there are always several |
| Netter | start | a Hithe-wife drowned in her nets; throws a net that holds Maren (a third of her speed, no jumping), then the knife | knees, elbows, crown | mash jump to tear the net, the gaff cuts it, fire burns it; break her right elbow and there are no more nets |
| Kilnman | start | a stoker; fire does not hurt him, and his shovel lights timber, rigging and oil | knees, shoulders, crown | get him into water: doused, every knot breaks twice as fast |
| Bloater | mid | floats off the floor at High tide and drifts to her; its crown is in its belly | elbows, belly crown | breaking the belly bursts a swarm of Driftlings; burn it instead and nothing comes out |
| Toller | mid | a bell-roper with a handbell: when he sees her, every body in the room that can stand comes to the sound | knees, elbows, crown | break his right elbow (the bell arm) first, or stay out of his sight |
| Cantor | mid | a Choir singer whose song stands downed bodies back up, knots and all; keeps its distance | jaw, shoulders, crown | break its jaw, or burn or haul away what you put down |
| Ossuary | mid | a heap of bones that assembles when she comes near; its arms reach twice as far | wrists, knees, crown | hit it while it assembles (its knots break twice as fast); with both wrists broken it cannot hold her |
| Kite | mid | a drowned brother whose cloak catches the wind; glides above her and dives | shoulders, crown | break a shoulder: the cloak folds and it falls, and has to crawl |
| Drowned Diver | end | a salvage diver in brass and canvas; keeps to harpoon range and backs away; does not drown | knees, right elbow, the helmet's latch, crown | break the latch at its neck: the helmet seals the crown until it does |
| Deepwalker | end | something huge from the deep; walks the Gullet floor and feels her in the water | knees, elbows, crown | its knots are dark: only a knot held in the focused beam can be broken |
| Wick | end | hunts light, not her: near one, her lantern's oil drains and flares die | elbows, crown | shutter the lamp and it loses her; the focused beam burns it |

The variants (the Hooded Bellmouth, the Gullet Pair, the Braced Shucker) and the six types from the design document
are described in [WORLD.md](WORLD.md) and the README.

## Later kinds in earlier places

The drowned move as the night goes on. A spawn in the room data can be marked `fromAct` (or `untilAct`). It appears
only from that act, so revisiting a region brings new kinds into it:

- **Act II:**
  - Bloaters in the Wreck Field, the Drowned Hithe and the sunk *Constance*
  - a second Netter at the Hithe
  - Kites over the cliff path and the Trestle
  - a Toller in the Cloister of Salt
  - a Cantor in the Salt Chapel
  - an Ossuary in the Ivory Seam
- **Act III:**
  - Drowned Divers under the sea off the Wrackline
  - a Deepwalker in the Brine Cisterns' tank
  - a Wick at the Mine Head, and one in the Hult Cottage
  - a Kite at the Wreckers' Lookout
  - in the Lower Cloister: a Drowned Diver in the Kitchens, a Wick in the Chantry, a Bloater in the Undercroft
  - in the Sunken Choir: a Drowned Diver in the Choir Crypt, Wicks in the Clerestory and the Pipe Gallery
  - in the Ribvault: a Wick in the Lung, two at the Skull Gate, and a Drowned Diver in the Heart-Root

And the end mixes every tier: the Skull Stair has Wicks, a Toller and a Kilnman, and the Gullet Mouth has a Bloater
beside its Deepwalker and Diver. In the Gullet at full size, Bloaters lie in the Long Throat and the Up-Current,
Deepwalkers walk the Stomach, the Deep and the Whale Fall, Divers hunt the Throat Gate, the Pylorus and the wreck, Gullet
Pairs hang in the Pylorus and the Brine Galleries, and a Toller keeps the Buoy Chapel.
In the Lantern Skull every tier is out: Wicks, an Ossuary and a lying Shucker in the Sinus, Kites, Saltgulls and a
Grafted on the Crown, a Cantor in the Left Eye, a Netter on the Nasal Bridge, a Toller in the Right Eye, a Bellmouth and
a Kilnman in the Ear-Bone, and in the Jaw a Grafted, a Wick, a Netter and a Kilnman.

## The journal's bestiary

The first time Maren sees a kind, it goes into the journal under **The drowned**. Each entry gives its tier, its
knots and how to put it down. Most new kinds also get a thought of her own, which hints at the answer. The text is
in `js/data/bestiary.js`.

## Code

- `src/Game/Bestiary.cs` has the new kinds. They share `Drowned`, one way of stalking her and striking.
- `BodyTypes.START`, `MID` and `END` list the tiers.
- New kinds borrow the voice of an older one (`Body.voice`), pitched by their own throats. The Saltgull has its own cry.
- `Body.Kind` is the bestiary's name, so the variants count as kinds of their own.
- Thrown shots (`Shot.kind`, `Shot.grav`) carry the Netter's net; `Player.netT` is how long the net holds her.

## Tests

`--test bestiary` checks:

- each new kind's rule
- that Bloaters reach the Wreck Field from Act II and not before
- that a Wick waits in the Hult Cottage in Act III
- that the journal lists what she has seen
