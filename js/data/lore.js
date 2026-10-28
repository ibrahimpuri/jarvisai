// All narrative text: told only through words on the screen, in the manner of Rain World and
// Hollow Knight. Shared with the Godot build (tools/export-data.js writes it out as JSON).
'use strict';

H.LORE = {
  // Shown line by line on a black screen when a new game starts.
  opening: [
    'Forty nights ago, the lighthouse on Hollowmere went dark.',
    'Since then the reef has taken eleven ships.',
    'Nobody on the mainland will row out. They say the abbey has gone quiet, and that the quiet walks.',
    'Your mother kept that light. Her mother kept it before her.',
    'You are Maren Hult. You dive wrecks for a living.',
    'Tonight you are rowing out to light it.',
  ],
  openingAfter: 'Low tide. You have until it turns.',

  // Subtitle under a region's name the first time you enter it.
  epigraphs: {
    wrackline: 'Where the sea leaves what it cannot keep.',
    saltworks: 'Salt is the Saint\'s blood, the Abbot said, and took his share.',
    cloister: 'The brothers walked here in silence. Some of them still do.',
    choir: 'Under water, every hymn becomes a different hymn.',
    ribvault: 'Climb the giant\'s ribs. The wind still remembers its breathing.',
    gullet: 'Below everything, something is still swallowing.',
    skull: 'From the skull you can see the mainland. That was always the trouble.',
  },

  // Words cut into the island. tx/ty are a hint; the game finds the nearest floor.
  carvings: {
    breakers: [{ tx: 27, ty: 12, title: 'Cut beside an old mooring ring', text: 'In a hand you know: IF YOU ARE READING THIS, THE LIGHT IS OUT. GO UP, AND LIGHT IT. DO NOT WAIT FOR ME. - A.H.' }],
    reef: [{ tx: 16, ty: 12, title: 'Cut fresh into the Constance\'s rail', text: 'No light on the skull. Struck at the turn. Ennis, Bray and Coll went over the side. I told the rest the water would bring them back. It did.' }],
    pier: [{ tx: 30, ty: 16, title: 'A tide post, painted in rings', text: 'Beside the highest ring: THE SAINT BREATHES IN. THE SAINT BREATHES OUT. COUNT YOUR STEPS BY IT.' }],
    seawall: [{ tx: 15, ty: 16, title: 'A numbered stone in the seawall', text: 'Laid in the fourth year of the Keeping by the first Hult, who asked only that no one ever be buried below the tide line.' }],
    chapel: [{ tx: 6, ty: 16, title: 'Painted above the chapel door', text: 'WHILE IT RINGS, THEY CANNOT COME. RING IT FOR THE LIVING, BROTHER, AND FOR NO ONE ELSE.' }],
    tithe: [{ tx: 36, ty: 16, title: 'The tithe, cut into the floor by the door', text: 'Of every ten baskets of salt, one to the Saint. Of every ten brothers, one.' }],
    tithe_gallery: [{ tx: 25, ty: 16, title: 'Scratched into the glass with a fingernail', text: 'it goes round the island at high water like a bell looking for its tower' }],
    saltgate: [{ tx: 10, ty: 15, title: 'A pan-master\'s slate', text: 'The salt in Pan Three grew back overnight again. Do not mention this to the mainland buyers. Do not touch it with bare hands.' }],
    saltbell: [{ tx: 33, ty: 18, title: 'Chalk tallies on the wall', text: 'Thirty-one marks. Beside the ninth: T. went down to the Cloister. Every mark after the ninth has been gone over twice, very hard.' }],
    kilns: [{ tx: 30, ty: 18, title: 'On a kiln door, in the Abbot\'s hand', text: 'Feed the fire and the fire feeds us. The Saint gives without end. Beneath it, smaller and shakier: it is in my hands now. it does not hurt. that is the worst of it.' }],
    abbot: [{ tx: 10, ty: 17, title: 'A floor mosaic, cracked by something heavy', text: 'The Mere-Saint, drawn as a whale with too many ribs, swallowing a ship whole. Every monk in the picture is smiling.' }],
    brineshaft: [{ tx: 38, ty: 18, title: 'A brass plate by the drop', text: 'BRINE SHAFT. DO NOT DESCEND WITHOUT WEIGHT. DO NOT ASCEND WITH IT.' }],
    cloister: [{ tx: 6, ty: 16, title: 'The Rule, carved into every arch', text: 'Walk. Do not speak. Do not look into the water. If the water looks back, walk faster.' }],
    cloisterbell: [{ tx: 22, ty: 16, title: 'Names scratched into the bell frame', text: 'Thirty names, every one struck through but the last. The last name is Ansel.' }],
    nave: [{ tx: 30, ty: 16, title: 'A brass plaque on a pew', text: 'Here sat the Bell-Ropers, who rang the Hours for the fishing fleet. The last of them rang the warning a full tide too late, and the sea took him still holding the rope.' }],
    priors: [{ tx: 26, ty: 16, title: 'Cut beside the Prior\'s door', text: 'I keep this door for the Ebb, when the sea turns its back. Even the Saint must breathe out.' }],
    choirsteps: [{ tx: 30, ty: 16, title: 'A hymn, one line on each step', text: 'Sing to the bone and the bone will answer. Sing to the salt and the salt will rise. Sing to the sea and the sea will take you home, home, under the skies.' }],
    choir: [
      { tx: 23, ty: 16, title: 'Above the font', text: 'Take of the Saint and the Saint takes of you. This is not a curse. This is communion. - H.V.' },
      { tx: 9, ty: 16, title: 'Carved around the rim of an empty niche', text: 'HERE THE HOLLOW LAY, AND HEARD US, AND THE SEA CAME WHEN WE SANG. SING SOFTLY, BROTHER. IT HEARS EVERYTHING.' },
    ],
    choirbell: [{ tx: 30, ty: 16, title: 'Scratched low on the wall, at a child\'s height', text: 'A lighthouse, a little boat, two figures holding hands. Underneath, in careful capitals: MAREN AND MAMA.' }],
    ribshaft: [{ tx: 20, ty: 41, title: 'Tally marks cut into the bone', text: 'Years of climbers counted in notches. One mark is bigger than the rest: AGATHE, AGE NINE, ALL THE WAY TO THE TOP.' }],
    ribbell: [{ tx: 26, ty: 16, title: 'Burned into the lintel', text: 'The light is for ships. The light is not for us. Remember who it is for.' }],
    prisms: [{ tx: 14, ty: 36, title: 'A teaching plate', text: 'For novice Keepers. Bend the beam, and learn that light obeys whoever holds the glass.' }],
    gulletmouth: [{ tx: 8, ty: 18, title: 'Barely legible on a drowned stone', text: 'The Saint was not dead when we came. It is not dead now.' }],
    lair: [{ tx: 30, ty: 18, title: 'Rope burns worn into the rock', text: 'A bell-ringer\'s count, pressed into stone over and over. One, two, three. It never reaches four.' }],
    skullstair: [{ tx: 10, ty: 16, title: 'Cut into the first step', text: 'Two hundred and twelve steps. My mother counted them for me. I count them for my daughter. - A.H.' }],
    lamp: [{ tx: 5, ty: 16, title: 'The lamp\'s brass plate', text: 'LUMEN CONTRA SALEM. Light against the salt.' }],
  },

  // What the drowned are still thinking, read by holding the focused beam on a body.
  whispers: {
    corpse: [
      '...I counted the bells... I lost count...',
      '...cold. so cold. and then not cold at all...',
      '...tell the mainland. tell them not to come...',
      '...the water was singing. I only wanted to hear the end...',
      '...my hands. look at my hands...',
      '...Brother? I can\'t see the lamp...',
      '...we only took a little salt...',
      '...it is quiet now. it is good to be quiet...',
    ],
    shucker: [
      '...knot... and knot... and pull...',
      '...the tide will lift me soon...',
      '...walk. walk. do not speak...',
      '...the Saint is so hungry...',
      '...I can hear your lamp...',
    ],
    bellmouth: ['...one more verse. just one more verse...', '...the Hours. who rings the Hours now...'],
    saltwright: ['...dig. the vein goes down forever...', '...the Abbot said we would be rich...'],
    gullet: ['...hang. listen. fall...'],
    grafted: ['...the hymn is nearly finished...', '...it doesn\'t hurt. it never hurts...'],
    named: {
      sailor: '...skipper? the light\'s out... the light\'s out...',
      first_shucker: '...not yet. the water isn\'t here yet...',
      teodor: '...Odile... give her my tags... tell her I\'m sorry about the boat...',
      'corpse@29,16': '...let me be useful. let me be the weight...',
      'corpse@32,16': '...down. I want to go down...',
    },
  },

  // Maren's few thoughts, shown once each as a quiet line at the bottom of the screen.
  thoughts: {
    boat: 'The boat is holed through. Well. I was never planning to row back.',
    timber: 'An old hull, stood on its end. Wet stone won\'t take a grip. Timber will.',
    salt: 'Salt, grown clean across the way through. Mama always said it cracks in the heat.',
    crystals: 'Salt crystals. They would open a boot like a knife. Best not to linger on them.',
    start: 'The Constance is still in one piece. More or less.',
    torso: 'Sparks. The flesh is only weight. Something else is holding it up.',
    crown: 'The knot in its chest is bare now. Close enough for the gaff.',
    rising: 'The tide has turned. Anything I leave lying in the water won\'t stay lying.',
    sailorKnots: 'Knots at his knees and elbows, going dark. Just a corpse, then. It\'s the bright ones I should fear.',
    bellmouth: 'It turns toward my lamp, the way a flower turns to the sun.',
    lowOil: 'The lamp is guttering. I need oil.',
    rings: 'Iron rings, set in the rock. The old riggers swung from these.',
    pier: 'No light on the skull. Forty nights of that.',
    firstStand: 'It stood up. The water stood it up.',
    firstKnot: 'They break at the joints. Like rigging.',
    firstDown: 'Haul it or burn it. The chalk said so.',
    chapel: 'A bell. Someone is still ringing it.',
    firstDeath: 'The water let me go. It won\'t always.',
    titheTrap: 'Up. Up, before the water.',
    glimpse: 'What was that.',
    firstHigh: 'Everything I left on the floor is under the water now.',
    actTwo: 'His boots are heavy. Good. Heavy things sink.',
    actThree: 'The spring tide is coming. I can feel it in the walls.',
    firstGraft: 'It doesn\'t hurt. That\'s what frightens me.',
    choir: 'They\'re singing. Under the water, they\'re singing.',
    skull: 'Two hundred and twelve steps.',
    agathe: 'Mama.',
    firstSever: 'The knot let go, and the arm went with it. There was nothing else holding it on.',
    saltLimb: 'It grew the arm back. Out of salt.',
    giftWave: 'The sea came all the way up the pier to leave this at my feet. Then it slid back down, as if it were shy.',
    firstSing: 'It hums when I sing to it. Somewhere under the island, something hums back.',
    singWake: 'Everything down here heard that.',
    calledWater: 'The water I called is the same water. It will stand them up at High like any tide.',
    turnTide: 'I held the note, and the Saint breathed. Mama would hate this.',
    learn_riptide: 'The Hollow sang back to the thing outside the glass. It kept a little of what it heard.',
    learn_salting: 'The salt in the pans is humming the same note as the box.',
    learn_answer: 'Every brother in the cloister turned his head as I passed. All of them, together.',
    // the last night: the legendary weapons, and what the Tine does to the one who carries it (Legend.cs)
    hollowQuiet: 'The Hollow takes the brine of the ones I break. The sea on its own only fills it halfway.',
    socketDry: 'A hollow in the bone, the shape of the box in my hand. Someone carved round it a long time ago.',
    tineFirst: 'It went through all of them. It didn\'t slow down. It wanted to keep going.',
    tineDrinks: 'It was empty, so it drank from me. It did not ask.',
    glassFirst: 'Mama\'s glass. It burns what the lamp burns. I can smell them cooking.',
    glassBurnsMe: 'The light found the salt in my hand. It hurts. Good.',
    host1: 'The Tine is warm when I wake. I don\'t remember picking it up.',
    host2: 'It has grown into the stock. Into my glove. I can\'t see where the bone stops.',
    host3: 'They don\'t all turn to look at me any more. Some of them bow their heads.',
    host4: 'I can hear it. Under the island. It is so old, and so tired, and it has been waiting for someone to hear it.',
    // monuments: what she thinks when she stops beside one (StoryFocus)
    mon_keeper: 'The first Hult, holding her lamp up at the reef. Someone has tied red cloth round her wrist. Mama did that, every spring.',
    mon_votives: 'A little boat nailed up for every ship the reef took. Eleven of them are new.',
    mon_scales: 'One pan for the salt. The other is worn smooth, in the shape of a man kneeling.',
    mon_abbot: 'The Abbot, holding out his hand. The salt has grown out of his palm like another hand, reaching back for him.',
    mon_weeper: 'Hands over his eyes. Do not look into the water. He kept the Rule longer than anyone.',
    mon_roper: 'He never let go of the rope. Not when the sea came in. Not after.',
    mon_bell: 'The great bell came down through the floor. It must have rung once more when it landed.',
    mon_mitten: 'A child\'s red mitten, tied to the highest peg she could reach. Agathe, age nine. Mama.',
    mon_swallowed: 'A keel, sticking out of the wall like a fishbone in a throat. It\'s still going down.',
    mon_lens: 'The old lens, cracked clean through. Light against the salt. The salt won, for a while.',
  },

  // Hints: a glyph and a word or two over Maren's head. Each appears only when it is relevant and the
  // player has stalled for a while, and never again once the action has been done, prompted or not.
  // {action} tokens become the button for whatever the player is holding.
  hints: {
    move: '{move} walk',
    jump: '{jump} jump',
    swim: '{move} swim     {jump} leap out at the surface',
    ladder: '{up} or {jump} climb',
    walljump: '{walljump} cling to timber and leap',
    drop: '{drop} drop through',
    shoot: '{aim} aim     {fire} fire',
    dash: '{dash} dash',
    heal: '{poultice} poultice',
    focus: '{focus} focus the lantern',
    swing: '{swing} swing',
    fight: '{fire} shoot the joints',
    gaff: '{gaff} gaff',
    reload: '{reload} reload',
    craft: '{quickcraft} rivets from scrap',
    map: '{map} map',
    weapon: '{weapon} change weapon',
    boots: '{boots} Ballast Boots',
    jet: '{dash} in water or mid-air',
    shutter: '{shutter} shutter the lantern',
    sneak: '{sneak} creep',
    sing: '{sing} sing',
    hollow: '{hollow} the Hollow',
    verse: '{verse} change verse',
  },

  // A dream while resting at a Bell Room, one each time, in order.
  dreams: [
    'You dream of the lamp room. You are small, and your mother lifts you up to watch the beam swing out over the water. "It\'s for the ships," she says. "Not for us. Never for us."',
    'You dream of salt. It grows on the rim of a cup left out overnight, in perfect white branches. In the morning there is more of it.',
    'You dream of bells. Hundreds of them, under the sea, all ringing at once, all a little out of time with each other.',
    'You dream that the island is breathing. In, at the Rising. Out, at the Ebb. You are something small inside a ribcage.',
    'You dream of a man holding a bell rope, waiting for the right moment to pull. The moment passes. He keeps waiting.',
    'You dream you are a bone at the bottom of the sea, listening. Far above, a small light goes out, and you are sorry about it. So you start to sing.',
    'You dream of the mainland, lit up and warm. Nobody there is thinking about you. That is all right. That is the point.',
    'You dream of your mother\'s handwriting. Every page says the same thing, and you cannot read any of it.',
    'You dream of a choir singing under the sea. The song is very beautiful, and it is about you.',
    'You do not dream. You sleep the way the drowned sleep, and wake when the bell tells you to.',
  ],

  death: {
    titles: ['The water takes you', 'Salt in the lungs', 'The tide keeps you a while', 'Under', 'The bell, far away'],
    lines: [
      'Somewhere above, a bell is ringing. You follow it back.',
      'The sea holds you for a while, then loses interest.',
      'For a moment you are part of the island. Then you are not.',
      'You hear the Saint breathe in. You wake before it breathes out.',
    ],
  },

  // Extra things the islanders say once their main business is done, in rotation.
  npcLore: {
    ansel: [
      'The monks came for the salt and stayed for the Saint. That was four hundred years ago. The salt was dead then. We are almost sure the salt was dead then.',
      'The Bell-Ropers rang the Hours for the fishing fleet. The last of them rang the warning too late, and the sea took him holding the rope. Some nights you can hear him still trying to get it right.',
      'You came here once before, you know. You were four. You climbed into the bell frame and would not come down until your mother promised you a ride on the lamp.',
      'I ring the bell every hour. If I stop, the crust reaches my heart. So I do not stop. It is a simple rule. Most good rules are.',
    ],
    odile: [
      'Teodor and I salvaged the Morrow wreck together, the whole of one winter. He could hold his breath for four minutes. He used to say the sea was the only place quiet enough to think.',
      'Mere-ivory is bone from the Saint. It is harder than steel and it hums when you work it. I try not to think about what it is humming.',
      'If I had a boat I would be gone. I do not have a boat. I have a bench and a bell and a very good hammer.',
    ],
    hesper: [
      'The Abbot thought the Saint was a mine. The Prior thought it was a clock. Your mother thinks it is a threat. Only the Choir ever thought to ask what it wanted.',
      'The hymn has seven verses. We know six. The seventh is the one the Saint sings back.',
      'Your mother and I were novices together. She was braver than me. I was more curious. Look where it got us both.',
    ],
  },

  // The Mere's Hollow: a reliquary box of Mere-ivory holding the Saint's ear-bone, which the sea gives Maren on
  // the Long Pier. Whale ear-bones are the densest bone there is and the last part of a whale to wash ashore;
  // the first brothers found this one on the strand and named the island for it. Its verses are lines of the
  // Choir's hymn, carved on the Choir Steps. (Godot build only.)
  hollow: {
    name: "The Mere's Hollow",
    reveal: 'A reliquary box of Mere-ivory, no bigger than a hymnal, its lid carved with a whale that has too many ribs. Inside, on salt-white velvet, lies a curled bone like a seashell, heavier than lead and faintly warm. Brass letters run around the lid: AUDIT ET RESPONDET. It hears, and it answers.\n\nHold it to your helmet and you can hear the tide breathing.',
    verses: {
      calling: { desc: 'Take it in hand ({hollow}) and sing ({sing}) toward where you want the water to be. The sea in this room rises or falls to meet the mark, holds a while, then settles back. Only the tide answers; still water does not hear it.\n\nWhen the Hollow is brimming, hold the note ({singhold}) and the Saint breathes early. The tide turns.' },
      riptide: { desc: 'Sing it ({sing}) and a rush of the sea throws back whatever stands before you, puts out fire, and tears loose any hand that has hold of you. Under water it carries you the way you point.' },
      salting: { desc: 'Sing it ({sing}) at the drowned and salt crusts them where they stand, so the next blow shatters a limb. Sing it at stone and a pillar of salt grows out of it for a while. Fire still cracks it.' },
      answer: { desc: 'Sing it ({sing}) at one of the drowned, standing or fallen. It turns on its own kind, and sings while it does, until the verse runs out and its knots let go for good.' },
    },
    // what each islander says the first time they see Maren carrying it
    npc: {
      ansel: 'Is that... no. Keep it, keep it. The first brothers found that bone on the strand, curled like a shell and humming, and named the island for it: the Mere\'s Hollow. Then they dug, and found the rest of the Saint underneath it, and stopped naming things.',
      odile: 'That\'s an ear-bone you\'re carrying. Whale ears wash up when everything else has rotted away; they\'re the densest bone there is. I\'ve found little ones on the mainland strand. Never one that size. Never one that was warm.',
      hesper: 'The Choir sang into the Hollow every Hour for three hundred years, and the tide came when it was called. When the Abbot wanted it for his pans, I rowed it out past the reef and gave it back to the sea. Now the sea has given it to you. I would very much like to know why.',
    },
  },

  // The legendary weapons of the last night, Act III only (Godot build only; see Legend.cs). The Abbot did not want
  // the Hollow for his pans: he meant to lay it in the Saint's Ear and be grown into what the Saint is, the thing the
  // sea dreams through. Hesper rowed it past the reef so that no one could. The Saint has been dying for three
  // hundred years, and a sea that size needs a host.
  legend: {
    socket: {
      name: "The Saint's Ear",
      dry: 'A hollow in the bone of the vault, curled like a shell and exactly the size of the reliquary in your hand. Letters are cut all round its lip, older than the Abbey: QUOD AURIS AUDIT, AURIS FIT. What the ear hears, the ear becomes.\n\nIt is dry. It is waiting for something.',
      wet: 'The hollow in the bone is running with brine, though nothing drips into it, and the Hollow hums against your breastplate so hard your teeth ache. It wants to go back in. It wants something in its hand when it does.\n\nScratched into the bone beside it, newer than the letters, in a hand you have seen on the Abbot\'s ledgers: HERE. AT THE SPRING TIDE. IT DOES NOT HURT.',
      ask: 'Lay the Hollow in the Saint\'s Ear, with the Harpoon Gun across it? The bone will grow the two together.',
      noHollow: 'The Ear is the shape of something you are not carrying.',
      noHarpoon: 'The Ear wants something in its hand. The Harpoon Gun would do.',
      merged: 'The bone takes the harpoon the way a shell takes a grain of sand. When you lift it out, the Hollow has grown through the stock and the prod and the line, ribbed and pale and warm, and it is not a harpoon any more.',
      already: 'The Ear is empty again. What it held is in your hand, and it is growing.',
    },
    tine: {
      name: "The Saint's Tine",
      desc: 'Throw it ({fire}) and a lance of the sea itself runs through everything in its way, breaking a knot in each. Each throw drinks a quarter of the Hollow; the brine of the knots it breaks fills it again, and so does the sea, to the brim. Empty, it drinks from you.\n\nWhile it is grown together the Hollow sings no verses, and the bells hurt it. A bench can prise it free, while it still comes free.',
    },
    unmerge: {
      ask: 'Prise the Hollow out of the Tine? The Harpoon Gun comes back as it was. The Hollow comes back empty.',
      rooted: 'Prise the Hollow out of the Tine? It has rooted: it will tear you coming out.',
      done: 'You work the bone loose from the stock with Ansel\'s pry, a hair at a time. The Hollow comes away quiet and cold.',
      torn: 'It comes out of the stock, and out of the glove, and out of the palm of your hand. It takes some of you with it.',
    },
    glass: {
      name: 'The Burning Glass',
      desc: 'Agathe\'s spare lamp lens: a disc of Mere-ivory ground as thin as a fingernail, in a brass hood that screws onto your lantern. Hold {fire} and your lamp burns in a line, like the great lamp: the beam cracks every knot it touches and burns the fallen so they never stand again. It drinks your oil quickly, and three seconds of it makes the lens too hot to hold to.',
      note: 'A label is tied to the hood in your mother\'s hand: “For Maren, if she comes. It burns what the lamp burns. Count who is still alive. A.H.”',
    },
    npc: {
      hesper: 'You put it in the Ear. Oh, Maren. That is what the Abbot wanted it for. Not his pans: he wanted to be what it grows into. The Saint is dying, and it has been dying since the brothers came, and a sea that size needs something to dream it. I rowed the Hollow out past the reef so that nobody could ever be that. Take it out while it still comes out.',
      ansel: 'What is that in your hand? It was the Hollow. It was a harpoon. Child, put it down. Please, put it down and let me look at your glove.',
      odile: 'That thing\'s breathing. Your harpoon is breathing. I am not going to say anything else about it.',
    },
    agathe: 'Maren. What is in your hand? No. Look at me, not at the window. The sea is up to the sill tonight, and the tables say it cannot be. It is waiting for you. If you go down to it, there will be no lamp, and no ferry, and no daughter. Only the sea, with your face.',
    choice: 'There is a fourth thing, and only you can do it now. Go to the window. The sea is waiting under it.',
  },

  // The Mere-Saint crossing, earned by finishing the night once: the Saint is awake under the island. Agathe hid the
  // seventh verse of the Choir's hymn in her log, one line to a page, as the lullaby she sang her daughter, so that it
  // would never be lost and the Choir would never have it whole. (Godot build only; see Difficulty.cs and Story.cs.)
  saint: {
    opening: 'Tonight the Saint is awake under the island. It has been waiting a long time for a Hult to hear it.',
    margin: 'In the margin, smaller, in pencil, a line of the song she used to sing you:',
    lullaby: [
      'Hush now, my small one, the tide is a door,',
      'and the Saint is a hill with a heart in its floor.',
      'It breathes with the Rising, it breathes with the Ebb,',
      'and it sings as it sleeps in its cold salt bed.',
      'It has sung for so long that it can\'t find the end,',
      'so hush, and we\'ll sing it the end, little friend.',
      'Then the bells can lie down, and the drowned can lie still,',
      'and the lamp can burn kind on the top of the hill.',
      'Hush now, my Maren. The Saint wants to sleep.',
    ],
    agatheHint: 'And read the margins of my pages, if you find them. Not the log. The margins.',
    agathe: [
      'You found every page. Then you have the song as well, the one in the margins.',
      'Maren, that is not a lullaby. I made it one, so that it would never be lost, and so that the Choir would never have it whole. It is the seventh verse: the one the Saint sings back, and has never been able to finish.',
      'The Saint is not angry. It is dying, and it is frightened, and it has been singing for three hundred years trying to reach the end. The Brine is only its song, getting into everything.',
      'There is one more thing a Keeper could do tonight, and only a Hult can do it. Wind my music box, and sing it the end.',
    ],
    box: 'Agathe\'s music box',
    noHollow: 'The music box plays the tune, thin and sweet, and nothing under the island hears it. A song needs something to carry it to the Saint.',
    ask: 'Wind the music box and sing the seventh verse? This ends the night.',
    title: 'The Seventh Verse',
    ending: [
      'You do not light the lamp, or ring the bell, or fire the beacon. You wind your mother\'s music box, lift the Hollow to the open window, and sing the song she sang you every night until you were seven.',
      'Agathe knows it by the second line. She sings the third with you, and her voice is still the one you remember.',
      'Under the island the Saint stops singing. For the first time in three hundred years, it is listening.',
      'It has been trying to reach the end of its song since before the brothers came. The Brine was only the song, getting into everything: the salt, the drowned, the bells. You give it the end.',
      'The ribs settle. The Skull lowers itself onto its bones as gently as a head onto a pillow. In every flooded room on Hollowmere the knots go dark one by one, and the drowned lie down in the water, and do not get up, and are not sorry.',
    ],
    grafted: 'The salt falls out of your skin like frost off a window, and out of Agathe\'s, and far below, out of everyone the Choir ever grafted.',
    clean: 'The salt falls from Agathe\'s hands like frost off a window. She looks at them for a long time.',
    dawn: 'At dawn you light the lamp together. There is nothing left for it to burn. It is only a light, and it is for the ships.',
    // the glimpse of what comes after, shown over the night sea once the epitaph is read
    glimpse: [
      'Nine nights later.',
      'The sea off Hollowmere is flat and grey and ordinary. Maren is cleaning the great lens when the beam, swinging out past the reef, finds a light where there is no land.',
      'It is answering hers. Three short, one long: a Keeper\'s signal, from the open sea.',
      'In the Prior\'s cell, under a stone the Choir never lifted, she finds a fifth tide table. It is not a table. It is a chart of the whole sea, and on it, in the Prior\'s hand, are eleven more islands, each drawn as a whale with too many ribs.',
      'Beside the last of them, out where the chart runs off the edge, he has written: THE MOTHER. SHE HAS NOT STOPPED SINGING.',
    ],
    sequel: { name: 'HOLLOWMERE', part: 'PART II', title: 'THE DEEP HOURS', tag: 'The light is for the ships. The song is for something else.' },
  },

  // Epilogue lines shown one by one after the ending, chosen by what happened.
  fates: {
    ansel: { alive: 'Brother Ansel rang the chapel bell every hour until the end of his life, and it was a long one.', burned: 'Brother Ansel was ringing his bell when the beam found him. The bell went on swinging for a while after.', sold: 'The chapel bell never rang again. Nobody found Brother Ansel.', verse: 'The crust fell from Brother Ansel\'s chest the morning after. He rang the chapel bell once more, for no reason at all, and never had to again.', sunk: 'Brother Ansel rang the chapel bell as the island went under, until the water reached the rope.' , host: 'Brother Ansel rang the chapel bell until the sea came in under the door. Then he set down the rope, and went out to meet it.' },
    odile: { verse: 'Odile Marrow rowed the first boat to the mainland in forty-one nights, and the second, and the tenth, and the island\'s dead came home in them to be buried above the tide line.', alive: 'Odile Marrow built a boat out of salvage and a stubborn temper, and was the first to row back to the mainland in forty nights.', lost: 'Odile Marrow\'s bench stands in the Saltworks, tools still laid out, waiting for someone who does not come.' , host: 'Odile Marrow rowed for the mainland alone, and made it, and told them. They thought the island had driven her mad. Then the ships started coming in on the reef.' },
    teodor: { saved: 'Teodor woke up on the ferry, knotless and very confused, with Odile holding his hand hard enough to hurt.', dead: 'Teodor\'s tags hang above Odile\'s bench, where the light catches them.' },
    hesper: { verse: 'Hesper Vane heard the seventh verse sung at last. She wept, and said it was not how she had imagined it, and that it was better.', alive: 'Hesper Vane sang the last verse alone on the shore, and nobody could say afterwards whether the sea had answered.', burned: 'Hesper Vane walked into the beam with her arms open. She did not look afraid.', dead: 'The Choir buried Hesper Vane in the drained shrine, and sang over her with no water to carry the song.', sunk: 'Hesper Vane went down with the island, singing the seventh verse at last.' , host: 'Hesper Vane knelt on the Choir Steps as the new Saint went down past her into the deep, and wept, and was the first to sing for it.' },
    agathe: { verse: 'Agathe Hult gave the lamp to her daughter, taught her every step of all two hundred and twelve, and slept at night.', dark: 'Agathe Hult rowed the ferry herself, and did not once look back at the dark lamp.', relight: 'Agathe Hult kept the lamp beside her daughter, and taught her every step of all two hundred and twelve.', hymn: 'Agathe Hult watched from the rope ferry until there was nothing left to watch.' , host: 'Agathe Hult was the first to drown for the new Saint. Her daughter called her by name, and she came.' },
    maren: { verse: 'Maren Hult kept the lamp of Hollowmere. Ships pass the reef safely now, and she sleeps, mostly. Some nights she hears something very far out at sea, singing the first verse.', dark: 'Maren Hult never kept a light. She dove wrecks for a living, and every one she found had a name she remembered.', relight: 'Maren Hult became the Keeper of Hollowmere. Ships pass the reef safely now. She does not sleep well.', relightGrafted: 'The beam found the salt in Maren Hult too. The Keepers\' log records her name, and that she lit the lamp first.', hymn: 'Maren Hult is part of the hymn now. Fishermen say that on still nights you can hear it under the reef.' , host: 'There is no Maren Hult. There is the sea off Hollowmere, very deep and very patient, and on still nights it says the names of ships before they wreck.' },
  },
};
