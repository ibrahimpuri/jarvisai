# Movement: climbing and the smaller frustrations

A playtest on a PS5 controller found that pressing Cross to climb a ladder did nothing. Four things were behind it:

- **Cross never climbed.** At a ladder, jump only jumped. The only way onto a ladder was pushing the left stick up.
- **The stick needed a shove.** "Up" counted only past about 60% of a full push.
- **A stick that leaned dropped her.** On a ladder, any sideways lean past 25%, while "up" was not quite registering,
  let go of the ladder.
- **The d-pad could not help.** D-pad up is the poultice button, so the d-pad did not climb.

There was also no ledge grab. A jump that clipped the corner of a ledge slid Maren down the wall.

## Ladders now

| Input | Does |
| --- | --- |
| Up on the stick (about a third of a push), or W | Climbs a ladder she is standing at, or catches one she is in the air beside |
| Jump (Cross, A, Space) at a ladder, no direction held | Gets on and climbs; holding jump keeps climbing |
| Jump held in the air beside a ladder, no direction held | Catches it |
| Jump with a direction, on a ladder | Leaps off to that side |
| A firm push sideways, on a ladder | Lets go |
| D-pad up and down, on a ladder | Climb (the poultice and the boots wait until she is off it, and a d-pad still held as she steps off the foot does not toggle the boots) |

- A run past a ladder with a direction held still carries on.
- Falling past a ladder with nothing held still falls past it.
- Up on the stick counts toward a ladder only when the push is mostly up. A run with the stick held a little high does not
  catch every ladder on the way.
- Climbing is a little quicker (112 px/s, was 95).

## Ledges

When a jump only just reaches a ledge, pushing toward it catches the lip and Maren pulls herself up in a quarter of a
second. Holding down lets her drop past instead. A jump pressed while she pulls herself up happens the moment she
stands.

The grab must never climb a wall the level means as a wall. The level design uses 4-tile stone walls as barriers that
need a ladder, a rope, timber to wall-jump, the Jet Valve or the tide. A plain jump lifts Maren's feet 61 px, just under
4 tiles (64 px), so the walls hold. So the grab only catches a lip that the way she last left firm footing could have
reached anyway:

| She last left | Highest lip she will catch above that point |
| --- | --- |
| The ground | 56 px (a jump clears 61) |
| The ground in water, with the Ballast Boots | 45 px |
| The water's surface | 37 px (a jump out clears 42) |
| A timber wall she was hanging on | 50 px |
| A swing rope | 40 px |
| A ladder | 19 px (she can climb it instead) |
| A Jet Valve burst in the air | 56 px from where she fired it |

Crystals, gates, the shut sluice hatch and the island's edge are never climbable.

## The smaller fixes

- **A light tilt no longer shortens a jump.** A light tilt of the stick sneaks, and sneaking is slow. It used to slow her
  in the air too, so a jump with the stick not fully pushed fell short. Sneaking now only slows her steps on the ground.
- **Tap-or-hold buttons act on the press.** Triangle (interact / burn), R1 (reload / quick-craft) and d-pad up (poultice
  / stitch kit) used to wait for the release, so they could tell a tap from a hold. When holding would do nothing
  different, they now act the moment they are pressed. That means no body nearby to burn, no scrap to craft with, or no
  stitch kit.
- **Swimming follows the stick.** On a pad she swims where the stick points, at the speed it is pushed, instead of in
  eight fixed directions.
- **Look up and down.** Standing still and holding down (or up, away from a ladder) moves the view that way after a
  moment. In a long fall the view also leans ahead to show where she will land.
- **Ceiling corners.** A jump that clips the corner of a ceiling by a few pixels slides past it into the gap instead of
  stopping dead.
- **Swinging.** The rope shortens or pays out only for a push that is mostly up or down, not a pump that leans.
- **Dragging bodies** is a little quicker (44 px/s, was 32).
- **Controls page.** The controls page explains the new climbing. It also had the quick-craft wrong: a scrap makes 2
  rivets in 2.5 s, not 3 in 2.

## Tests

- **`--test climb`** plays climbing as a PlayStation player would. It covers:
  - Cross at a ladder's foot and half a stick push.
  - A stick leaning sideways, and a firm push off.
  - Cross with a direction, and a running jump past a ladder.
  - Catching a ladder in the air, and falling past one.
  - The top of a ladder, and Space on a keyboard.
  - Two set-up ledge cases.
  - The real-input checks below.
- **The real-input checks:**
  - 20 running jumps from four distances and five timings at a 3-tile ledge must all get out. Without the grab, 18 do.
  - Repeated jumps at a 4-tile stone wall must all fail.
- **`--test qol`** covers:
  - A light-tilt jump.
  - Triangle on the press.
  - The d-pad on a ladder, and the boots when she steps off it.
  - Analog swimming.
  - Looking down.
  - A ceiling corner.
