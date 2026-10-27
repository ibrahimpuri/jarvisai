// Items, recipes, weapons, fittings and grafts, straight from the design document.
'use strict';

// perSlot: how many fit in one satchel slot. slots: key items take 2 slots each.
H.ITEMS = {
  rivets:   { name: 'Rivets', perSlot: 18, color: '#c9b37a', desc: 'Ammunition for the Rivet Driver. Crafted from scrap, never bought.' },
  bolts:    { name: 'Harpoon bolts', perSlot: 6, color: '#a8b8c0', desc: 'Pin a limb to a wall for 4 s. Recoverable, but lost if it sinks at Ebb.' },
  flares:   { name: 'Flares', perSlot: 3, color: '#ff9a4a', desc: 'Thrown with the Flare Sling. Light that draws Driftlings.' },
  spools:   { name: 'Saw-Line spools', perSlot: 3, color: '#d0d0d0', desc: 'A wire strung at knee height. Cuts knots that cross it.' },
  oil:      { name: 'Oil flask', perSlot: 3, color: '#e0b040', desc: 'One flask: 90 s of focused beam or 3 burns. Rekindles Bell Rooms.' },
  poultice: { name: 'Poultice', perSlot: 3, color: '#7fbf7f', desc: 'Restores 2 pips over 4 s; you can only walk while it works. A hit cancels the remainder.' },
  stitchkit:{ name: 'Stitch Kit', perSlot: 3, color: '#e07070', desc: 'Restores all health in 4 s while kneeling.' },
  scrap:    { name: 'Scrap', perSlot: 10, color: '#8a8a8a', desc: 'The contested material: rivets or bolts.' },
  cord:     { name: 'Cord', perSlot: 10, color: '#b89a6a', desc: 'Tarred cord from the wrecks.' },
  kelp:     { name: 'Kelp', perSlot: 10, color: '#4f8a4f', desc: 'Binds a poultice.' },
  salt:     { name: 'Salt', perSlot: 10, color: '#e8e8f0', desc: 'Dead salt. The live kind is worse.' },
  tallow:   { name: 'Tallow', perSlot: 10, color: '#e8d8a0', desc: 'Rendered into lamp oil.' },
  spirit:   { name: 'Spirit', perSlot: 10, color: '#b0d0ff', desc: 'Surgical spirit for a Stitch Kit.' },
  ivory:    { name: 'Mere-ivory', perSlot: 10, color: '#f4ecd8', desc: 'Rare bone from the Mere-Saint. Buys a weapon tier.' },
  // key items
  crank:    { name: 'Sluice Crank', key: true, slots: 2, color: '#c08040', desc: 'Opens the seawall gate to the Saltworks.' },
  tags:     { name: "Teodor's tags", key: true, slots: 1, color: '#d0c080', desc: "A diver's brass tags. Odile will know them." },
  clapper:  { name: 'Bell clapper', key: true, slots: 2, color: '#b0a070', desc: 'The tongue of a Bell Room bell.' },
  prism:    { name: 'Salt prism', key: true, slots: 2, color: '#d0f0ff', desc: 'Bends a focused beam.' },
};

H.RECIPES = [
  { id: 'rivets',   makes: 'rivets',   n: 2, cost: { scrap: 1 },             where: 'any',   time: 2.5 },
  { id: 'poultice', makes: 'poultice', n: 1, cost: { kelp: 2, salt: 1 },     where: 'any',   time: 3 },
  { id: 'bolts',    makes: 'bolts',    n: 2, cost: { scrap: 1, cord: 1 },    where: 'bench', time: 0 },
  { id: 'oil',      makes: 'oil',      n: 1, cost: { tallow: 2 },            where: 'bench', time: 0 },
  { id: 'flares',   makes: 'flares',   n: 2, cost: { oil: 1, cord: 1 },      where: 'bench', time: 0 },
  { id: 'stitchkit',makes: 'stitchkit',n: 1, cost: { poultice: 1, cord: 1, spirit: 1 }, where: 'bench', time: 0 },
  { id: 'spools',   makes: 'spools',   n: 1, cost: { cord: 2, scrap: 1 },    where: 'bench', time: 0 },
];

H.WEAPONS = {
  driver:  { name: 'Rivet Driver', ammo: 'rivets', mag: 6, reload: 1.5, cooldown: 0.36,
             tiers: ['Base', 'Twin-feed: fires 2 rivets per pull', 'Knot-seeker: snaps aim to a knot within 10°'] },
  harpoon: { name: 'Harpoon Gun', ammo: 'bolts', mag: 1, reload: 1.0, cooldown: 0.5,
             tiers: ['Base', 'Tether: yank a pinned enemy toward you', 'Barbed: the pinned knot shatters after 2 s'] },
  sling:   { name: 'Flare Sling', ammo: 'flares', mag: 1, reload: 0.8, cooldown: 0.6,
             tiers: ['Base', 'Sticky flares cling to bodies', 'Burn time doubled'] },
  sawline: { name: 'Saw-Line', ammo: 'spools', mag: 1, reload: 0.6, cooldown: 0.6,
             tiers: ['Base', 'String two lines at once', 'Line rewinds itself'] },
  // The legendary weapons of the last night (Act III only; see Legend.cs). They are not upgraded at a bench.
  // The Tine is the Mere's Hollow grown through the Harpoon Gun at the Saint's Ear in the Gullet; it throws on the
  // Hollow's resonance, and empty it throws on Maren's blood. The Glass is Agathe's lamp lens on Maren's lantern;
  // it burns on lantern oil and must cool.
  tine:    { name: "The Saint's Tine", ammo: 'resonance', mag: 1, reload: 0, cooldown: 0.85, legendary: true,
             tiers: ['The Hollow grown through the Harpoon Gun'] },
  glass:   { name: 'The Burning Glass', ammo: 'oil', mag: 1, reload: 0, cooldown: 0.1, legendary: true,
             tiers: ["Agathe's spare lamp lens"] },
};
H.UPGRADE_COST = [null, { scrap: 4, ivory: 1 }, { scrap: 8, ivory: 2 }];

H.FITTINGS = {
  cork:    { name: 'Cork Lining', p: 1, effect: '+40% breath', trade: 'None' },
  soles:   { name: 'Muffled Soles', p: 2, effect: 'Footstep noise radius halved', trade: 'Cannot stomp' },
  bilge:   { name: 'Bilge Pump', p: 2, effect: 'Poultices heal 1 extra pip', trade: 'None' },
  feeder:  { name: 'Rivet Feeder', p: 2, effect: 'Reload 40% faster', trade: 'Magazine drops from 6 to 5' },
  tallowr: { name: 'Tallow Reservoir', p: 2, effect: '+50% lantern oil capacity', trade: 'Lantern glow is visible from farther away' },
  splitter:{ name: 'Knot Splitter', p: 3, effect: 'Gaff hits on knots deal double damage', trade: 'Gaff hits on flesh deal half' },
  lead:    { name: 'Lead Plates', p: 3, effect: 'Take 1 less damage per hit', trade: 'Slower dash; sink fast in water' },
};

H.GRAFTS = {
  ear:  { name: 'Salt Ear', knot: 'Neck', power: 'Hear every Brine body within two rooms, shown on the map', cost: 'Hits from behind stagger you for 1 s' },
  arm:  { name: 'Brine Arm', knot: 'Wrist', power: 'Gaff hits crust a limb solid for 3 s', cost: 'A heavy hit can knock the gaff loose for 2 s' },
  lung: { name: 'Tide Lung', knot: 'Chest', power: 'Breathe underwater without limit', cost: 'Bell resonance hurts you: Bell Room healing is halved' },
};

H.ABILITIES = {
  swing:   { name: 'Gaff Swing', desc: 'Hold {swing} near an iron ring to swing from it. Let go to fly.' },
  boots:   { name: 'Ballast Boots', desc: 'Press {boots} to sink and walk the seabed. Ignore currents. Stomp a downed knot.' },
  jet:     { name: 'Jet Valve', desc: 'Press {dash} in the air or underwater to burst. Costs 3 s of suit air.' },
  tidekey: { name: 'Tide Key', desc: 'Work sluice gates ({interact}) to raise or drop a room\'s water.' },
  seal:    { name: 'Pressure Seal', desc: 'Dive the deep water of the Gullet. Breath rises to 60 s.' },
  lens:    { name: 'Saltsight Lens', desc: 'See knots in darkness without burning oil.' },
};

H.KNOT_TYPES = ['knee', 'elbow', 'shoulder', 'jaw', 'crown', 'spine', 'tendril', 'buoy', 'plate'];
