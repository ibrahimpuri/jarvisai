# The crossings: difficulty, the Mere-Saint and the true ending

Difficulty used to be a handful of scattered checks. A Shucker took seven rivets on every crossing, and Pilgrim and
Drowned differed from Keeper by a few percent of enemy speed. Now one table (`src/Game/Difficulty.cs`) says how hard the
night is, and the whole game reads it.

## The table

| | Pilgrim | Keeper | Drowned | Mere-Saint |
| --- | --- | --- | --- | --- |
| Who it is for | the story | the island as it was made | those who know it | after a finished night |
| Maren's pips at the start | **7** | 5 | 4 | 4 |
| With all four Keeper's Stitches | **11** | 9 | 8 | 8 |
| Rivets to put down a Shucker | **3** | 7 | 9 | 10 |
| Knot strength | × 0.4 | × 1 | × 1.25 | × 1.5 |
| The great dead (bosses) | × 0.55 | × 1 | × 1.2 | × 1.35 |
| Speed of the drowned | × 0.75 | × 1 | × 1.12 | × 1.2 |
| A blow's telegraph | × 1.35 (longer) | × 1 | × 0.9 | × 0.85 |
| Pause between blows | × 1.4 | × 1 | × 0.88 | × 0.8 |
| Heavy blows | a pip softer, no winding | as they are | 15% land a pip harder | 30% land a pip harder |
| Grace after a hit | 1.1 s | 0.6 s | 0.5 s | 0.45 s |
| Dash window / recovery | 0.22 / 0.45 s | 0.12 / 0.75 s | 0.1 / 0.8 s | 0.1 / 0.85 s |
| Presses to break a grab | 4 | 8 | 10 | 12 |
| Rivets from a scrap | **3** | 2 | 2 | 2 |
| Extra scrap per container | +1 | 0 | 0 | 0 |
| Scrap at the start | 4 | 0 | 0 | 0 |
| Help when out of rivets and materials | always | always | half the time | a third of the time |
| A poultice restores | **3 pips** | 2 | 2 | 2 |
| Saving at a bell | free | free | 1 tallow | 1 tallow |
| Tide length | × 1.25 | × 1 | × 1 | × 0.85 (the Saint breathes faster) |
| How far the drowned hear her | × 0.7 | × 1 | × 1.15 | × 1.3 (the Saint is listening) |
| Lantern oil use | × 0.7 | × 1 | × 1.1 | × 1.2 |
| Hints wait | half as long | as set | as set | as set |
| Pad aim assist | × 1.5 | × 1 | × 1 | × 1 |

**Pilgrim also has:**
- **Healing:** a pip comes back every 18 s while nothing hunts her.
- **Glinting knots:** knots within 150 px glint even in the dark.
- **The satchel:** it stays on her shoulder when she dies.

The crossing screen shows each crossing's numbers under its description: pips, rivets to put down a Shucker, minutes per
tide, rivets per scrap and its special rules. The chosen tide length still multiplies the crossing's own.

Knot health is rounded so a rivet's two points count cleanly, and never drops below one. A body keeps its scaled
strength when the tide re-knots it.

### Rivets to put a body down

Two limb knots, then the crown. Every knot on the island goes through the same scaling, including grafted Hesper's
thicker knots and the bodies the Shoal sheds:

| | Pilgrim | Keeper | Drowned | Mere-Saint |
| --- | --- | --- | --- | --- |
| Shucker | **3** | 7 | 9 | 10 |
| Saltwright (plated) | **3** | 7 | 9 | 10 |
| Bellmouth | **3** | 6 | 7 | 7 |
| Grafted | **3** | 6 | 7 | 7 |
| Grafted Hesper | 6 | 10 | 12 | 13 |
| The Abbot | 6 | 10 | 13 | 14 |

## The Mere-Saint

The fourth crossing is asleep (a shut, salt-crusted eye on its card) until the player finishes a night once: any
ending, on any crossing. `Profile` keeps this in `user://hollowmere_profile.json`, beside the save, so a new game keeps
it. Once it is awake:

- **Its card:** a great eye ringed in ribs, sea-green light moving in it.
- **Its description:** *"The Saint is awake, and it is listening."*
- **The opening:** one more line before Maren sets out: *Tonight the Saint is awake under the island. It has been
  waiting a long time for a Hult to hear it.*
- **The title screen:** it says so.

## The true ending: The Seventh Verse

Hesper says it early: *"The hymn has seven verses. We know six. The seventh is the one the Saint sings back."* The score
is built around two themes, the Choir's hymn and the lullaby Agathe sang Maren. The true ending joins them:

1. **The margins.** On the Mere-Saint crossing, every one of Agathe's nine pages has a line of the lullaby pencilled in
   its margin. Agathe, at the lamp, tells Maren to read the margins.
2. **The song.** With all nine pages, Agathe says what it is. It is the seventh verse: the end the Saint has spent three
   hundred years trying to sing. She made it a lullaby so that it would never be lost, and so that the Choir would never
   have it whole. The Saint is not angry. It is dying and frightened, and the Brine is only its song getting into
   everything.
3. **The music box.** Agathe's music box stands open by the lamp. Winding it, with the Hollow in hand, sings the Saint
   the end of its song:
   - The Saint sleeps.
   - Every knot on the island goes dark, and the drowned lie down for good.
   - The salt falls out of everyone it was in.
   - At dawn the lamp is lit with nothing left for it to burn.
4. **The epitaph.** Its fates: Ansel is free of his crust, Odile rows the island's dead home, Hesper hears the verse at
   last, Agathe sleeps at night. Maren keeps the lamp, and some nights hears something far out at sea singing the first
   verse.
5. **The glimpse.** After the epitaph comes *Turn the page*: nine nights later, from the lamp gallery, over a painted
   night sea. The beam finds a light where there is no land, answering it: three short, one long, a Keeper's signal.
   In the Prior's cell is a fifth tide table that is a chart of the whole sea, with eleven more islands drawn as whales
   with too many ribs. Beside the last: *THE MOTHER. SHE HAS NOT STOPPED SINGING.* Then the title card: **HOLLOWMERE ·
   PART II · THE DEEP HOURS**. Far below the painted sea, something enormous turns over.

On the other crossings the margins are blank and the music box does not appear.

The text is all in `js/data/lore.js` under `saint`, and the fates under `fates.*.verse`.

## Tests

`--test difficulty` covers:

- **Every crossing:** her pips, and that a Shucker falls to exactly 3, 7, 9 and 10 rivets.
- **Blows:** a heavy blow costs 1 on Pilgrim and 2 (or 3) elsewhere.
- **Pilgrim:** healing while safe, and keeping the satchel on death.
- **The unlock:** the Mere-Saint card asleep until a night is finished, then chosen.
- **The margins:** they carry the lullaby on the Mere-Saint crossing.
- **The music box:** absent with eight pages, standing with nine.
- **The true ending:** from the confirm, through the epitaph and the glimpse, to the part 2 title.
- **Keeper:** blank margins and no box.
