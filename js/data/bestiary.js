// The drowned, kind by kind: the journal's bestiary (filled in as Maren sees each one), and what she thinks the first
// time she sees the newer kinds. Tiers: start (Act I), mid (Act II), end (Act III). See godot/docs/ENEMIES.md.
'use strict';

H.LORE.bestiary = {
  // ---- the start: the Wrackline and the Saltworks
  shucker: { name: 'Shucker', tier: 'start', text: 'A drowned monk. It shambles toward any sound and lunges to grab within two metres. Knots at both knees and both elbows; the crown in its chest is sealed until two of them are broken. Break the knees and it crawls, low and fast, but it cannot climb. Break the elbows and it cannot grab. Break the grabbing arm mid-grab and it lets go at once.' },
  gullet: { name: 'Gullet', tier: 'start', text: 'It hangs from the ceiling on a crystal tendril and drops on any noise beneath it. Shoot the tendril from range and it lands stunned for three seconds. Then there are three spine knots to break.' },
  gulletpair: { name: 'Gullet Pair', tier: 'start', text: 'A Gullet hung from two tendrils. Cut one and it swings from the other, hard, through anyone under it. Only cutting both brings it down.' },
  bellmouth: { name: 'Bellmouth', tier: 'start', text: 'A bloated chorister. Light makes it inhale for a breath and a half, and then it screams, and every sleeping body in the room wakes. Break its jaw during the inhale, or shutter the lamp before it sees you.' },
  hooded: { name: 'Hooded Bellmouth', tier: 'start', text: 'A Bellmouth with a page of the hymnal tied over its eyes. The lamp means nothing to it, so shuttering will not help: it screams at noise. Walk softly past it, or break its jaw before the breath is in.' },
  saltgull: { name: 'Saltgull', tier: 'start', text: 'Gulls drowned in the Brine and come back crusted white. They roost on rock and rigging and lift together at a noise, a lamp turned on them, or anyone walking underneath, then come down in long swoops. One knot, at the wing: any hit brings a bird down. There are always several.' },
  netter: { name: 'Netter', tier: 'start', text: 'A Hithe-wife drowned in her own nets. She throws them from a few strides off: a net holds Maren until she tears it (mash jump), cuts it (the gaff) or it burns. Then she comes in with the gutting knife. Break her right elbow, the throwing arm, and there are no more nets.' },
  kilnman: { name: 'Kilnman', tier: 'start', text: 'A stoker of the kilns. Fire does not hurt him, and his shovel throws embers that catch on timber, rigging and oil. Get him into water and the heat goes out of him: every knot turns brittle, and rivets break them twice as fast.' },
  // ---- the middle: the Lower Cloister, the Sunken Choir and the Ribvault
  saltwright: { name: 'Saltwright', tier: 'mid', text: 'An armoured salt-miner whose pick breaks walls and floors. Salt plates cover both arm knots. In the second and a half after it swings, pry a plate off with the gaff, then shoot what was under it.' },
  driftling: { name: 'Driftling', tier: 'mid', text: 'Fist-sized swimmers in swarms of six to ten, only in water at High tide, drawn to light. One knot each. Shutter the lamp, throw a flare to lure them off, or dash through them.' },
  braced: { name: 'Braced Shucker', tier: 'mid', text: 'The Brine learns. The ones that come back after the first night grow salt plates over the knot she breaks most, and always leave two others open. Change how you fight.' },
  bloater: { name: 'Bloater', tier: 'mid', text: 'The drowned that floated. At High tide it lifts off the floor and drifts to her through the water, belly first. Its crown is in the belly: break it and the belly bursts, and a swarm of Driftlings with it. Burn it and nothing comes out. On a dry floor it can only drag itself.' },
  toller: { name: 'Toller', tier: 'mid', text: 'A bell-roper who still carries his handbell. When he sees her he rings it, and every body in the room that can still stand gets up and comes to the sound. Break his right elbow, the bell arm, first, or stay out of his sight.' },
  cantor: { name: 'Cantor', tier: 'mid', text: 'A singer of the Choir. Its song stands downed bodies back up, knots and all. Break its jaw, or burn or haul away what you have put down before it can sing to it. It keeps its distance, and it is not brave.' },
  ossuary: { name: 'Ossuary', tier: 'mid', text: 'A heap of bones that stands up when she comes near and knots itself together. While it is assembling its knots are bare and break twice as fast. Afterwards its arms reach twice as far as anything else. Break both wrists and it has nothing to hold her with.' },
  kite: { name: 'Kite', tier: 'mid', text: 'A drowned brother whose cloak the wind took. It rides the gusts above her and drops on her out of the dark. Break a shoulder and the cloak folds: it falls, and has to crawl.' },
  // ---- the end: the Gullet and the Lantern Skull
  grafted: { name: 'Grafted', tier: 'end', text: 'Living members of the Choir with Brine grafts of their own. They carry lamps and harpoons, shut doors, and go back into the water to heal. Fight them one at a time, and keep them from the water.' },
  diver: { name: 'Drowned Diver', tier: 'end', text: 'A salvage diver in brass and canvas, like her, come for the abbey\'s salt and kept by it. It keeps to harpoon range and backs away when she closes. Its helmet seals the crown: break the latch at its neck first. Break its right elbow and it has no harpoon.' },
  deepwalker: { name: 'Deepwalker', tier: 'end', text: 'Something that drowned so deep the weight of the water made it huge. It walks the Gullet\'s floor and feels her in the water from across the room. Its hands close like a door. Its knots are dark: only a knot held in the focused beam can be broken.' },
  wick: { name: 'Wick', tier: 'end', text: 'In the Skull the drowned learned to hate the light. Near one, oil runs out of her lantern and flares die in its hands. It hunts the lamp, not her: shutter it and it loses her. The focused beam burns it, and it cannot stand in it.' },
  undertow: { name: 'The Undertow', tier: 'mid', text: 'A drowned bell-ringer the size of a chapel, fused to a channel buoy whose bell clangs as it moves. It travels only through waist-deep water in late Rising and High tide, never enters a Bell Room or a dry room, and is always heard before it comes. Break its buoy knot to drive it off for the rest of the tide. It cannot be killed until the Gullet.' },
};

Object.assign(H.LORE.thoughts, {
  see_gulletpair: 'Two tendrils on that one. Cut one and it will only swing.',
  see_hooded: 'Its eyes are bound with a hymnal page. It will not see my lamp. It will hear my feet.',
  see_saltgull: 'Gulls on the rocks, white as salt, and not one of them moving. Not asleep. Waiting for a noise.',
  see_netter: 'A woman wound in net, and more of it coiled in her hands. If that lands on me I am caught.',
  see_kilnman: 'He is still burning inside. Fire will not hurt him. Water might.',
  see_braced: 'It has grown salt over the knots I keep breaking. It is learning me.',
  see_bloater: 'Swollen like a drowned dog. Something is moving in its belly. I do not want to know what.',
  see_toller: 'A handbell. If he rings it, everything in here comes. The bell arm first.',
  see_cantor: 'It is singing, and the dead around it are listening. That song will not let them stay down.',
  see_ossuary: 'Bones. Too many, and not piled the way bones fall.',
  see_kite: 'Something up there in the wind, with a cloak spread like a sail.',
  see_diver: 'A diver. Brass helmet, canvas suit, like mine. It has a harpoon gun, and it is aiming it at me.',
  see_deepwalker: 'Something on the floor down there, as big as a boat. I cannot see its knots. Only the focused beam would show them.',
  see_wick: 'My lamp dims as it comes. It wants the light, not me.',
});
