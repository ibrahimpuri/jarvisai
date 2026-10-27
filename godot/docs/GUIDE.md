# The guide, the journal and the chart

Hollowmere is meant to frighten, not to lose people. The guide (`src/Game/Guide.cs`) always knows the next step of the
main story and shows it three ways. It can be turned off in the pause menu (**Guide: on/off**).

## How it knows

The main story is a chain of steps, each finished by something the game already records: a room visited, a flag, an
item in the satchel, an ability. The current objective is simply the first step not yet done, so a step taken early is
skipped without fuss, and loading a save never confuses it.

| Act | Step | Done when |
| --- | --- | --- |
| I | Get ashore | the Long Pier is reached |
| I | Find the bell that is still ringing | Brother Ansel has been spoken to |
| I | Fetch the Sluice Crank | the crank is in the satchel |
| I | Open the Seawall gate | the crank is in the socket at the foot of the tower |
| I | Climb to the Saltworks | the Evaporation Pans are reached |
| I | Cross the Evaporation Pans | the Kilns are reached |
| I | Face the Abbot of Salt | the Abbot is dead |
| II | Take the Abbot's Ballast Boots | Maren has the boots |
| II | Go down into the Lower Cloister | the Cloister is reached |
| II | Open the Prior's door | Maren has the Jet Valve |
| II | Climb to the Sunken Choir | Maren has the Tide Key |
| II | Rise into the Ribvault | the Ribvault Bell Room is reached |
| II | Take the Pressure Seal | Maren has the Seal (Act III begins) |
| III | Dive the Gullet | the Gullet Mouth is reached |
| III | Kill the Undertow | the Undertow is dead |
| III | Climb into the Lantern Skull | the Skull Stair is reached |
| III | Get past the Shoal | the Shoal is broken |
| III | Reach the lamp. Find your mother. | Agathe has been spoken to |
| III | Decide what a Keeper is for | an ending is chosen |

Some steps have a more pressing line for where Maren stands: in the flooding Tithe Hall the guide says how to get out;
with the pans' sluices jammed it says to wait for the Ebb; with the sea's hold complete it mentions the window.

## How it shows

- **The tracker**, under the tide dial: the objective and where it is ("Tithe Hall, The Wrackline · 2 rooms away").
  A new objective is announced with a banner and a soft bell.
- **The pointer**, in the world: in the objective's own room a gold diamond floats over the thing to find (the crank,
  the socket, Ansel, the Jet Valve, ...). Anywhere else, a pair of chevrons marks the opening that leads on, toward
  the next room on the route; when the way on is a ladder through the floor or ceiling, they sit at the end of the
  ladder she can reach, pointing along it. When the opening is out of view, an arrow at the edge of the screen points
  to it, kept clear of the vitals and the weapon plate.
- **The journal** (the first page after the map in the satchel, Tab or I): the objective in full with how to do it, the
  steps of this act already done, everything else worth doing (the Hollow on the pier, Teodor, the Prior's tables, the
  last night's weapons, the Tine's hold), Agathe's pages and the tables.

The route is found over the island's seams: every pair of rooms whose open tiles touch across their shared edge,
built once from the rooms themselves, so it never disagrees with the chart.

## The chart

The map (`src/View/Chart.cs`) is Maren's chart of the island, drawn on old paper as a Keeper's sea chart:

- every room she has been in drawn as its real shape, the open stone washed in its region's colour and inked round;
- rooms whose door she has seen but not entered as dashed outlines with a question mark;
- the sea as rows of little waves; region names in italic; a compass rose; a legend;
- the Bell Rooms (gold when lit), benches, the people she has met, Wrack Pools, the tide lines she has witnessed, her
  lost satchel, the Undertow's wake, and the Salt Ear graft's pinpricks;
- the objective as a red-ink cross in a breathing ring, named, with the way there dotted in red from where she stands,
  through each door in turn.

## What the playtest fixed

- **Ladders through shut gates.** Climbing the Seawall tower before fitting the crank carried Maren straight through the
  shut gate into the Saltworks (and the Ribvault ladder through the Skull's hatch); coming back down, the gate stopped
  her, and she was stranded above it without the crank. Ladders now stop under anything solid, with a line saying
  where the gate's socket is, and pressing down on a shut gate from its far side lifts its bar and lets it fall open,
  so a save already stranded up there can always get back down.
- **The Tithe Hall's walkway ladder** ended a tile and a half above the highest the flood rises and far above a jump
  from the floor, so after taking the crank the only way out was a chain of gaff swings. It now reaches down to a
  jump from the floor, and the flood carries her to it.
- **Jumping out of water.** Floating at the surface, Maren flickered between swimming and standing, and a jump was lost
  on every other frame. A jump now always leaves the water from the surface.
- **The Evaporation Pans.** The hatch into the pan shut the instant the last lever was thrown, four tiles from it, so the
  pan could never be entered and the Kilns never reached. The hatch now grinds shut over three seconds, drawn as it
  falls: time to run through and rise with the barge, or swim, to the way on at the top.

## Tests

- `--test walkthrough` plays the crank the whole way round with real inputs:
  - the shut tower and the climb back down;
  - the socket's advice;
  - the gallery and its ladder, the altar, the flood and the walkway ladder;
  - the socket, the tower and the Saltworks;
  - an old save stranded above the gate getting down.
- `--test pans` plays the pans with real inputs: the levers, the sprint through the hatch, the rise, the Kilns.
- `--test worldaudit` checks the island's seams: every opening at a room's edge meets an opening beside it, every
  ladder off a room's edge carries on, and nothing is placed inside a wall.
- `--test guide` follows the steps through Act I:
  - the pointer at the pier's ladder foot, the gallery's ladder top and the crank itself;
  - the flood and jam advice;
  - the journal and the chart;
  - skipping steps done early;
  - turning the guide off.
