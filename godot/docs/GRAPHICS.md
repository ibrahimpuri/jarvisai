# Hollowmere graphics blueprint

How the Godot build draws a room: the planes it is built from, how they are lit, and how the world reacts when
Maren stops to look at something. The target is somewhere between Hollow Knight (dark silhouetted play, haze,
soft light falling through it, a framed foreground) and Rain World (dense, grimy, rained-on places whose palette
drifts with the cycle). It all serves one goal: the player should feel they are in a real, ruined place that
remembers what happened in it.

The game is Godot 4.7 with C#. Where a Unity term makes the intent clearer it is given alongside the Godot one.

---

## 1. Architectural layer blueprint

### One camera, many canvases

Unity games of this kind often stack cameras: one for the background, one for gameplay, one for the foreground,
each rendering its own sorting layers, with a render texture in between for blur. Godot gets the same result with
**one `Camera2D`** and **several `CanvasLayer`s**, each a separate canvas with its own transform.

- A `CanvasLayer` with `FollowViewportEnabled` moves with the camera. The game sets each depth plane's offset
  itself, as `(cam - roomCentre) * (1 - f)`, so a plane with factor `f` scrolls at `f` times the speed of play
  (`WorldView.DepthLayer`). This is the parallax a Unity background camera gives.
- `CanvasLayer.Layer` is the **sorting layer**: a lower number draws further back. Inside a canvas, child order and
  `z_index` are the **order in layer**.
- A `DrawLayer` (our `Node2D` that paints with a Canvas-style API) owns one material and draws its whole sub-scene
  through it, which is what a **Sorting Group** gives in Unity: the group sorts and shades as one.
- A `SubViewport` is a **render texture**. Static art for each room is painted once into a SubViewport (a *bake*)
  and drawn as a single textured quad, so a room costs one draw per plane, not thousands.

### The stack, back to front

```
 layer  plane                     parallax   what is on it                                      lit by
 -----  ------------------------  --------   -------------------------------------------------  -----------------------------
  -10   sky                        screen    night sky, moon, clouds, sea (Backdrop.cs)          nothing (painted light)
   -8   far depth                  0.30      the region's vista: arcades, rose window, gears,    region palette + fog,
                                             ribs, the Gullet's throat (Strata.PaintFar)         depth-of-field blur
   -7   mid depth                  0.62      columns, gantries, crags (Strata.PaintMid)          palette + fog, slight blur
   -6   back fog                   0.75      drifting fog, lit from inside by every lamp,        radiance buffer (scatter),
                                             light shafts at the key light's angle, dust motes    key light colour
                                             turning in the beams (fog.gdshader)
   -5   near depth                 0.84      the ruin's own bones in near-black silhouette: the  palette, little fog,
                                             vault's broken ribs and piers, a candle corona,      slight blur
                                             pipes and hoists, the Saint's spine, the Gullet's
                                             tendrils (Strata.PaintNear)
 ---------------------------------------- the gameplay canvas (layer 0, parallax 1.0) -------------------------------
    0   back wall (bake)           1.0       ashlar masonry and its weathering (Masonry.cs),     world.gdshader: relief,
                                             windows, architecture, monuments (Monuments.cs)     Light2D, hemisphere, bounce
    0   deco                       1.0       swaying props drawn live: kelp, chains, candles     world.gdshader
    0   entities                   1.0       Maren, the drowned, objects, gore                   entity.gdshader + Light2D
    0   water, water lines         1.0       the sea and pools (water.gdshader)                  own shader
    0   solid rock (bake)          1.0       the rock Maren walks on (Terrain.cs)                world.gdshader
    0   front haze                 1.1       a thin mist in front of the world                   radiance buffer
    0   dyn, fx (shaded/glow/text) 1.0       salt crystals, sparks, embers, whispers             world/entity/additive
    0   occluders, lights          1.0       LightOccluder2D from the tiles, Light2D pool        -
 ------------------------------------------------------------------------------------------------------------------
    1   foreground                 1.12      out-of-focus rubble, stumps, chains, roots, reeds   palette, blur, occlusion
                                             in front of everything (Strata.PaintFore)           window around Maren
    5   finishing pass            screen     bloom, grade, lean, vignette, refraction, grain     post.gdshader
   10   HUD                       screen     health, lamp oil, tide dial, prompts, crosshair     never graded or leaned
   20   menus                     screen     -                                                    -
```

The **radiance buffer** sits off the stack: a `SubViewport` at a quarter of the view (`LightingManager.Radiance`)
into which every light in view is splatted as a wide soft blob. It is the bounce-light map that the world, the
characters and both fog planes sample through `SCREEN_UV`.

### Separating the gameplay plane from the atmosphere and the background

The planes are kept apart by what reaches them, not only by draw order:

1. **Light.** `Light2D`s and the `CanvasModulate` (the region's ambient darkness) act only on canvas 0. The depth
   planes and the sky are on their own canvases, so no lamp ever lights the far vista and the vista never darkens
   with the room. Their only light is painted in, plus the fog's scattered glow.
2. **Colour.** Depth planes are coloured by the palette shader with a strong pull toward the region's fog colour
   (aerial perspective: further means paler and closer to the air), and darken toward the top of the room (the
   vault), so the openings read as deep space lit from below rather than flat paper. The near plane is the
   opposite: almost black, almost no fog, so it reads as something close and dark in front of something pale and
   far, which is where a Hollow Knight room gets its depth. The gameplay plane keeps full contrast.
   Characters are drawn in their own colours with one bright note (Maren's helmet, a body's knots), so they read
   against any background.
3. **Focus.** Far and mid are blurred (a rotated 12-tap Poisson disc in `palette.gdshader`); the gameplay plane is
   always sharp; the foreground is blurred hardest. StoryFocus can pull the background further out of focus.
4. **Atmosphere between them.** The back fog plane hangs between the depths and the play, so the vista always sits
   behind air; the front haze is thin enough that it only shows where light passes through it.
5. **Foreground occlusion.** The foreground plane frames the shot as dark, out-of-focus shapes, but it must never
   hide the player. Its shader opens a soft window (radius 16% of the screen height, 30% of the silhouette left
   at the centre) around Maren's screen position every frame, so a pillar in front of her thins to a ghost of
   itself and closes again as she passes.

---

## 2. Lighting engine (`LightingManager.cs`)

One node decides how everything is lit, so the layers always agree. Every lit material registers with it
(`RegisterWorld`, `RegisterEntity`, `RegisterFog`, `RegisterPost`) and it sets their uniforms each frame.

### Direct light, with 2D shadows

- Every lamp, sconce, candle cluster, fire cell, flare and glowing pickup is a `PointLight2D` from a pool, with
  shadows cast by `LightOccluder2D`s generated from the room's tiles. The lantern is a cone light that walls block.
- Each region has a **key light** (`DirectionalLight2D`): the moon outdoors, casting shadows under every overhang;
  indoors a soft fill from the direction the region's light comes from (the rose window, the kilns, the shafts).
- The baked layers are flat paintings, so `world.gdshader` builds **relief normals** from the bake's value channel
  (brighter = standing out), with cavity occlusion from its Laplacian. In `light()` it shades each light with
  wrapped diffuse, a rim term (edges catching the light) and Blinn specular on **wet** stone, so a lantern swung
  past a wall rakes across every brick and the tidal stone glints.
- Wetness is real data: below the tide's high-water line, in brine and deep rooms, and in the region's mood.

### Indirect and ambient light

- **Ambient** is a hemisphere: one colour from above (`sky`), one from below (`ground`), times the region's
  `CanvasModulate`. Cavities are darker (`ao`).
- **Bounce** comes from the radiance buffer. A body standing by a kiln goes warm on the kiln side, a lamp in a
  corridor spills round the corner, a lantern carried through the hall glows in the fog around it.

### Regions, moods and colour grading

A `Mood` per region holds: ambient, sky and ground colours, key light colour, energy, angle and height, bounce and
occlusion strength, fog colour, the back and front fog densities, scattering and light shafts, wetness and
specular, the depth-of-field per plane, and the colour grade (exposure, an S-curve for contrast, saturation,
lift/gamma/gain and a split tone into shadows and highlights).

| Region | Reads as |
| --- | --- |
| Wrackline | sea-glass grey-green, damp, cold light from the sea side |
| Saltworks | bone and amber, salt hanging in the air, kiln light, strong shafts |
| Cloister | moss and wet stone, muted |
| Choir | deep blue under gold, light from the rose window, the floor drowned |
| Ribvault | ochre and old bone, wind dust in the shafts |
| Gullet | near black and teal, slick, lit by what glows in it |
| Skull | rust and lamp orange |
| Outdoors | moonlight with real shadows, rain on everything |

Crossing into another region starts a **2.8 second blend** (smoothstep) from the current mood to the new one, and
the region palettes the bakes are coloured from blend over the same time, so nothing snaps at a door. The frame
time fed to the blend is capped, so the long frame of a room bake cannot skip it. On top of the mood, the tide
pulls everything colder and darker as it comes in (ambient, fog, saturation and the key light), in Rain World's
manner, and StoryFocus can lean the ambient toward a monument's colour.

The grade runs in `post.gdshader` on layer 5, so it reaches every world plane but never the HUD. The same pass
adds **bloom**: the screen's own mip chain is sampled at three widths and whatever is bright in it bleeds out, so
lamps, shafts, glints and the lantern glow into the air around them (strongest in the Gullet, where the only
light is what glows).

---

## 3. Environmental interaction and juice (`StoryFocus.cs`, `Monuments.cs`)

### What it tracks

StoryFocus keeps the room's points of interest:

- the room's **monument**, a large story prop standing against the back wall behind the play (below);
- **carvings** not yet read (they stop mattering once read);
- the **bells**, the **font**, and **the Hollow** lying where the sea left it.

Each has a reach (weight 1 inside `inner`, falling smoothly to 0 at `reach`), an importance (`weight`: monuments
1, carvings 0.7, bells 0.55), and its own colour.

### How the focus rises and falls

Every frame the nearest thing in reach is weighed by distance and by how still Maren is (running past something
is not looking at it). The focus `W` rises at 0.9 per second and falls at 2.2 per second; when the subject
changes, the old one fades out before the new one rises, so the frame never jumps from one thing to another.

**Combat safety.** Anything hostile within 260 px, a recent hit or a boss in the room drops `Calm` to 0 at 5 per
second, and with it the whole focus: the camera never drifts toward a statue while something is coming for her.
Peace brings it back over a couple of seconds.

### What it changes

| Cue | How |
| --- | --- |
| Camera offset | the camera target moves toward the thing by `pull` of the gap (capped at `maxPull`), times `W` |
| Camera damping | the camera follows at `1 - 0.5 W` of its usual speed, so the frame settles and lingers |
| Lean | the finishing pass magnifies the frame by up to 6% about a point between Maren and the thing, so even in a one-screen room, where the camera cannot travel, the shot leans in. Aiming goes through `G.ScreenToWorld`, so the cursor stays true |
| Soft focus | the far and mid planes blur further (`FocusBlur`), so the lit thing on the back wall stands out of a softened background |
| Vignette | the frame's edges close in a little (`FocusVignette`) |
| Ambient colour | the region's ambient leans toward the thing's colour (`FocusTint`, `FocusW`) and characters take a little of it |
| Light | a soft light in its colour comes up on it, as a real Light2D (so it shades the relief and casts shadows) and in the bounce buffer |
| Thought | stay 1.6 seconds beside a monument and Maren has a thought about it, once (`lore.js`, `thoughts.mon_*`) |

### The monuments

Each is environmental storytelling first: it says what happened in the room without a word, and the thought only
confirms it for a player who stopped to look. Placement is from the room's original map, on the nearest clear
stretch of floor to a hint, so the art and the focus always agree.

| Room | Monument | What it says |
| --- | --- | --- |
| Seawall | **The first Hult**, holding up a lamp with no flame in it; red cloth round her wrist | Maren's family has kept this light for generations, and someone kept her memory |
| Chapel | **The board of little boats**, one for every ship the reef took; eleven are new | the reef has been killing since the light went out |
| Tithe Hall | **The tithe scales**: salt in one pan, the other worn smooth by a kneeling man | "Of every ten brothers, one" |
| Kilns | **The Abbot**, palm held out, salt grown from it like a second hand reaching back | the Saint's blood, taken, and what it took in return |
| Cloister | **The weeping brother**, hands over his eyes beside a basin, moss on his shoulders | the Rule: do not look into the water |
| Nave | **The last Bell-Roper**, slumped at the foot of a rope that goes up into the dark, still holding it | he rang the warning a tide too late |
| Choir | **The great bell**, fallen through the tower floor, cracked, its clapper thrown out | the night the abbey went quiet |
| Ribshaft | **The climbing pegs**, a child's red mitten on the highest one | Agathe, age nine: Maren's mother |
| Gullet Mouth | **A swallowed ship**, its keel and ribs standing out of the flesh wall | the Saint is not dead |
| Lamp Room | **The old lens**, cracked through, leaned against the wall | "Light against the salt" |

---

## 4. Surfaces

- **Masonry** (`Masonry.cs`): courses of ashlar of varied heights, each block its own stone with a lit top,
  contact shadow where the wall meets the rock, chipped corners where the ruin breaks, pitting and hairline cracks.
  Then the weather, each mark the trace of an event: damp blooming through, drip streaks under ledges, soot
  above every lamp that burned, regional growth (moss, salt bloom, gilt, weed), and in the tidal rooms the sea's own
  record: darker green stone below the high-water line, a white salt rind along it, barnacles near the floor and a
  faint storm line far higher, from the night the light went out.
- **Relief**: the value channel doubles as a height map, so every painted brick takes light from the side the lamp
  is on.

---

## 5. Tools

- `HM_GFX_OFF=fog,haze` hides the back fog or the front haze, to judge each one's part in a frame.
- `--test storyfocus`: every monument is placed; lingering by the Abbot brings up each cue; walking away and a
  hostile arriving let it go; crossing regions blends the light.
- `--test monuments --out dir`: a screenshot tour with Maren beside each monument.
- `--shots dir [room...]`: every room, for before/after comparison.
