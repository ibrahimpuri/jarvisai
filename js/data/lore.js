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
    choir: [{ tx: 23, ty: 16, title: 'Above the font', text: 'Take of the Saint and the Saint takes of you. This is not a curse. This is communion. - H.V.' }],
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
  },

  // Hints: a glyph and a word or two over Maren's head. Each appears only when it is relevant and the
  // player has stalled for a while, and never again once the action has been done, prompted or not.
  // {action} tokens become the button for whatever the player is holding.
  hints: {
    move: '{move} walk',
    jump: '{jump} jump',
    swim: '{move} swim     {jump} leap out at the surface',
    ladder: '{up} climb',
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
  },

  // A dream while resting at a Bell Room, one each time, in order.
  dreams: [
    'You dream of the lamp room. You are small, and your mother lifts you up to watch the beam swing out over the water. "It\'s for the ships," she says. "Not for us. Never for us."',
    'You dream of salt. It grows on the rim of a cup left out overnight, in perfect white branches. In the morning there is more of it.',
    'You dream of bells. Hundreds of them, under the sea, all ringing at once, all a little out of time with each other.',
    'You dream that the island is breathing. In, at the Rising. Out, at the Ebb. You are something small inside a ribcage.',
    'You dream of a man holding a bell rope, waiting for the right moment to pull. The moment passes. He keeps waiting.',
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

  // Epilogue lines shown one by one after the ending, chosen by what happened.
  fates: {
    ansel: { alive: 'Brother Ansel rang the chapel bell every hour until the end of his life, and it was a long one.', burned: 'Brother Ansel was ringing his bell when the beam found him. The bell went on swinging for a while after.', sold: 'The chapel bell never rang again. Nobody found Brother Ansel.', sunk: 'Brother Ansel rang the chapel bell as the island went under, until the water reached the rope.' },
    odile: { alive: 'Odile Marrow built a boat out of salvage and a stubborn temper, and was the first to row back to the mainland in forty nights.', lost: 'Odile Marrow\'s bench stands in the Saltworks, tools still laid out, waiting for someone who does not come.' },
    teodor: { saved: 'Teodor woke up on the ferry, knotless and very confused, with Odile holding his hand hard enough to hurt.', dead: 'Teodor\'s tags hang above Odile\'s bench, where the light catches them.' },
    hesper: { alive: 'Hesper Vane sang the last verse alone on the shore, and nobody could say afterwards whether the sea had answered.', burned: 'Hesper Vane walked into the beam with her arms open. She did not look afraid.', dead: 'The Choir buried Hesper Vane in the drained shrine, and sang over her with no water to carry the song.', sunk: 'Hesper Vane went down with the island, singing the seventh verse at last.' },
    agathe: { dark: 'Agathe Hult rowed the ferry herself, and did not once look back at the dark lamp.', relight: 'Agathe Hult kept the lamp beside her daughter, and taught her every step of all two hundred and twelve.', hymn: 'Agathe Hult watched from the rope ferry until there was nothing left to watch.' },
    maren: { dark: 'Maren Hult never kept a light. She dove wrecks for a living, and every one she found had a name she remembered.', relight: 'Maren Hult became the Keeper of Hollowmere. Ships pass the reef safely now. She does not sleep well.', relightGrafted: 'The beam found the salt in Maren Hult too. The Keepers\' log records her name, and that she lit the lamp first.', hymn: 'Maren Hult is part of the hymn now. Fishermen say that on still nights you can hear it under the reef.' },
  },
};
