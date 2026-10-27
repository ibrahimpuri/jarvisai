# Adding art to Hollowmere

The game draws everything it needs from code, so it always runs. Made art (hand-painted, or painted offline by the
tools in `tools/`) replaces or enriches parts of that drawing wherever a file exists, and falls back to the code
wherever it does not. Everything lives under `godot/art/`. `Art.cs` loads it, and missing files are simply skipped.

Run the game with `HM_ART=off` to see it drawn from code alone, for comparison.

## The palette encoding

Room art is not stored in colour. Like the rooms the code draws, it is stored in three channels and coloured in the
game from the region's palette (`Palette.cs`), so one painting serves every state of the tide, which recolours
the world as it comes in:

| Channel | Means |
| --- | --- |
| red | **value**: 0 is the region's shadow colour, 1 its lit colour |
| green | **accent**: how much of the region's accent colour shows (moss, salt bloom, gilt, weed, the Gullet's teal) |
| blue | **glow**: light the surface gives off itself (windows, kiln fire, nodules), shown through the dark |

To paint in this encoding, paint a greyscale image for the value, a mask for where the accent shows, and a mask for
what glows, then put them in the red, green and blue channels of one PNG. `tools/paint_vistas.py --preview dir` shows
how the game will colour an encoded image.

## What can be added

### Surface materials: `art/materials/<surface>_d.png` and `<surface>_n.png`

Tiling textures for the lit walls and rock. Each surface has two maps, both tiling seamlessly:

- `_d` is the detail map. Red is the grain around 0.5 (darker pits, lighter faces), green is where the accent grows
  on it, and blue is its cavities.
- `_n` is the normal map, with x toward the higher side and y up the screen as negative, the same convention the
  world shader uses for the relief it reads from the drawing.

The surfaces are `ashlar`, `rock`, `brick`, `saltrock`, `bone`, `flesh` and `shore`. Which room uses which is set in
`Art.SurfacesFor`, and how strongly each one shows (tile size, grain, bump, accent, cavity) is set in
`Art.SURFACES`. `tools/paint_materials.py` paints the current set; replace any of them with a painted texture of the
same name.

### Vistas: `art/vistas/<region>.png`

The great space behind the ruined wall, one per region (`wrackline`, `saltworks`, `cloister`, `choir`, `ribvault`,
`gullet`, `skull`), in the palette encoding. It is drawn on the far plane, scaled to cover the room's far area, and
each room of a region shows a different part of it. Paint it wide (2048 by 1024 works): light, receding layers, the
region's architecture, aerial perspective (paler and softer further away). The game blurs it a little for depth of
field and darkens it toward the vault. `tools/paint_vistas.py` paints the current set.

## After adding a file

Godot has to import a new image before the game can load it. Open the project in the Godot editor once, or run:

```sh
godot --headless --path godot --import
```

Commit the image together with the `.import` file Godot writes beside it.
