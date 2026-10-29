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
| W / S | Climb ladders, swim, shorten or lengthen the swing rope. Standing still: look up or down |
| Space | Jump (S + Space drops through planks). At a ladder: climb it. Held in the air: catch one |
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
| V | Quick-craft 2 rivets from 1 scrap |
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
- An art direction between Hollow Knight and Rain World. Each region has one colour identity (sea-glass grey-green on the shore, bone and amber in the Saltworks, moss in the Cloister, blue and gold in the Choir, ochre in the Ribvault, black and teal in the Gullet, lamp-orange in the Skull), and like Rain World's palettes it drifts with the cycle: as the tide comes in everything goes colder and darker, the fog thickens, the rain and lightning outside build to a storm and the ceilings drip. Rooms are drawn in encoded value/accent/glow and coloured by a palette shader (`Palette.cs`, `shaders/palette.gdshader`), so the tide recolours them without redrawing.
- Depth in layers (`Strata.cs`): a far vista of the region's architecture sunk in fog (vaulted arcades and lancets, a rose window and organ, scaffold towers and gear wheels, the giant's ribs, the throat of the Gullet, the Skull's lit eye sockets), closer columns, gantries and crags in the mid distance with parallax, a ruined back wall that survives only near the rock (masonry falling brick by brick, an ossuary of skulls in the bone halls) and breaks open onto the depths, and an out-of-focus foreground of rubble, stumps, chains, roots and reeds in front of everything.
- The playable rock as a silhouette (`Terrain.cs`): drawn on a dual grid so outer corners round and inner corners fillet while the edges stay on the collision lines, darkening away from its faces, with a lit lip where it faces up, and grass, moss, salt, bone, roots and stalactites by region.
- Characters as silhouettes with one bright note: Maren dark in her coat and a short cloak that trails on verlet rope physics, her brass helmet her face and its port glowing when the lantern is lit; the drowned in black robes under bone-white masks.
- Set dressing generated from each room's tiles (`Decor.cs`): arcades and lancet windows, timber framing in the Saltworks, the giant's ribs, ridged flesh in the Gullet; banners, sconces, candles, statues, barrels, nets, books and bones; kelp, ivy and chains that sway; cobwebs, moss, salt, barnacles and a wet tide band on the stone. Candles, sconces, lamps and glowing nodules are real flickering lights, with moths and drifting spores.
- A parallax night behind the shore (`Backdrop.cs`): the island with the abbey on its plateau and the dark lighthouse on the skull, moonlit clouds, sea stacks, fog banks, the moon's path on the water and gulls. The title screen is built from the same scene, with a boat rowing in.
- Vector-drawn characters with simple rigs: Maren in her brass helmet and oilskin coat (stride, coat sway, landing squash, lean, bubbles under water), hooded and salt-crusted drowned with torn hems, NPCs, the Gullet, the Abbot, the Undertow and the Shoal. Item icons in the HUD, the satchel and on pickups.
- Hit-stop and white hit flashes on knots, dust from landings and dashes, a dip to black between rooms and a red edge at one pip of health.
- Fonts: IM Fell English for titles and text, Alegreya Sans for small print (both SIL Open Font License, in `godot/fonts`).
- Native controller support through SDL3: Xbox, PlayStation (DualShock 4, DualSense) and Switch (Pro Controller, Joy-Con pairs), with the extras below.
- **Dismemberment** (`Gore.cs`). A Brine body is held up by its knots, so when a limb knot breaks the limb comes away: the forearm, the shin, the whole arm or the jaw tears off as a tumbling piece, the stump keeps weeping, and old blood thick with salt sprays away from the shot. It stains floors and walls until the tide comes in and washes the stone clean, and under water it hangs in slow dark clouds. Pieces lie where they fall and Maren kicks them as she walks. A body the tide re-knots grows its lost limbs back in salt crystal, so a scarred walker tells you it has died before. A Saltwright's pick goes with the hand that held it. The pause menu sets gore to Full, Reduced or Off (salt only, as before).
- **The voices of the drowned** (`Voices.cs`, `Audio.cs`). A formant synthesiser gives each kind of Brine body its own voice. Shuckers murmur wetly while they lie dormant, moan as they walk, cry out the moment they see Maren, snarl before they lunge, wail when a limb comes off and rattle when they go down. Bellmouths hum the Hours to themselves, Saltwrights breathe through salt and grunt as they swing, Gullets click above you, the Grafted Choir sings the hymn in D and shrieks a semitone apart, and the Abbot roars. Only the knotted dead breathe, so a silent body is a true corpse. Their wet, dragging or salt-crunching footsteps are placed in the room, so you can track them by ear. Each room has its own reverb (the nave and the sunken choir ring for seconds), the world muffles when Maren's helmet goes under, a sting of strings marks the moment something first sees her, Bell Rooms hold a soft breathing chord, and indoors the Saint's ribs groan under the abbey and a bell is rung somewhere far off.
- **Hurt, inside the helmet.** At two pips Maren's faceplate cracks, the edges of the view craze like the glass she looks through, and she breathes hard inside the helmet.
- **The Mere's Hollow** (`Hollow.cs`), a relic the sea gives Maren on the Long Pier, with four water verses on an energy bar. See [The Mere's Hollow](#the-meres-hollow).
- **A guide, a journal and a new chart** (`Guide.cs`, `Chart.cs`): the next main objective on the HUD, in the world, in the journal and on a hand-drawn chart with the route marked. See [The guide](#the-guide-the-journal-and-the-chart).
- **Combat tuned so Maren is the prey**, and **two Act III legendary weapons**, the Burning Glass and the Saint's Tine (`Legend.cs`), with the sea's hold on her and a fourth ending, Host of the Mere. See [Maren is the prey](#maren-is-the-prey), [The last night](#the-last-night-the-burning-glass-and-the-saints-tine) and [godot/docs/COMBAT.md](godot/docs/COMBAT.md).
- **Four crossings and a true ending** (`Difficulty.cs`): one difficulty table from Pilgrim (seven pips, three rivets to a Shucker) to Drowned, and an unlockable Mere-Saint crossing whose hidden lullaby opens a fifth ending and a glimpse of part II. See [The crossings](#the-crossings-the-mere-saint-and-the-true-ending).
- **Climbing that works on a pad** (`Player.cs`, `Inp.cs`): Cross climbs ladders, a softer stick, the d-pad on ladders, a mid-air catch, a ledge grab that respects the level's walls, and smaller movement fixes. See [Climbing and movement](#climbing-and-movement).
- **Handmade menus grown over by the sea** (`MenuArt.cs`, `MenuWidgets.cs`, `UiTitle.cs`, `UiSatchel.cs`, `UiBell.cs`): every menu drawn as driftwood, slate, planks, parchment or stone with kelp, wrack, barnacles and salt over its edges, and a satchel, crafting page, upgrade bench and item box modelled on the Resident Evil remakes. See [The menus](#the-menus) and [godot/docs/MENUS.md](godot/docs/MENUS.md).
- **Lighting and depth** (`LightingManager.cs`, `shaders/world.gdshader`). Direct light from every lamp, fire and the lantern with 2D shadows, shading painted relief (each brick turns toward the light, wet stone below the tide line glints); indirect light as a sky/ground hemisphere plus a bounce buffer every lamp splats into, so light spills round corners and warms whatever stands near a fire; a key light per region (the moon outdoors, with shadows). Each region has a mood (ambient, key light, fog, wetness, depth of field and a colour grade) that blends over a few seconds when you cross into the next. Fog with a body hangs between the depths and the play, lit from inside by the lamps, with light shafts falling at the region's angle and dust turning in them; in front of it the ruin's own bones stand in near-black silhouette (the vault's broken ribs and piers, a candle corona on its chains, the Saltworks' pipes and hoists, the Saint's spine); the depths darken toward the vault and are out of focus, as is the foreground, which thins to a window wherever it would hide Maren; and bloom lets every lamp glow into the air. See [the graphics blueprint](godot/docs/GRAPHICS.md).
- **Walls that remember** (`Masonry.cs`): ashlar courses with chipped edges, damp, drip streaks under every ledge, soot above every lamp that burned, moss, salt bloom and gilt by region, and in the tidal rooms the sea's own record: green stone below the high-water line, a salt rind, barnacles, and a storm line far higher from the night the light went out.
- **Made art, where it exists** (`Art.cs`, `godot/art`). Every wall and stretch of rock carries a tiling surface material with its own normal map, so lamplight rakes across limestone, brick, salt-rotted rock, bone, flesh and basalt rather than flat paint; each region has a painted far vista (a drowned chapel at the back of a sea cave, the salt halls and their wheels, the cloister garth, the rose window over the flooded Choir, the inside of the giant's ribcage, the Gullet's throat, the Skull's eye sockets open on the night); characters get an ink edge and a rim in the nearest lamp's colour so they stand off the world, and Maren is never lit by her own lantern, so her brass helmet keeps its shape. Water mirrors what stands above it, rippled and fading with depth, so the flooded Choir holds its lamps. Art is stored in the same value/accent/glow encoding as the rooms, so the tide still recolours it, and the game falls back to its own drawing wherever a file is missing. `tools/paint_materials.py` and `tools/paint_vistas.py` paint the current set; [ART.md](godot/docs/ART.md) explains how an artist adds or replaces art.
- **Sound built to frighten** (`Score.cs`, `Dread.cs`, `Voices.cs`, `tools/score`; see [AUDIO.md](godot/docs/AUDIO.md)). An adaptive score, rendered offline as looping stems and mixed live: every region has its own music (the hull's timbers groaning and the Saint calling from under the reef, the dead machinery of the Saltworks still breathing, monks chanting with one voice sliding off the note, a drowned organ playing the hymn, the giant's heart, a throat swallowing, the lullaby on a music box running down in the Skull), and over it dread rises when something knows she is there, the chase comes in when it is coming, the drowned sing under the water as the tide rises, and the Bell Rooms have the only warm music on the island. Everything is in D, around two themes: the Choir's hymn and the lullaby Agathe sang Maren. Stingers mark being seen, a scare, a boss, a discovery, the tide turning and death. When nothing hunts her, a dread director makes the dark speak: knocks on the far side of a wall, nails on stone, her name whispered inside the helmet, footsteps that stop, something breathing behind her, a child humming the lullaby, and once in a long while a slam right beside her. The drowned have new throats (a growl an octave down, several voices in one body, screams torn ragged, gasps before they cry, sobbing when a limb comes off, death rattles, the sleeping dead muttering her name), and Maren's boots sound of what is under them, her heart pounds at the last pip, she chokes when her air runs out and coughs the sea out when she comes up. Music ducks under screams, everything dulls under water, and the pause menu has a separate music volume.
- **Monuments and story focus** (`Monuments.cs`, `StoryFocus.cs`). Ten rooms have a monument standing behind the play that tells what happened there: the first Hult holding up a lamp with no flame in it, a board of little boats for every ship the reef took, the tithe scales, the Abbot with salt growing out of his palm, a brother with his hands over his eyes, the last Bell-Roper still holding his rope, the great bell fallen through the Choir floor, a child's mitten on the highest climbing peg, a ship half swallowed by the Gullet and the cracked lighthouse lens. Stop near one (or an unread carving, a bell, the font) and the world notices: the camera eases toward it and follows more loosely, the frame leans in, the background softens, the light warms to its colour and a soft light comes up on it, and if she stays a moment Maren has a thought about it. Never in a fight: anything hostile nearby lets it all go at once.

**Play on Windows:** unzip `Hollowmere-Godot-Windows.zip` and run `Hollowmere.exe`. Keep the `data_Hollowmere_windows_x86_64` folder next to it; it holds the .NET runtime. Saves live in `%APPDATA%\Godot\app_userdata\Hollowmere`.

**Build it yourself** with the Godot 4.7 .NET editor and the .NET 8 SDK:

```sh
node tools/export-data.js                 # rooms, items and lore -> godot/data/game.json
python3 tools/make_light_textures.py      # light shapes -> godot/textures/
python3 tools/paint_materials.py          # surface materials -> godot/art/materials/ (optional: committed)
python3 tools/paint_vistas.py             # region vistas -> godot/art/vistas/ (optional: committed)
python3 tools/score/compose.py            # the score, stingers and ambience beds -> godot/audio/ (optional: committed)
cd godot && dotnet build                  # compile the C#
godot --headless --path . --export-release "Windows Desktop" ../windows-build/godot/Hollowmere.exe
```

The game has its own test harness. These run inside the real game:

```sh
godot --headless --path godot -- --test              # every scripted scenario: the play.js ports, controllers, the opening, hints
godot --path godot -- --shots out/ [room...]         # screenshots of every room
godot --path godot -- --test perf                    # frame cost of the painters in the busiest rooms, and of gore at its caps
godot --path godot -- --test showcase --out shots/   # staged screenshots of the Riptide, the Answer, a cracked faceplate and the journal
godot --headless --path godot -- --test voices --dump-sounds sounds/   # write every sound made to a WAV file
godot --headless --path godot -- --test audio        # the score follows the room, the hunt, Bell Rooms, the tide; the dread director
godot --path godot -- --test monuments --out shots/  # a screenshot tour of the monuments with the story focus up
HM_GFX_OFF=fog,haze godot --path godot -- --shots out/ kilns   # hide the fog planes to judge their part in a frame
```

| Folder | Contents |
| --- | --- |
| `godot/src/Core` | Constants and helpers, game data loading, input and controllers (`Pad.cs`: detection, rumble, light bar, gyro), synthesised audio, the adaptive score and mix (`Score.cs`), settings |
| `godot/src/World` | The tide clock, tile queries and water, AABB physics |
| `godot/src/Game` | Player, bodies and knots, bosses, objects, combat, story, narrative, hints, the opening's zones, the game loop and saving; gore (`Gore.cs`), the voices of the drowned (`Voices.cs`), the Mere's Hollow (`Hollow.cs`), the last night's weapons and the sea's hold (`Legend.cs`), the guide (`Guide.cs`), the story focus (`StoryFocus.cs`) and the dread director (`Dread.cs`) |
| `godot/src/View` | The world view (lights, shadows, water, post), the lighting manager and region moods (`LightingManager.cs`), region palettes (`Palette.cs`), depth layers (`Strata.cs`), back-wall masonry (`Masonry.cs`), monuments (`Monuments.cs`), silhouette terrain (`Terrain.cs`), tile art, room dressing (`Decor.cs`), the shore night and title vista (`Backdrop.cs`, `TitleVista.cs`), HUD and item icons, button glyphs, the menus and their hand-drawn art (`Ui*.cs`, `MenuArt.cs`, `MenuWidgets.cs`), a Canvas-style drawing layer that batches its shapes |
| `godot/shaders` | The lit world (`world.gdshader`), characters in the bounce light (`entity.gdshader`), fog and light shafts (`fog.gdshader`), the palette shader with depth of field and foreground occlusion, water, and the finishing pass with the colour grade |
| `godot/docs/GRAPHICS.md` | The graphics blueprint: the layer stack, the lighting engine, the story focus |
| `godot/docs/ART.md`, `godot/art` | How made art is added, and the art itself: surface materials and region vistas |
| `godot/docs/AUDIO.md`, `godot/audio`, `tools/score` | The sound design, the rendered score, stingers and ambience beds, and the offline composer that renders them |

## The Mere's Hollow

*AUDIT ET RESPONDET. It hears, and it answers.*

The Mere's Hollow is a reliquary box of Mere-ivory holding the Saint's ear-bone. Whale ear-bones are the densest bone there is and often the last part of a whale to wash ashore. The first brothers found this one on the strand, curled like a shell and humming, and named the island for it. Then they dug, found the rest of the Saint underneath, and stopped naming things. For three hundred years the Choir sang into it every Hour, and the tide came when it was called. When the Abbot wanted it for his salt pans, Hesper rowed it out past the reef and gave it back to the sea.

Forty nights later the sea gives it to Maren. The first time the tide turns while she stands on the Long Pier, the sea heaves up over the boards and slides back down, leaving the box behind, glowing faintly and humming. The same wave stands the first drowned man up. Ansel, Odile and Hesper each say what they know of it the first time they see her carrying it, a carving in the Sunken Choir marks the niche where it lay, and one of the Bell Room dreams is its own.

Its powers are verses of the Choir's hymn, the one already carved on the Choir Steps. The Hollow learns each verse by hearing the thing the verse is about:

| Verse | Line | Learned | What the Saint does |
| --- | --- | --- | --- |
| The Calling | *Sing to the sea* | with the Hollow, from the sea | The sea in the room rises or falls to the point Maren sings toward (up to 6 tiles), holds, then settles back. Only the tide answers: still pools, the brine shaft, the Choir's font and the Gullet's deep water do not hear it, so it never breaks the Ballast Boots, Pressure Seal or Choir's Water gates. With the Hollow brimming, a held note makes the Saint breathe early and the tide rushes through to its next turn. |
| The Riptide | *...and the sea will take you home* | when the Undertow passes the Tithe window | A rush of the sea throws back whatever stands before her, slams it into walls, puts out fire, drowns flares and tears loose any grip on her. Under water it carries her the way she points. |
| The Salting | *Sing to the salt and the salt will rise* | in the Saltworks, where the live salt hums back | Salt crusts one of the drowned where it stands, and the next blow shatters a limb. Sung at stone, a pillar of salt grows out of it for 15 s, as a step or a wall. Fire still cracks it. Saltwrights and the Abbot are salt already. |
| The Answer | *Sing to the bone and the bone will answer* | in the Cloister, where the brothers all turn their heads | One of the drowned, standing or fallen, turns on its own kind and sings while it fights. When the verse runs out its knots let go for good. |

Breaking a knot lets out the brine that held it, and the Hollow drinks it in and rings louder. This is resonance, an energy bar under the lantern gauge, and every verse spends a share of it: the Calling 30, the Riptide 35, the Salting 40, the Answer 70. A knot gives 4 and a crown 8. Held in the sea it drinks slowly, and only to halfway, and resting at a bell fills it to halfway too. A verse just sung rings in the bone: the Hollow drinks nothing from the sea for a moment, and that verse cannot be sung again until it has rung out (4 s for the Calling, 30 s for the Answer). Singing has a price, as the Choir warned: *Take of the Saint and the Saint takes of you.* Everything near hears the Hollow and wakes, and the swell rises. Water it calls over the downed dead stands them up at the next High like any tide. Inside a Bell Room nothing answers at all.

**Controls.** Keyboard: 1 to 4 and 6 choose weapons, 5 takes the Hollow in hand (or the Tine, once grown), H sings at any time (left click or J also sings while it is in hand), Y changes verse (or R while it is in hand). Controller: the Hollow sits after the weapons on the d-pad, RT sings and RB changes verse. Holding the note, with the Hollow brimming, turns the tide. The journal's Gear page lists the verses learned. The box hangs on Maren's belt, and its seam glows sea-green while it holds enough for a verse.

## The guide, the journal and the chart

Nobody should be lost on the island. The guide always knows the next step of the main story:
- **The tracker:** it sits under the tide dial, with a banner when the objective changes.
- **In the world:** a gold marker floats over the thing to find, and chevrons mark the way on through each room, at the foot or top of a ladder when that is the way.
- **The journal:** the first page after the map gives the objective in full, how to do it, and everything else worth doing.

The map is now Maren's chart of the island on old paper: every room she has entered drawn as its real shape, unexplored doors as dashed question marks, the Bell Rooms, benches and people she has met, the tides she has seen, and the objective as a red-ink cross with the route dotted in from where she stands. The guide can be turned off in the pause menu. [godot/docs/GUIDE.md](godot/docs/GUIDE.md) has the step list, and the opening fixes from the playtest:
- Ladders no longer carry Maren through shut gates. A shut gate opens from its far side.
- The Tithe Hall's walkway ladder now reaches down to the floor and the flood.
- A jump from the water's surface always leaves the water.
- The Evaporation Pans' hatch now closes slowly enough to run through.

## The menus

Every menu is made from the island and the sea has grown over it: kelp hangs from the corners and sways, wrack climbs the sides, barnacles crowd the corners and water drips off the salt-crusted lower edge. Nothing is an image; it is all drawn as it moves.
- **Title:** the name in tarnished brass with a tide line across it, salt, barnacles and kelp hanging off the letters. The choices are boards on a driftwood signpost, with a lantern on a chain that swings to the one in hand.
- **The crossing:** a candle, a storm lantern and a drowned skull for the three difficulties.
- **Satchel**, after Resident Evil 2: the suit's heartbeat gauge reads Steady, Hurt or Bleeding out beside the pockets, and the item in hand can be used, examined (it turns in the lamplight, with Maren's note on it) or crafted with.
- **Craft**, after Resident Evil Village: what it makes and each cost against what she has, with everything on hand along the foot.
- **Bench:** the upgrades as a tier ladder with the price, after Resident Evil 4's merchant.
- **The reliquary** is an item box with two pocket grids.
- **Other screens:**
  - the Bell Room has its bell swinging over the choices;
  - dialogue shows each speaker in a brass medallion, with the words typed out;
  - death sinks Maren into black water.

[godot/docs/MENUS.md](godot/docs/MENUS.md) has the details and the sources.

## The crossings, the Mere-Saint and the true ending

Difficulty is one table now, and the crossings differ a lot.
- **Pilgrim:** Maren has seven pips and a Shucker falls to three rivets. Scrap makes three rivets and a poultice restores three pips. She heals while nothing hunts her, the knots glint in the dark, hints come quickly and she keeps her satchel when she dies.
- **Keeper:** the island as it was made: five pips, seven rivets.
- **Drowned:** four pips and nine rivets. The drowned are faster, some blows land harder, and every save costs a tallow candle.

The crossing screen shows each one's numbers.

Finishing the night once, any way, wakes a fourth crossing, the **Mere-Saint**. The Saint is awake under the island and listening: ten rivets to a Shucker, the hardest blows, sharper hearing and a faster tide. On that crossing Agathe's nine pages each carry a line of the lullaby she sang Maren in their margins. Find them all, and the lullaby turns out to be the seventh verse of the Choir's hymn. Agathe's music box then appears at the lamp, and opens the true ending, **The Seventh Verse**, followed by a glimpse of part II. [godot/docs/DIFFICULTY.md](godot/docs/DIFFICULTY.md) has the whole table and the story (spoilers).

## Climbing and movement

Climbing was the worst of the controls on a PlayStation pad. Cross at a ladder now climbs it, and holding Cross keeps climbing. A third of a push up on the stick climbs, a stick that leans a little no longer drops her off, the d-pad climbs while she is on a ladder, and holding jump in the air catches one. A jump that only just reaches a ledge catches the lip and pulls her up onto it. The grab only catches lips her last jump could have reached anyway, so the island's 4-tile walls still need a ladder, a rope, timber or the tide.

The smaller fixes:
- **Jumps:** a light tilt of the stick no longer shortens a jump.
- **Tap-or-hold buttons:** they act on the press when holding would do nothing else.
- **Swimming:** follows the stick.
- **The view:** standing still and holding up or down looks that way, and a long fall shows where she will land.
- **Ceiling corners:** a jump that clips a ceiling's corner slips past it.

[godot/docs/MOVEMENT.md](godot/docs/MOVEMENT.md) has every rule and the tests.

## Maren is the prey

The drowned close on her faster than she can comfortably back away. Once a Shucker has seen her it lurches at nearly her running speed, then stops to gather itself and lurches again. Its wind-up is quicker and its lunge longer, a grab bites every second and takes eight presses to break, and its knots take seven rivets to down it, one more than a magazine. Maren has 0.6 s of grace after a hit, a heavy blow knocks the wind out of her, her dash is a short dodge that needs 0.75 s to come back, and a scrap makes only two rivets. The Brine grows stronger with each act. The Pilgrim crossing keeps her old margins. [godot/docs/COMBAT.md](godot/docs/COMBAT.md) has every number, before and after.

## The last night: the Burning Glass and the Saint's Tine

Two legendary weapons appear only at the spring tide (Act III). They are opposites: one is the lamp, and one is the sea.

- **The Burning Glass.** Agathe's spare lamp lens, left for Maren in the Ribvault Bell Room with her red cloth and a note. On the lantern it burns in a line like the great lamp: it cracks the knots it touches and burns the fallen so they never stand. It drinks a full lantern's oil in six seconds, slows her to a walk, and the lens must cool after three. Keyboard 6.
- **The Saint's Tine.** In the roof of the Gullet's dome there is a hollow in the bone the exact shape of the reliquary: the Saint's Ear. At the spring tide it runs with brine. Lay the Hollow in it with the Harpoon Gun across it, if Maren chooses, and the bone grows them together. The Tine throws a lance of the sea that runs through three bodies and breaks a knot in each, on a quarter of the Hollow a throw. Empty, it drinks her blood instead. While it is grown, the Hollow sings no verses, the bells bleed it dry, and resting heals her by half. A bench prises it apart again: the harpoon comes back as it was and the Hollow comes back empty.

**The sea's hold.** Every second the Tine stays grown together outside a Bell Room is counted and saved. A thin vein under the Hollow's bar shows it, and only the Glass's light burns any of it back out of her.
- A quarter of the way, the dark talks to her more.
- Halfway, the Tine has rooted, and prising it out tears her.
- Three quarters of the way, the drowned hesitate before they strike.
- After eight minutes she can hear the Saint under the island, dying and tired.

If she still has the Tine in her hand when she reaches the lamp, the sea is waiting at the lamp room's window. That is the fourth ending, **Host of the Mere**: she does what the Abbot of Salt could not, and becomes the thing the sea dreams through.

## What this build takes from Hollow Knight, Rain World, Dead Space and Resident Evil

The aim is a player immersed in Maren's journey, so each borrowing is chosen for that.

| From | What players love about it | Here |
| --- | --- | --- |
| Dead Space | Strategic dismemberment, and feedback in the world rather than on a HUD ([diegetic UI](https://www.hudsandguis.com/2012/08/22/dead-space-2-diegetic-interface-design)) | Knot breaks tear limbs off and change how bodies move. Low health also shows in the world, on Maren's cracking faceplate and the crazed edges of the view, and the Hollow's charge glows on her belt. |
| Rain World | Creatures that act and sound alive, and a [threat score that follows danger](https://killscreen.com/the-threat-music-and-animal-sounds-of-rain-world) | Every kind of drowned has its own voice and footsteps, placed in the room. A sting marks the moment you are seen, and the drone already rises with tension. The drowned the Answer turns fight their own kind. |
| Hollow Knight | A resource earned by fighting (SOUL), abilities gained in ceremonial moments, lore told through the world and left for players to piece together | Resonance from breaking knots. Each verse is learned at a place in the story, with an inscription. The Hollow's history is spread across a relic, three islanders, a carving and a dream. |
| Resident Evil | The stalker heard before he is seen, the relief of a safe room, scarcity, and an inventory that is a puzzle of its own | The Undertow's bell and the drowned's footsteps carry. Bell Rooms hold their own calm chord and the Hollow is silent there. Resonance is scarce and singing is loud. The satchel, crafting page, bench and reliquary follow the remakes' screens (see [The menus](#the-menus)). |

## Controllers

Both editions play fully on a controller, from the title screen to the endings. Prompts always show the buttons of the controller in your hands: Xbox letters, PlayStation shapes, or Switch letters in Nintendo's positions. They switch to keycaps the moment you touch the keyboard or mouse.

| Control | Action |
| --- | --- |
| Left stick | Move, climb (a third of a push is enough), swim where it points. A light tilt creeps quietly; click it (L3) to keep sneaking. Standing still, up or down looks that way |
| Right stick | Aim. Leave it alone and Maren aims where she walks; a gentle aim assist settles on knots and barrels |
| A / Cross / Switch B | Jump (down + jump drops through planks). At a ladder with no direction held: climb it; held in the air: catch one |
| B / Circle / Switch A | Dash |
| X / Square / Switch Y | Gaff strike |
| Y / Triangle / Switch X | Interact, talk, drag a body. Hold it to burn a downed body (with no body near, it acts on the press) |
| LB / L1 / L | Gaff Swing (hold) |
| RB / R1 / R | Reload. Hold it to quick-craft rivets from scrap |
| LT / L2 / ZL | Focused beam (hold) |
| RT / R2 / ZR | Fire |
| D-pad up | Poultice. Hold it for the Stitch Kit. On a ladder, the d-pad climbs instead |
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
- **Bosses.** The Abbot of Salt (his plates fall only when his slams hit the kilns). The Almoner, chained to the Refectory's table (Godot edition): which limbs break first decides whether he swims at you at High tide or heaves the table. The Precentor, fused into the Choir's organ (Godot edition): break his jaw while he draws breath, or ring the loft's tuned bell at the note of the salt in his throat. The Hanging Mother, a Gullet the size of a chapel in the Ribvault's Spine (Godot edition): cut her tendrils to bring her down onto the ribs, and break her spine there. The Undertow, a stalker under hard rules: late Rising and High only, deep water only, never in Bell Rooms, never within 10 s of a load, bell always audible first, ripples on the map. Breaking its buoy knot drives it off for the tide, and it can only be killed in its Gullet lair. The Shoal, which sheds a body per broken knot. Teodor and Hesper are conditional fights.
- **Sandbox systems.** Noise radii (walk, run, rivet, metal floors ×1.5, Muffled Soles), light and focused-beam occlusion, fire that spreads along timber and rigging and cracks salt walls, and persistence for bodies, bolts, items, doors and puzzle state.
- **Survival.** A slot-limited satchel (8 → 12 → 16; key items take 2 slots), anywhere and bench crafting, poultices and Stitch Kits that lock you in place, breath as helmet bubbles, Bell Rooms (rekindle, save, rest to the next Low, shared Reliquary, bench), and the hidden lifeline rule. On death your satchel stays where you fell, and the Ebb carries it to the Wrack Pool if you died in tidal water.
- **Build choices.** Seven fittings under a Pressure Rating raised by six Brass Seals. Three Brine Grafts, each with its own vulnerability; Ansel can cut one out once at the cost of a health pip. Four weapons with three behaviour-changing tiers paid in scrap and Mere-ivory.
- **Traversal.** Gaff Swing, Ballast Boots (sink through buoyant brine, stomp), Jet Valve, Tide Key, Pressure Seal (the Gullet's pressure won't let an unsealed suit dive) and Saltsight Lens.
- **World.** Seven regions; 26 hand-built rooms and six Bell Rooms in the first build, growing to the document's full island (120 rooms and 11 Bell Rooms so far; see [the full island](godot/docs/WORLD.md)). Puzzles: Sluice Ladder barge, Hymn of Hours, Tuned Bells (the Tide Key becomes a tuning fork), Lamp Prisms (bend the focused beam) and Dead Weight (drag knotless bodies onto the lift).
- **RPG layer.** Four NPCs (Ansel, Odile, Hesper, Agathe). Four quests with physical consequences: Lines Gone Slack, The Choir's Water, The Prior's Tables and A Keeper's Log. The Brine breaches a Bell Room in Act III. Three endings: Relight the Lamp, The Saint's Hymn and Keeper's Dark, each gated as the design describes. The Godot edition adds a fourth, Host of the Mere, and a true ending, The Seventh Verse, on the Mere-Saint crossing.
- **Fairness and tension.** A pacing valve after fights, readable dormancy (true corpses have no knots), adaptive encrustation (new bodies grow plates over the knot type you break most, always leaving two open, and Ansel remarks on it), tells before damage, and the logbook map that only records water lines for tide phases you have seen.
- **The opening.** The Breakers teach movement without words (see above). Then the Wrackline and the Tithe Hall set piece play as written: the first Shucker stands when the water reaches it, lifting the crank tolls the bell and floods the hall, you escape by chandelier rings to the gallery, and the Undertow passes behind the glass.
- **Difficulty.** Pilgrim, Keeper and Drowned, plus a separate 100% / 150% / 200% tide-length setting. The Godot edition reads them from one table (`Difficulty.cs`) and adds the Mere-Saint, unlocked by finishing the night.

## Scope compared with the document

The document plans 8-10 people over 30 months for about 180 rooms and a 10-12 hour game. The first build was a complete but compact version of it: all seven regions and the whole critical path, from the outer rocks to all three endings, across 26 rooms.

The full island is now being built from it, one region at a time, with a playable build after each. [godot/docs/WORLD.md](godot/docs/WORLD.md) has the plan:

- the seven regions on a larger grid
- about 180 rooms and 14 Bell Rooms
- six fixed bosses: the Abbot of Salt, the Almoner, the Precentor, the Hanging Mother, the Undertow and the Shoal
- four enemy variants
- bell-cable lifts

**Phase 1 is built:** the Wrackline and the Saltworks at full size, 61 rooms in all.
- **The Wrackline:**
  - east along the coast from the Seawall Chapel: the Hult Cottage, the Wreckers' Lookout, the cliff path, the Ferry House (its second Bell Room), the Wreck Field, the Rope Ferry Landing, the Drowned Hithe, the Tide Pools and the Sea Arch
  - under the sea: the sunk *Constance*, the Breakers' Floor, the Smugglers' Cut and a brine sump
- **The Saltworks:**
  - the Kilns climb to the Mine Head (its second Bell Room) and up through a salt mine in the Saint's flank to the Abbot, who now waits at its top
  - the Saw-Line is in the mine's Pick Gallery
  - a salt chute brings you back down
- **Two variants:**
  - the Hooded Bellmouth, which screams at noise rather than light
  - the Gullet Pair, which hangs from two tendrils
- **Eleven new kinds of the drowned, in three tiers:**
  - **start:** Saltgull flocks, the net-throwing Netter, the fireproof Kilnman
  - **mid:** the Bloater, the bell-ringing Toller, the Cantor who sings the dead back up, the Ossuary, the gliding Kite
  - **end:** the Drowned Diver, the Deepwalker, the light-drinking Wick

  Earlier kinds keep appearing later, and later kinds come into earlier regions on revisits. The journal keeps a bestiary of every kind Maren has seen, with how to put each one down. See [godot/docs/ENEMIES.md](godot/docs/ENEMIES.md).
- **The engine:** a chart that grows and zooms, saves that move with the island, and bell-cable lifts between a region's Bell Rooms from Act II.

**Phase 2 is built:** the Lower Cloister at full size, 86 rooms in all.
- 25 new rooms between the Bell-Rope Nave and the Prior's Walk, on two floors: a dry upper floor (the Dorter, the Library, the Counting House and its Wheel of Lots, the Chantry, the Pulpitum) and a tidal lower floor that floods at High water and that the Undertow walks (the Lavatorium, the Chapter House, the Undercroft, the Kitchens, the Charnel House, the Tithe Cells)
- **the Almoner**, the region's boss, chained to the Refectory's long table where the two floors meet; the doors west are chained to him
- **the Almonry**, the Cloister's second Bell Room, just past him
- the Jet Valve opens the Light-Well, the Bell-Cote and the Treasury's strongbox on the way back
- everything west of the Cloister moved 18 screens west to make room, and old saves land on it where it is now

**Phase 3 is built:** the Sunken Choir at full size, 104 rooms in all.
- 18 new rooms west of the Choir Bell Room: the cult's half-drowned church (the Narthex, the Drowned Stalls, the Song School, the Baptistery, the Bone Choir), the West Window and its bell tower, and the galleries back above the choir
- **the Precentor**, the region's boss, fused into the organ over the west door; he keeps the Tide Key
- **the Cantor's Vestry**, the Choir's second Bell Room
- tuned bells everywhere: each room's bell rings at the level of its own basin, and the Tide Key fills a basin to its deepest note

**Phase 4 is built:** the Ribvault at full size, 120 rooms in all.
- 16 new rooms inside the Saint's ribcage: the climb up the west ribs and the Rib Ladder, along the Collarbone and the Vertebrae to the Spine, and down the east ribs to the Lantern Lung and the Lamp Prisms
- **the Hanging Mother**, the region's boss, hangs from the vertebrae in the Spine; the Pressure Seal is beyond her
- **the Sternum Bell**, the Ribvault's second Bell Room
- the Lantern Skull now sits on top of the Ribvault, over the Skull Gate

**Phase 5 is built:** the Gullet at full size, 134 rooms in all.
- 14 new rooms in the Saint's throat, all deep water: down the Long Throat to the Stomach, through the Pylorus to the Deep, east along the Road and up the Up-Current, and back into the Undertow's lair from behind
- breath is the clock: air vents in the rock, and two domes of trapped air, **the Drowned Bell** and **the Last Air**, the Gullet's Bell Rooms
- the lair's front onto the Gullet Mouth stays shut until **the Undertow** dies

**Phase 6 is built:** the Lantern Skull at full size, and the island is whole: 145 rooms.
- 11 new rooms in the Saint's skull: the Sinus, the Crown in the wind, the eyes, the Nasal Bridge, the Jaw, the Temple's prisms and the Ear-Bone
- **the Shoal** turns in the Brainpan now and throws itself at you; its death lifts the bone shutter onto the Lamp, which moved to the far end of the Skull
- **the Keeper's Watch** and **Agathe's Room**, the Skull's Bell Rooms

**Playtested end to end:** sweeps that walk every door, soak every room with the drowned, use every object and save
at every Bell Room, and a check that the guide's way to every objective can be played (`godot/docs/PLAYTEST.md`).
- fixes: underwater ledges and ladder tops, the Cloister Bell Room's breach that never lifted, the guide pointing
  through shut doors and up one-way drops, banners and thoughts sitting on each other
- try a boss again from its door; ride the bell-cables to any lit Bell Room; an air-vent pointer when breath runs low;
  unopened boxes marked on the chart

Still smaller than the document: art and audio are procedural (stick-skeleton puppets, tile shading and synthesised sound).

## Development tools

```sh
node tools/validate.js      # room sizes, legend, and that every open edge meets an open neighbour
node tools/reach.js         # movement-model reachability of the critical path, gate integrity, and soft-lock (trap) detection
node tools/throughs.js      # how every room plays with each set of tools, for the guide -> godot/data/through.json
node tools/guidecheck.js    # the guide's way to every objective can be played (--bells: from every Bell Room too)
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
| `js/data/rooms.js` | The island's first 26 ASCII rooms on a world grid, with their entities |
| `js/data/rooms_<region>.js` | Each region's rooms for the full island, with their carvings (see `tools/data-files.js` for the load order) |
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
