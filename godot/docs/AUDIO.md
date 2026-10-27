# Hollowmere sound

The island is a ship of stone that the sea has got into, and everything it sounds like should say so. The design
goal is dread, not noise: long quiet stretches in which the player listens harder and harder, sounds that come from
where they cannot see, and a score that knows what is happening slightly before the player does. Rated 18+, so the
drowned are allowed to be as horrible to hear as they are to look at.

## The score (`tools/score/compose.py`, `Score.cs`)

Everything is in **D**, the key the drowned Choir sings in. Two themes run through the whole island:

- **The hymn**: the phrase the Hollow plays (A, G, F, D), and its chords (Dm, B♭, F, C, Gm, A). The Choir's drowned
  organ plays it; the drowned sing it at high tide.
- **Agathe's lullaby**: eight bars in 3, the song Maren's mother sang her in the lighthouse. It is the warmest
  thing in the game and the most frightening: a music box on the title, a glass harmonica in the sea caves, a bone
  flute in the Ribvault, a music box running down in the Skull, a cello in the Bell Rooms, and, in the worst places,
  a child humming it in the dark.

### Layers

The score is rendered offline as looping stems and mixed live by how things stand. Each layer fades toward a target
the game sets every frame (on game time, so it behaves the same in a test as on a real machine).

| Layer | Loop | What it is | When |
| --- | --- | --- | --- |
| `bed_<region>` | 64 s | the region's own music (below) | always, in that region; steps back for the chase, almost silent in a Bell Room |
| `dread` | 48 s | a trembling semitone cluster (D, E♭, E), glass harmonics scraping over it, a Shepard tone rising forever, a heartbeat that is not hers, reversed whispers | rises with the tension (the swell, the Undertow, a boss) and with anything that has seen her, nearer is stronger |
| `chase` | 30 s | fists on the hull in an uneven pattern, strings hacking at D and E♭, steel screaming, brass in a cluster | a hunter within eleven tiles that has seen her, or a boss; it holds four and a half seconds after it loses her, then fades |
| `tide` | 48 s | the drowned singing under the water, in waves; the Saint under the island | grows with the tide in the rooms the sea reaches; faint, through the walls, elsewhere |
| `sanctuary` | 64 s | a warm drone, the lullaby on a cello, the bell still humming | in a Bell Room; nothing else follows her in |
| `title` | 96 s | the sea at night, the lullaby on a music box, then on glass, then the drowned choir and the Saint answering | the title screen |

### The region beds

| Region | Bed |
| --- | --- |
| Wrackline | the sea caves: low strings breathing with the swell every 16 seconds, the hull's timbers groaning (stick-slip friction through wooden modes), the Saint's call rising from under the reef, the lullaby far off on wet glass, a foghorn from the mainland |
| Saltworks | steel bowed by the wind (inharmonic plate partials, semitones rubbing), a piston somewhere still firing out of time as if the engine forgot how to run, low brass, steam hissing, salt in the air |
| Cloister | the brothers chanting the Rule under the arches over a held drone; halfway through, one of them slides off the note and does not stop; reversed whispers; wind in the arcades; drips |
| Choir | the drowned organ playing the hymn's chords with its pipes beating against each other, the hymn sung high and slow, bells under water, bubbles; everything heard through water |
| Ribvault | wind through the giant's ribs whistling, its breath (16-second inhale and exhale), its heart still beating every four seconds, a bone flute, bone creaking |
| Gullet | a sub-bass throb that swallows every six and a half seconds (a rising sweep and a wet gulp), a choir of gurgling throats, glass squealing, squelches |
| Skull | the lighthouse's clockwork still ticking and sometimes missing a tick, its ratchet winding, the wind in the eye sockets, the lullaby on a music box that slows and sinks the second time through, a distant bell |
| Shore | low strings in the storm, a trembling cluster, the Saint far out under the reef, a foghorn |

### Stingers

`seen` (a breath drawn in backwards, then strings and the hull struck together), `scare` (strings shrieking up,
steel slammed, the floor dropping away), `boss` (three blows on the hull, brass and a screaming choir), `death` (the
choir falling away beneath her, bubbles going up, something very large closing), `discover` (a chord blooming out
of glass and voices, unresolved: a monument, the Hollow, a verse learned), `turn` (the Saint drawing breath as the
tide turns toward High).

## The ambience beds

`amb/<region>` (60 s loops) are the rooms themselves, with no music in them: cave drips and far surf, salt hiss and
cooling metal ticking, wind in the arcades, water lapping in a drowned choir, wind gusting through ribs and dust
falling, wet breathing and slime dripping, wind howling round the Skull, and on the shore the storm, rain, surf and
thunder. The old real-time surf that follows the tide still plays under them in the rooms the sea reaches.

## The dread director (`Dread.cs`)

When nothing is hunting her, the dark makes sounds, from out of view (inside the wall beside her, below the floor,
off the edge of the screen), placed in the room and sent through its reverb:

- three knocks on the far side of a wall, a pause, then one more
- nails dragged over stone
- her name, whispered close enough to be inside the helmet ("Maren", or "Mama", and "help")
- wet footsteps passing behind the wall, and stopping
- something heavy dragged somewhere below
- a scream a long way off
- something breathing right behind her, slowly, and then not
- chains (the Saltworks, the Skull)
- a child humming the lullaby, slightly wrong (the Cloister, the Choir, the Skull)
- a wet laugh (the Gullet)
- and once in a long while, after long calm in the dark, **a slam right beside her**, with the `scare` stinger, the
  screen shaking and Maren gasping

With the Saint's Tine grown in her hand it talks to her more, and more again as the sea's hold grows; once the Tine
has rooted, the whisper sometimes says *come down*. While she carries it the drowned choir under the water (the `tide`
layer) follows her into every room, louder as the hold grows.

It paces itself like a horror film: nothing in a fight or in a Bell Room (fear needs quiet), then every 20 to 50
seconds divided by a pressure that grows as the tide rises, when her lamp is shuttered, the longer it has been
calm, and with each act. The big scare needs seventy seconds of calm, is likelier in the dark, and then waits four
to seven minutes.

## The voices of the drowned (`Voices.cs`, `Synth.Voice` and `Synth.Whisper` in `Audio.cs`)

A formant synthesiser: a glottal pulse at a pitch that glides, shaped by four formant bands that morph from one
vowel to another, with breath, a wet gurgle (water in the throat), vocal fry, and now:

- **growl**: period doubling, every other pulse weaker, so a subharmonic an octave down comes through, as if
  something else were using the throat;
- **throats**: several voices in one body, each a little off in pitch and shape;
- **harsh**: the cry torn ragged, clipped in the throat;
- **inhale**: a gasp drawn in before the scream.

Every body has a throat of its own (one of three pitches fixed by who it was), so a room of them is many voices.

| Kind | What you hear |
| --- | --- |
| Shucker | asleep, it mutters, and close enough it mutters her name; awake it moans through water with a growl under it; on seeing her it gasps and shrieks; it snarls before it lunges; a limb torn off is an agonised scream that breaks into sobbing; it dies choking and bubbling, and lets one last breath go |
| Bellmouth | hums the Hours with a second voice a tone below in its throat; bellows; its scream is a cracked bell of four throats |
| Saltwright | breathes through salt that crackles in its lungs; roars; howls when a limb goes, salt cracking |
| Gullet | clicks, and something like laughter; hisses and shrieks |
| Grafted | sings the hymn, with one voice in it on the wrong note; breaks into a shriek of three throats a semitone apart |
| Shoal | five throats moaning together |
| Undertow, Abbot | vast moans and roars, several throats and a subharmonic, with a bell or a sub-bass under them |

Rising, the drowned cough brine out of their lungs before they gasp.

`Synth.Whisper` speaks in whispered phones (vowels, nasals, liquids, hisses and stops) whose formants glide from one
to the next: close enough, in the dark, to be heard as words.

## Maren

- Her weighted diving boots sound of what is under them: stone, metal (the grates and plates), wood (creaking
  boards, timber), water, salt, bone and, in the Gullet, flesh. Landing rattles her corselet and helmet.
- Hit, the blow rings on her brass and she cries out inside the helmet.
- At two pips she breathes hard; at the last pip her heart is louder than the room, and the score narrows to it.
- Out of air under water she chokes; coming up after a long dive she drags air in and coughs the sea out.
- Death: her cry cut short inside the brass, or, drowned, the helmet filling.
- A big scare makes her gasp.

## Effects

- The rivet driver is a punch of compressed air, steel slammed home and the mechanism clacking back; the harpoon a
  prod's thunk and the line whirring out; ricochets spang off stone with a chip of rock.
- Gore (18+): hits on the drowned are wet slaps over something that is not quite bone; a knot breaking is a crack of
  bone and a tearing; a limb coming away is the rip, the bone going splinter by splinter, the skin tearing, the weight
  of it and spatter.
- The menus tick softly, bone on wood.
- The last night's weapons (`Legend.cs`): the Saint's Tine throws with a wet crack and the sea tearing past, and
  under it something very large sings the hymn's lowest note through several throats; empty, it drinks from her with
  a pull of wet breath and her heart answering out of time. The bone growing through the harpoon at the Saint's Ear is
  grinding and cracking, something swallowing, and the hymn's chord swelling in the walls; prising it free is a wet
  tearing and a ringing out of tune. The Burning Glass catches with a thump of air and a high glassy whine, hisses on
  what it burns, and ticks and steams when the lens overheats. The Ear itself hums at the spring tide.

## The mix

| Bus | Carries | Effects |
| --- | --- | --- |
| `World` | effects and voices, placed in the room | the room's reverb (the nave and the sunken choir ring for seconds), and a low-pass when her helmet is under |
| `Music` | the score | a low-pass under water (to 1.1 kHz) and at the last pip (to 2.6 kHz); a compressor keyed from `World`, so a scream or a shot ducks the music |
| `Ambience` | the room beds | a low-pass under water |
| `Sting` | stingers | under water only a little dulled; a stinger also ducks the score while it plays |

The pause menu sets the overall volume and the music volume separately.

## Rebuilding

```sh
python3 tools/score/compose.py                       # every cue -> godot/audio/{score,amb,sting}/*.ogg (about 5 minutes)
python3 tools/score/compose.py score/bed_choir       # one cue
python3 tools/score/compose.py --reel reel.mp3       # and a listening reel of the start of each cue
godot --headless --path godot --import               # then import new files; score and ambience import as looping
```

`--test audio` checks the score follows the room, the hunt, the Bell Rooms, the tide and the title, that the dread
director's sounds exist and that it never acts in a fight; `--dump-sounds dir` writes every synthesised effect to WAV.
