# Hollowmere

A 2D survival-horror metroidvania set in an abbey built inside the ribcage of a dead sea giant, where the tide redraws the map every ten minutes and brings the dead back with it.

This is a playable browser build of the *Hollowmere Game Design Document*. It runs from plain files: no build step, no dependencies, no assets. Every sprite and sound is generated in code.

## Play

Open `index.html` in a desktop browser (Chrome, Edge, Firefox, Safari). It works straight from the file system, or from any static server:

```sh
python3 -m http.server 8000   # then visit http://localhost:8000
```

Play with keyboard and mouse, or with an Xbox, PlayStation or Switch controller (see [Controllers](#controllers)). Headphones help, because the Undertow's bell is panned.

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

## Godot / C# edition

`godot/` is a rebuild of the same game in Godot 4.7 with C# (.NET 8). The game systems are a faithful port of the JavaScript code; Godot does the presentation:

- Real 2D lights with shadows cast by the masonry (occluders are generated from each room's tiles), including a cone light for the lantern that walls block.
- Per-region ambient darkness, bloom on flames, knots and embers, and light shafts through stained glass.
- A water shader with depth tint and drifting caustics, rain, lightning and a moonlit sky outdoors, and dust, drips, salt and bubbles indoors.
- A finishing pass with underwater refraction, colour grade, vignette and film grain.
- Synthesised sound rendered into cached samples, with streamed surf and drone ambience.
- Native controller support through SDL3: Xbox, PlayStation (DualShock 4, DualSense) and Switch (Pro Controller, Joy-Con pairs), with the extras below.

**Play on Windows:** unzip `Hollowmere-Godot-Windows.zip` and run `Hollowmere.exe`. Keep the `data_Hollowmere_windows_x86_64` folder next to it; it holds the .NET runtime. Saves live in `%APPDATA%\Godot\app_userdata\Hollowmere`.

**Build it yourself** with the Godot 4.7 .NET editor and the .NET 8 SDK:

```sh
node tools/export-data.js                 # rooms, items and lore -> godot/data/game.json
python3 tools/make_light_textures.py      # light shapes -> godot/textures/
cd godot && dotnet build                  # compile the C#
godot --headless --path . --export-release "Windows Desktop" ../windows-build/godot/Hollowmere.exe
```

The game has its own test harness. These run inside the real game:

```sh
godot --headless --path godot -- --test              # every scripted scenario: the play.js ports, controllers, the opening, hints
godot --path godot -- --shots out/ [room...]         # screenshots of every room
```

| Folder | Contents |
| --- | --- |
| `godot/src/Core` | Constants and helpers, game data loading, input and controllers (`Pad.cs`: detection, rumble, light bar, gyro), synthesised audio, settings |
| `godot/src/World` | The tide clock, tile queries and water, AABB physics |
| `godot/src/Game` | Player, bodies and knots, bosses, objects, combat, story, narrative, hints, the opening's zones, the game loop and saving |
| `godot/src/View` | The world view (lights, shadows, water, sky, post), tile art, HUD, button glyphs, menus, a Canvas-style drawing layer |
| `godot/shaders` | Water and post-processing shaders |

## Controllers

Both editions play fully on a controller, from the title screen to the endings. Prompts always show the buttons of the controller in your hands: Xbox letters, PlayStation shapes, or Switch letters in Nintendo's positions. They switch to keycaps the moment you touch the keyboard or mouse.

| Control | Action |
| --- | --- |
| Left stick | Move, climb, swim. A light tilt creeps quietly; click it (L3) to keep sneaking |
| Right stick | Aim. Leave it alone and Maren aims where she walks; a gentle aim assist settles on knots and barrels |
| A / Cross / Switch B | Jump (down + jump drops through planks) |
| B / Circle / Switch A | Dash |
| X / Square / Switch Y | Gaff strike |
| Y / Triangle / Switch X | Interact, talk, drag a body. Hold it to burn a downed body |
| LB / L1 / L | Gaff Swing (hold) |
| RB / R1 / R | Reload. Hold it to quick-craft rivets from scrap |
| LT / L2 / ZL | Focused beam (hold) |
| RT / R2 / ZR | Fire |
| D-pad up | Poultice. Hold it for the Stitch Kit |
| D-pad down | Ballast Boots |
| D-pad left / right | Change weapon |
| R3 | Shutter the lantern |
| View / Create / − | Journal: map, satchel, crafting, gear, logbook. The bumpers turn its pages |
| Menu / Options / + | Pause |

Menus are navigated with the d-pad or left stick. On a Switch controller, A (the right button) confirms and B backs out, as on the console; this can be swapped in the controller settings. Unplugging the controller mid-game pauses it. Rumble follows every jolt, shot and hit, with Off / Gentle / Full settings.

The Godot edition adds the platform features SDL3 exposes: the DualSense and DualShock 4 light bar glows like Maren's lantern (dimmer as the oil runs low, red when she is nearly dead, sea-green underwater), and gyro aim is available on PlayStation and Switch controllers.

## The opening and hints

The game teaches itself without a tutorial screen. A new run starts on the Breakers, the outer rocks where Maren's rowboat breaks up, and the climb to the reef asks for each of her moves in turn: jump a rock, clear a tide pool (falling in teaches swimming), wall-jump up an old hull stood on end, drop through planks, shoot an oil barrel so its fire cracks a salt plug, and dash through a tunnel of salt crystals. The tide holds at Low out there, and the tide dial only appears once she reaches the reef.

Nothing tells the player what to do unless they stall. A faint glyph and a word or two rise over Maren only when something is relevant and the player has hesitated for a few seconds (walk, jump, swim, wall-jump, drop, shoot, dash, heal, focus the beam, swing, fight the joints, gaff the Crown, reload, craft, change weapon, map, the Ballast Boots and the Jet Valve). Each hint retires for good the first time its action is done, whether or not it was ever shown, so a player who works things out never sees one. Maren's own thoughts carry the rest: wet stone will not take a grip but timber will; the salt cracks in the heat; sparks mean the flesh is only weight. Hints can be switched off in the pause menu.

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
- **World.** Seven regions, 26 hand-built rooms, six Bell Rooms. Puzzles: Sluice Ladder barge, Hymn of Hours, Tuned Bells (the Tide Key becomes a tuning fork), Lamp Prisms (bend the focused beam) and Dead Weight (drag knotless bodies onto the lift).
- **RPG layer.** Four NPCs (Ansel, Odile, Hesper, Agathe). Four quests with physical consequences: Lines Gone Slack, The Choir's Water, The Prior's Tables and A Keeper's Log. The Brine breaches a Bell Room in Act III. Three endings: Relight the Lamp, The Saint's Hymn and Keeper's Dark, each gated as the design describes.
- **Fairness and tension.** A pacing valve after fights, readable dormancy (true corpses have no knots), adaptive encrustation (new bodies grow plates over the knot type you break most, always leaving two open, and Ansel remarks on it), tells before damage, and the logbook map that only records water lines for tide phases you have seen.
- **The opening.** The Breakers teach movement without words (see above). Then the Wrackline and the Tithe Hall set piece play as written: the first Shucker stands when the water reaches it, lifting the crank tolls the bell and floods the hall, you escape by chandelier rings to the gallery, and the Undertow passes behind the glass.
- **Difficulty.** Pilgrim, Keeper and Drowned, plus a separate 100% / 150% / 200% tide-length setting.

## Scope compared with the document

The document plans 8-10 people over 30 months for about 180 rooms and a 10-12 hour game. This build is a complete but compact version of it: all seven regions and the whole critical path, from the outer rocks to all three endings, across 26 rooms, so a first run is far shorter than the document's 10-12 hours. Things that are smaller than the document describes:

- 26 rooms and 6 Bell Rooms instead of about 180 and 14. Bell-cable lifts, which the document lists as cuttable, are omitted.
- Hesper and Teodor are conditional fights rather than six fixed bosses.
- Art and audio are procedural placeholders: stick-skeleton puppets, tile shading and synthesised sound.

## Development tools

```sh
node tools/validate.js      # room sizes, legend, and that every open edge meets an open neighbour
node tools/reach.js         # movement-model reachability of the critical path, gate integrity, and soft-lock (trap) detection
node tools/play.js          # 26 scripted scenarios with real keyboard, mouse and (simulated) controller input (Playwright)
node tools/route.js         # the movement model plans routes; an in-page autopilot plays them with real physics
node tools/soak.js 5        # random bot in every room and tide phase, reports runtime errors
node tools/tour.js out/     # screenshots of every room
node tools/desktop-test.js <exe> [out/]   # launches the packaged desktop app, plays, checks fullscreen and saves
```

`F9` toggles invulnerability for testing. `Shift+F9` grants every tool.

## Code map

| File | Contents |
| --- | --- |
| `js/data/rooms.js` | The island: 26 ASCII rooms on a world grid, with their entities |
| `js/data/items.js` | Items, recipes, weapons, fittings, grafts and abilities |
| `js/settings.js` / `js/input.js` | Saved settings; keyboard, mouse and controller input, rumble |
| `js/glyphs.js` | Button prompts for keyboard, mouse, Xbox, PlayStation and Switch, on the canvas and in menus |
| `js/hints.js` | Hints that surface only when the player stalls |
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
