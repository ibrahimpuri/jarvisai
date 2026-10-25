# Hollowmere

A 2D survival-horror metroidvania set in an abbey built inside the ribcage of a dead sea giant, where the tide redraws the map every ten minutes and brings the dead back with it.

This is a playable browser build of the *Hollowmere Game Design Document*. It runs from plain files: no build step, no dependencies, no assets. Every sprite and sound is generated in code.

## Play

Open `index.html` in a desktop browser (Chrome, Edge, Firefox, Safari). It works straight from the file system, or from any static server:

```sh
python3 -m http.server 8000   # then visit http://localhost:8000
```

You need a mouse and keyboard. Headphones help, because the Undertow's bell is panned.

### Windows desktop app

There is also a native Windows build (x64), packaged with Electron so it ships its own Chromium and renders on your GPU:

- `Hollowmere-Setup-1.0.0.exe` installs the game with Start menu and desktop shortcuts.
- `Hollowmere-Portable-1.0.0.exe` runs without installing.

Download them from the repository's **Releases** page, or from the latest **Windows build** run under **Actions**. The files are not code-signed, so SmartScreen may warn the first time: choose **More info → Run anyway**. The game starts fullscreen; F11 or Alt+Enter toggles it, and alt-tabbing pauses. Saves and settings live in `%APPDATA%\Hollowmere`.

To build it yourself on Windows with Node.js 22:

```sh
npm ci
npm run dist:win      # writes release/Hollowmere-Setup-1.0.0.exe and release/Hollowmere-Portable-1.0.0.exe
npm start             # or run the desktop app straight from the source
```

`npm run build:web` is the "compile" step: it concatenates all scripts in load order and minifies them into `dist/app/game.min.js`, which is what the desktop app loads.

| Key | Action |
| --- | --- |
| A / D | Move |
| W / S | Climb ladders, swim, shorten or lengthen the swing rope |
| Space | Jump (S + Space drops through planks) |
| Shift (hold) | Sneak: footsteps carry 2 m instead of 6 m |
| C | Dash, 0.2 s invulnerable. With the Jet Valve: air and underwater bursts in any direction |
| Mouse | Aim |
| Left click / J | Fire the selected weapon |
| Right click / K (hold) | Focused beam: makes knots glint, burns double oil |
| F | Gaff strike: melee, pry plates, kick barrels, ring bells |
| Q (hold) | Gaff Swing from an iron ring, or hook-pull a light enemy |
| E | Interact, talk, drag or release a downed body |
| X | Burn a downed body with lantern oil |
| R | Reload (locks you in place) |
| T | Shutter the lantern |
| Z / G | Poultice / Stitch Kit |
| V | Quick-craft 3 rivets from 1 scrap |
| B | Ballast Boots on or off |
| 1-4 / wheel | Select weapon |
| I | Satchel, crafting, gear, logbook |
| Tab / M | Map |
| Esc / P | Pause |

The game saves when you rest or save at a Bell Room. Continue from the title screen.

## What is in the build

Every core system in the design document is implemented and wired together:

- **The tide clock.** One global 10-minute cycle (Low 3:00, Rising 2:00, High 3:00, Ebbing 2:00), shown on the HUD dial and as painted tide marks on pillars. Rooms sit on a single world grid, so one sea level floods the low rooms and leaves the high ones dry. Ebb gates, tide doors and the Prior's Hymn-of-Hours door open by phase. Ebb currents push swimmers and carry loose items to each region's Wrack Pool. Act III brings spring tides that raise every line.
- **Knot combat.** Every Brine body is a skeleton with knots at its joints. Flesh hits deal 10%. The Crown is sealed until two limb knots break. Breaking knees makes a Shucker crawl, and breaking elbows stops its grab. Every attack has a wind-up during which its knots flare (rivets hit them for double damage). You can break a grab by mashing, or by shooting the grabbing arm.
- **Re-knotting.** A dropped body that the Rising water reaches stands again at High, in every room on the island, including ones you are not in. To stop it, haul the body above the tide line or burn it with oil. Resting re-knots everything you left lying in the water.
- **Enemies.** Shucker, Gullet (drops on noise, shoot the tendril), Bellmouth (inhales at light, its scream wakes the room), Driftling swarms at High tide (drawn to light, a flare stuck to a body lets them strip its knots), Saltwright (pry plates in the 1.5 s after its swing; its pick breaks salt walls), Grafted Choir (Act III, harpoons, heal in water).
- **Bosses.** The Abbot of Salt (his plates fall only when his slams hit the kilns). The Undertow, a stalker under hard rules: late Rising and High only, deep water only, never in Bell Rooms, never within 10 s of a load, bell always audible first, ripples on the map. Breaking its buoy knot drives it off for the tide, and it can only be killed in its Gullet lair. The Shoal, which sheds a body per broken knot. Teodor and Hesper are conditional fights.
- **Sandbox systems.** Noise radii (walk, run, rivet, metal floors ×1.5, Muffled Soles), light and focused-beam occlusion, fire that spreads along timber and rigging and cracks salt walls, and persistence for bodies, bolts, items, doors and puzzle state.
- **Survival.** A slot-limited satchel (8 → 12 → 16; key items take 2 slots), anywhere and bench crafting, poultices and Stitch Kits that lock you in place, breath as helmet bubbles, Bell Rooms (rekindle, save, rest to the next Low, shared Reliquary, bench), and the hidden lifeline rule. On death your satchel stays where you fell, and the Ebb carries it to the Wrack Pool if you died in tidal water.
- **Build choices.** Seven fittings under a Pressure Rating raised by six Brass Seals. Three Brine Grafts, each with its own vulnerability; Ansel can cut one out once at the cost of a health pip. Four weapons with three behaviour-changing tiers paid in scrap and Mere-ivory.
- **Traversal.** Gaff Swing, Ballast Boots (sink through buoyant brine, stomp), Jet Valve, Tide Key, Pressure Seal (the Gullet's pressure won't let an unsealed suit dive) and Saltsight Lens.
- **World.** Seven regions, 25 hand-built rooms, six Bell Rooms. Puzzles: Sluice Ladder barge, Hymn of Hours, Tuned Bells (the Tide Key becomes a tuning fork), Lamp Prisms (bend the focused beam) and Dead Weight (drag knotless bodies onto the lift).
- **RPG layer.** Four NPCs (Ansel, Odile, Hesper, Agathe). Four quests with physical consequences: Lines Gone Slack, The Choir's Water, The Prior's Tables and A Keeper's Log. The Brine breaches a Bell Room in Act III. Three endings: Relight the Lamp, The Saint's Hymn and Keeper's Dark, each gated as the design describes.
- **Fairness and tension.** A pacing valve after fights, readable dormancy (true corpses have no knots), adaptive encrustation (new bodies grow plates over the knot type you break most, always leaving two open, and Ansel remarks on it), tells before damage, and the logbook map that only records water lines for tide phases you have seen.
- **The opening.** The Wrackline and the Tithe Hall set piece play as written: the first Shucker stands when the water reaches it, lifting the crank tolls the bell and floods the hall, you escape by chandelier rings to the gallery, and the Undertow passes behind the glass.
- **Difficulty.** Pilgrim, Keeper and Drowned, plus a separate 100% / 150% / 200% tide-length setting.

## Scope compared with the document

The document plans 8-10 people over 30 months for about 180 rooms and a 10-12 hour game. This build is a complete but compact version of it: all seven regions and the whole critical path, from the reef to all three endings, across 25 rooms, so a first run is far shorter than the document's 10-12 hours. Things that are smaller than the document describes:

- 25 rooms and 6 Bell Rooms instead of about 180 and 14. Bell-cable lifts, which the document lists as cuttable, are omitted.
- Hesper and Teodor are conditional fights rather than six fixed bosses.
- Art and audio are procedural placeholders: stick-skeleton puppets, tile shading and synthesised sound.

## Development tools

```sh
node tools/validate.js      # room sizes, legend, and that every open edge meets an open neighbour
node tools/reach.js         # movement-model reachability of the critical path, gate integrity, and soft-lock (trap) detection
node tools/play.js          # 22 scripted gameplay scenarios with real keyboard and mouse input (Playwright)
node tools/route.js         # the movement model plans routes; an in-page autopilot plays them with real physics
node tools/soak.js 5        # random bot in every room and tide phase, reports runtime errors
node tools/tour.js out/     # screenshots of every room
node tools/desktop-test.js <exe> [out/]   # launches the packaged desktop app, plays, checks fullscreen and saves
```

`F9` toggles invulnerability for testing. `Shift+F9` grants every tool.

## Code map

| File | Contents |
| --- | --- |
| `js/data/rooms.js` | The island: 25 ASCII rooms on a world grid, with their entities |
| `js/data/items.js` | Items, recipes, weapons, fittings, grafts and abilities |
| `js/tide.js` | The tide clock |
| `js/world.js` | Tile queries, phase-dependent gates, water (sea, fixed, pools, floods) and room persistence |
| `js/physics.js` | AABB movement, one-way and moving platforms, line of sight |
| `js/player.js` | Maren: movement, swimming, swing, lantern, weapons, healing, dragging |
| `js/bodies.js` | Knot skeletons and the Brine enemies |
| `js/bosses.js` | The Undertow (roaming system and body), the Abbot of Salt, the Shoal |
| `js/objects.js` | Chests, Bell Room furniture, levers, barge, lift, prisms, Choir objects, collectibles |
| `js/combat.js` | Noise, fire, particles, hitscan, projectiles, the Saw-Line |
| `js/render.js` / `js/hud.js` | Tiles, water, lighting, HUD and the logbook map |
| `js/story.js` | NPC dialogue, quests, Agathe's log and the endings |
| `js/ui.js` | Menus |
| `js/game.js` | Loop, rooms, tide consequences, acts, death, saving |
| `desktop/` | Electron shell: window, GPU flags, fullscreen, menu bridge |
| `.github/workflows/windows-build.yml` | Builds, tests and publishes the Windows .exe files |
