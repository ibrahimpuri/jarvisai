# The menus

Every menu in Hollowmere is drawn as if it were made from the island: driftwood, wet slate bound in iron and brass, salt-
bleached planks, a Keeper's parchment, cut stone. The sea has grown over all of them. Kelp hangs from their top corners and
sways, bladderwrack climbs their sides, barnacles crowd their corners, salt crusts their lower edge and water gathers there
and drips off. Behind every menu the game is dimmed as if seen from under the water, with shafts of light, motes drifting up
and kelp swaying in from the bottom of the screen.

Nothing is an image file. The art is drawn in code each frame (`src/View/MenuArt.cs`), so it moves and it scales cleanly to
any resolution. The moving parts are small Godot controls (`src/View/MenuWidgets.cs`). The screens themselves are in:

- `src/View/Ui.cs` (the theme, panels, buttons, dialogue, end screens);
- `src/View/UiTitle.cs` (title, crossing, settings, controls, pause);
- `src/View/UiSatchel.cs` (satchel, crafting, gear, journal, map);
- `src/View/UiBell.cs` (Bell Room, reliquary, bench, the Choir's sluice and font).

## What Resident Evil taught the satchel

The satchel and the crafting follow the Resident Evil remakes, which players praise for making inventory a game in its own
right:

- **[Resident Evil 2](https://www.twinfinite.net/guides/resident-evil-2-remake-how-to-combine-items/) (2019).** One screen
  holds the item grid and a condition readout: an ECG whose colour and speed say Fine, Caution or Danger. Each item can be
  used, examined (turned in the hand, with a line of text) or combined. An item box is shared between every safe room.
- **[Resident Evil Village](https://www.primagames.com/tips/resident-evil-village-crafting-how-to-craft-items-guide) (2021).**
  On the crafting screen the thing to make and its cost are at the top, the materials on hand below, and the known recipes
  down the side. Each cost is shown against what you have.
- **[Resident Evil 4](https://www.progameguides.com/resident-evil/resident-evil-4-remake-merchant-upgrades/) (2023).**
  The merchant's upgrade screen shows a weapon's tiers as a ladder with the next one's price.
- **The safe room.** Resident Evil's typewriter rooms are the one place the music turns warm. The Bell Room menu is built
  around its bell, swinging over the choices.

How Hollowmere uses them:

| Resident Evil | Hollowmere |
| --- | --- |
| Condition ECG: Fine / Caution / Danger | The diving suit's heartbeat gauge: **Steady** (green-blue, 62 bpm), **Hurt** (amber, 96 bpm), **Bleeding out** (red, 128 bpm, stumbling), with her health pips, lantern oil, breath and the Hollow's resonance under it |
| Item grid with large items taking two cells | The satchel's pockets, stitched oilcloth with brass corners on the one in hand; key items take a double pocket |
| Use / Examine / Combine / Discard | **Use** (poultice, stitch kit, oil flask), **Examine**, **Craft with it** (opens the recipe that uses the item), **Drop**. Key items cannot be dropped |
| Examine: the item turning, a line of text | The item turns in a pool of lamplight, with Maren's note on it. Every item has one, in `js/data/items.js` (`examine`) |
| Village crafting: result and cost, materials, recipes | **Craft**: the recipes down the left (marked *ready*, *short* or *bench*), the one in hand on the right with what it makes, each cost as *have / need* in green or red, the Craft button, and everything on hand along the foot. The bench uses the same page |
| RE4 upgrade ladder | **Bench, Upgrade**: the weapons down the left; the one in hand with its tiers as a ladder of brass diamonds, what is next, and the price against what she has |
| Shared item box | **The reliquary**: the satchel's pockets on one side and the chest's on the other. Choosing a pocket moves the stack across; *Put one in* / *Take one* move one at a time |

## The screens

- **Title.** The island at night from the water with the boat rowing in. The name is in tarnished brass, as if it had been
  under the sea: dark and green below a tide line, salt on the tops of the letters, barnacles at their feet, wrack up the
  H, kelp hanging from five letters and water running off them. The choices are boards nailed to a driftwood signpost on the
  rocks, kelp wound up the post and a little bell on its arm. A lantern on a chain hangs by the board in hand and swings
  after it when you move. With a save, a line along the foot says where Maren last rang a bell, the act, the time on the
  island and the difficulty.
- **The crossing** (a new game). Three slate cards for the difficulty: a candle for Pilgrim, a storm lantern for Keeper,
  a drowned skull with kelp through its eye for Drowned. The tide length in minutes; *Cast off* on a brass plaque.
- **Settings.** Sound, picture, help on the way (hints, the guide), gore. Reached from the title and the pause menu.
- **Pause** ("The tide waits"). A slate down the left: Resume, Journal, Map, Satchel, Settings, Controls, Controller
  settings, and the two quits, which now ask first. On the right, a scrap of chart with the act, the tide and the next step.
- **Satchel.** Leather bookmarks across the top (Map, Journal, Satchel, Craft, Gear; the bumpers turn them). The satchel
  page is condition, pockets and the item in hand, as above. Gear shows the weapons with their tiers as notches filed into
  a brass strip, the relics, the fittings on a pressure gauge, the grafts and the Hollow's verses.
- **Journal.** An open book of two parchment pages, a ribbon in the left: the next step in full on the left, the rest on
  the right (everything else worth doing, Agathe's pages, the Prior's tables, what she has collected).
- **Bell Room.** The bell swings on its yoke at the top, rings of sound going out from its lip.
- **Bench.** Salt-bleached planks; pages for Craft, Upgrade, Fittings (the pressure gauge, each fitting's effect and
  trade-off) and, in the chapel, Ansel's surgery with his tray.
- **Dialogue.** A slate across the foot of the view, the speaker's name in brass, their likeness in a brass medallion (robe
  or cowl, face lit from the left, the salt-crusted ones with one eye gone white, a hat, the lamp if they carry one), and the
  words typed out at the pace of speech. A press shows the whole line; the next press moves on.
- **Inscriptions.** A slab of stone, the letters cut deep.
- **Death.** Black water. Far below, Maren sinks with her arms up and the lantern going down beside her, her last breath
  rising, dark shapes at the edge of the light turning toward her. Above, rings from a bell. *Wake at the bell.*
- **Endings.** After the last line of an ending, an epitaph on a page of the Keeper's log.
- **The Brine font** has a basin of live salt glittering over its grafts; the Choir sluice is cut stone.

## How it is built

- **The look under a button** is a `ButtonArt`, a child drawn behind the button that reads its state (idle, hover, focus,
  pressed). Styles: a lantern bead and glow for list entries, brass tags for toggles, leather bookmarks for tabs, a riveted
  brass plaque for the main action, a driftwood board for the title, an oilcloth pocket for the satchel.
- **Panels** draw their material behind their content, and a `FrameGrowth` as their last child draws the growth in front,
  reaching past the edges.
- **Tests.** `UiRoot.Instant` is set when the game runs under the test harness, so dialogue appears at once. `--test menus`
  visits every screen and saves a picture of each.
