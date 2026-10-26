// NPCs, quests, collectibles and endings.
'use strict';
H.Story = (() => {
  const G = () => H.Game;
  const P = () => H.Game.player;
  const W = () => H.World;

  const PAGES = {
    1: 'Keeper\'s log, I. The monks did not build the lamp to guide ships. They built it to burn. Its beam is cut through a lens of Mere-ivory, and the Brine cannot abide it.',
    2: 'Keeper\'s log, II. The Abbot ordered the kilns stoked day and night. "Salt is the Saint\'s blood," he said. The salt was alive, and it climbed his arms like frost.',
    3: 'Keeper\'s log, III. Brother Ansel rings his bell every hour. He says the sound keeps the crust from his heart. I have seen the white on his hands. He is not wrong, and he is not well.',
    4: 'Keeper\'s log, IV. The Prior hid his tide tables when the Choir began to graft. The tables say when each door breathes. Whoever holds them holds the island.',
    5: 'Keeper\'s log, V. Hesper took the first graft in the font. She says the Saint is singing to her. She says if the hymn is finished the island will go home to the sea. She is not afraid.',
    6: 'Keeper\'s log, VI. I lit the lamp on the night of the spring tide, as Keepers have for two hundred years. Three of the Choir fell in the courtyard, smoking. The beam does not care who it burns.',
    7: 'Keeper\'s log, VII. I put the lamp out. Forty ships will pass this reef before the equinox. Without the light some will wreck. With it, everyone who carries a graft will die. I have not slept.',
    8: 'Keeper\'s log, VIII. There is a third way. The old warning beacon in the skull burns red, not white; it warns ships off without burning anyone. And the rope ferry still reaches the mainland. It needs hands to row it.',
    9: 'Keeper\'s log, IX. Maren, if you are reading this, you came for the lamp. Before you light it, count who is still alive. Then decide what a Keeper is for.',
  };
  const TABLES = {
    1: 'Prior\'s tide table, page one. "At Low the crypt door gives." Columns of numbers, each crossed through with a hymn line.',
    2: 'Prior\'s tide table, page two. "The Choir water does not answer the moon. It answers the key."',
    3: 'Prior\'s tide table, page three. "My door I keep for the Ebb, when the sea turns its back."',
    4: 'Prior\'s tide table, page four. "At the spring tide every line climbs one mark. Even the Bell Rooms are not deep enough."',
  };

  const LOOK = {
    ansel: { robe: '#4a4038', skin: '#a0a898', crust: true, hat: null },
    odile: { robe: '#5a4430', skin: '#c09878', hat: '#3a3028' },
    hesper: { robe: '#3a3a5a', skin: '#a8b0c0', crust: true, lamp: true, hat: '#c0a060' },
    agathe: { robe: '#2a3a4a', skin: '#c0a890', hat: '#b08840', lamp: true },
  };
  const NAMES = { ansel: 'Brother Ansel', odile: 'Odile Marrow', hesper: 'Hesper Vane', agathe: 'Agathe Hult' };

  function npcName(id) { return NAMES[id]; }
  function npcLook(id) { return LOOK[id]; }
  function choirFriendly() { return !W().flags.choirDrained && !W().flags.choirHostile; }
  function npcPresent(id, roomId) {
    const q = G().quests;
    if (id === 'ansel') return q.tables !== 'sold';
    if (id === 'odile') return !q.odileLost;
    if (id === 'hesper') return choirFriendly() && !q.hesperDead;
    if (id === 'agathe') return true;
    return true;
  }

  function talk(id) {
    const g = G(), p = P(), q = g.quests;
    const say = (lines, done) => H.UI.dialogue(NAMES[id], lines, done);
    if (id === 'ansel') {
      if (!q.anselMet) {
        q.anselMet = true;
        say([
          'Agathe\'s girl. You have her jaw. Come in, come in; nothing that walks out there will cross this threshold while the bell rings.',
          'Forty days since the lamp went dark. The Brine took the Abbey in a week. It grows knots at the joints of the drowned and works them like puppets.',
          'You will have seen the chalk: haul them or burn them. Leave one lying in the water and the next tide gives it back.',
          'If you mean to reach the lamp you must go up through the Saltworks. The seawall gate wants its Sluice Crank. The crank lies on the altar of the Tithe Hall, below us.',
          'And mind the hall\'s bell rope. Rest here when you must. Resting turns the tide to the next Low, and the tide does not care what you left lying in it.',
        ], () => { g.setQuest('crank', 'active'); });
        return;
      }
      if (g.tables.size >= 4 && q.tables !== 'given' && q.tables !== 'sold') {
        say([
          'The Prior\'s tables! All four. Bless you, child.',
          'Take this. I ground it from the Prior\'s own spectacles and a sliver of Mere-ivory. Through it you will see the knots in the dark, without wasting oil.',
        ], () => {
          q.tables = 'given'; p.abilities.add('lens'); H.Audio.pickup();
          H.toast('Saltsight Lens: knots show in darkness without burning oil.');
        });
        return;
      }
      const hints = [];
      if (!W().flags.seawallOpen) hints.push('The crank. Tithe Hall, below the seawall. Go at Low tide; you have Low and Rising before the pews go under.');
      else if (g.act === 1) hints.push('The Abbot of Salt waits above the kilns. They say his plates only fall when he strikes his own furnaces.');
      if (g.tables.size > 0 && g.tables.size < 4) hints.push(`You have ${g.tables.size} of the Prior's four tide tables. Bring me all four. The Choir would pay for them too, but I would rather you did not.`);
      if (g.act >= 2 && !p.has('jet')) hints.push('The Prior\'s door answers to the hymnal. "When the sea turns its back": that is the Ebb.');
      if (g.act >= 2 && g.encrustedType) hints.push(`The Brine is learning. The ones that come back now are braced at the ${g.encrustedType}, where you have been breaking them. It always leaves two knots open.`);
      if (p.grafts.size) hints.push('I can see the salt in you. If you want a graft cut out, use my bench. It costs a pip of your health, and only once.');
      if (!hints.length) hints.push('The lamp is in the skull of the Saint. Your mother is there, I think. Go carefully.');
      const al = H.Narr.npcLore('ansel'); if (al) hints.push(al);
      say(hints);
      return;
    }
    if (id === 'odile') {
      if (!q.odileMet) {
        q.odileMet = true;
        say([
          'Another diver! Odile Marrow. Salvage smith, when there is salvage. Use my bench all you like. Scrap and Mere-ivory buy weapon tiers.',
          'My partner Teodor went down into the Lower Cloister a week ago and did not come up. If you find him... bring me his tags. Or bring me him.',
          'I am not stupid. If he is walking, he is Brine. But I have heard the Brine lets go of some of them, if the Crown is left whole.',
        ], () => { if (!q.teodor) q.teodor = 'asked'; });
        return;
      }
      if (p.bag.count('tags') && q.teodor === 'killed') {
        say(['His tags.', '...', 'Thank you for not leaving him down there. Here: I finished his harpoon. Barbed bolts. It is the best work I have done.', 'And take his satchel. He would want it used.'], () => {
          p.bag.remove('tags', 1);
          if (!p.weapons.harpoon) { p.weapons.harpoon = { tier: 0, mag: 1 }; p.weaponOrder.push('harpoon'); p.bag.add('bolts', 4); }
          p.weapons.harpoon.tier = 2;
          p.bag.size = Math.max(p.bag.size, 12);
          q.teodor = 'tags';
          H.toast('Harpoon Gun upgraded to tier 3 (Barbed). Satchel expanded to ' + p.bag.size + ' slots.');
        });
        return;
      }
      if (q.teodor === 'bound') {
        say(['You... you brought him back whole. His Crown is still lit.', 'I will keep him tied in the back room, where the bell is loudest. Perhaps the Brine will let go.', 'Take his satchel. I owe you more than that.'], () => {
          q.teodor = 'captured'; p.bag.size = Math.max(p.bag.size, 12);
          H.toast('Satchel expanded to ' + p.bag.size + ' slots.');
        });
        return;
      }
      const lines = [];
      if (q.teodor === 'asked') lines.push('Any sign of Teodor? He wore brass tags at his collar. Shuckers in the Cloister, mostly.');
      if (q.teodor === 'captured' && g.act < 3) lines.push('Teodor is quiet today. Sometimes he hums.');
      if (W().flags.choirDrained && !q.cacheTold) { lines.push('You drained the Choir? Then my old salvage cache is lying in the open on the shrine floor. Take what is in it.'); q.cacheTold = true; }
      if (!lines.length) lines.push('Bring me scrap and Mere-ivory and I will make your tools nastier. That is what I am for.');
      const ol = H.Narr.npcLore('odile'); if (ol) lines.push(ol);
      say(lines);
      return;
    }
    if (id === 'hesper') {
      if (!q.hesperMet) {
        q.hesperMet = true;
        say([
          'A Keeper\'s daughter in the Saint\'s own shrine. Be welcome. We will not harm you while you keep the water.',
          'The Saint sings, diver. Under the salt and the bone, it is still singing. The Choir is learning the hymn.',
          'The font will give you the Saint\'s gifts, if you want them. Each one grows a knot on you. Each one makes you more like us.',
          'And if you find the Prior\'s tide tables, lay them in our bowl. The Saint will reward you.',
        ]);
        return;
      }
      if (p.has('tidekey') && !q.hesperSatchel && !q.cacheSatchel) {
        say(['You hold the Tide Key, and still the shrine is full. You could have drained us, and you did not.', 'Take this. A Choir satchel, woven of kelp and salt. It will hold more than you think.'], () => {
          q.hesperSatchel = true; p.bag.size = Math.max(p.bag.size + 4, 12);
          H.toast('Satchel expanded to ' + p.bag.size + ' slots.');
        });
        return;
      }
      say([H.Narr.npcLore('hesper'), g.act >= 3 ? 'The spring tide is here. When you reach the lamp, I will be there, if you want the hymn finished.' : 'Three gifts, and the Choir at your side, and you could finish the Saint\'s hymn yourself.']);
      return;
    }
    if (id === 'agathe') {
      const n = g.pages.size;
      const lines = [
        'Maren. You rowed out. Of course you did.',
        'I put the lamp out. Its beam burns the Brine out of anything it touches, and that includes everyone who took a graft. Hesper. Ansel. Maybe you.',
      ];
      if (n < 9) lines.push(`I wrote it all down. My log is scattered across the island; you have ${n} of my 9 pages.`);
      lines.push('There are three things a Keeper could do tonight.');
      lines.push('Light the lamp: the great lamp at the centre. The Brine burns out of the island, and everyone carrying a graft dies with it.');
      lines.push('Ring the Saint\'s bell: the hymn can only be finished by someone with three grafts and the Choir beside her. The island goes home to the sea.');
      lines.push('Or fire the old red beacon and leave the lamp dark: ships are warned off and no one is burned. But we need my whole log to know the ferry\'s tides, and at least two others alive to row it.');
      say(lines);
    }
  }

  function offerTables() {
    const g = G();
    if (g.tables.size === 0) { H.UI.say(null, 'An offering bowl of salt-white bone. Hesper said the Choir wants the Prior\'s tide tables.'); return; }
    if (g.tables.size < 4) { H.UI.say(null, `The bowl is sized for four pages. You have ${g.tables.size}.`); return; }
    H.UI.confirm('Lay all four tide tables in the Choir\'s bowl? Ansel will not get them.', () => {
      g.quests.tables = 'sold';
      g.freeGraft = true;
      H.Audio.toll();
      H.toast('The Choir takes the tables. Far below, a bell cracks and falls silent.');
      H.UI.say('Hesper Vane', 'The Saint is pleased. The font will give you one gift freely, without the knot weighing on your path. Ansel\'s bell has rung its last. Do not grieve; he was already ours.');
    });
  }

  function collect(spec) {
    const g = G(), p = P();
    H.Audio.pickup();
    switch (spec.type) {
      case 'stitch':
        if (p.maxHp < 9) p.maxHp++;
        p.hp = p.maxHp;
        H.toast(`Keeper's Stitch. Health rises to ${p.maxHp} pips.`);
        g.collected[spec.id] = true;
        break;
      case 'seal':
        p.pressure = Math.min(10, p.pressure + 1);
        H.toast(`Brass Seal. The suit's Pressure Rating rises to ${p.pressure}.`);
        g.collected[spec.id] = true;
        break;
      case 'page':
        g.pages.add(spec.n);
        H.UI.say(`Agathe's log (${g.pages.size}/9)`, PAGES[spec.n]);
        break;
      case 'table':
        g.tables.add(spec.n);
        H.UI.say(`Prior's tide table (${g.tables.size}/4)`, TABLES[spec.n]);
        break;
      case 'ability': {
        const a = H.ABILITIES[spec.id];
        p.abilities.add(spec.id);
        H.Audio.bell(520, 0.3);
        H.UI.say(a.name, a.desc);
        if (spec.id === 'jet') W().flags.priorWedged = true;
        if (spec.id === 'seal') g.startAct(3);
        break;
      }
      case 'weapon': {
        const def = H.WEAPONS[spec.id];
        if (!p.weapons[spec.id]) { p.weapons[spec.id] = { tier: 0, mag: def.mag }; p.weaponOrder.push(spec.id); }
        const ammo = { harpoon: ['bolts', 4], sling: ['flares', 3], sawline: ['spools', 2] }[spec.id];
        if (ammo) p.bag.add(ammo[0], ammo[1]);
        H.UI.say(def.name, { harpoon: 'Pins a limb to a wall for 4 s. Bolts can be recovered if they do not sink at Ebb. Change weapon with {weapon}.', sling: 'Throws flares that burn for 10 s and draw Driftlings. A lit flare stuck to a body lets the swarm strip its knots for you.', sawline: 'Strings a wire at knee height that cuts the knots of anything crossing it.' }[spec.id]);
        break;
      }
    }
  }

  // ---- endings ---------------------------------------------------------------------------
  function survivors() {
    const q = G().quests;
    const list = [];
    if (q.tables !== 'sold') list.push('Brother Ansel');
    if (!q.odileLost) list.push('Odile Marrow');
    if (!W().flags.choirDrained && !q.hesperDead) list.push('Hesper Vane');
    if (q.teodor === 'saved') list.push('Teodor');
    return list;
  }
  function ending(kind) {
    const g = G(), p = P(), q = g.quests;
    if (kind === 'hymn') {
      const allied = choirFriendly() && !q.hesperDead;
      if (p.grafts.size < 3 || !allied) {
        H.UI.say(null, `The Saint's bell will not sound for you. The hymn needs three grafts (you carry ${p.grafts.size}) and an allied Choir${allied ? '' : ' (the Choir is not with you)'}.`);
        return;
      }
    }
    if (kind === 'dark') {
      const s = survivors();
      if (g.pages.size < 9 || s.length < 2) {
        H.UI.say(null, `Without Agathe's whole log (${g.pages.size}/9 pages) and at least two others alive to row the ferry (${s.length}: ${s.join(', ') || 'none'}), the beacon would only light an empty island.`);
        return;
      }
    }
    const titles = { relight: 'Relight the Lamp', hymn: "The Saint's Hymn", dark: "Keeper's Dark" };
    H.UI.confirm(`${titles[kind]}. This ends the night. Are you sure?`, () => finish(kind));
  }
  function finish(kind) {
    const g = G(), p = P(), q = g.quests;
    let text = [];
    if (kind === 'relight') {
      text.push('You open the shutters and the great lamp catches. The beam swings out across the reef, white as salt.');
      text.push('Everywhere it touches, the Brine boils away. Knots crack in every flooded room on the island at once. The drowned lie down for good.');
      const dead = [];
      if (q.tables !== 'sold') dead.push('Brother Ansel, still ringing his bell');
      if (!q.hesperDead && !W().flags.choirDrained) dead.push('Hesper Vane and the Choir');
      if (dead.length) text.push('The beam does not care who it burns: ' + dead.join('; ') + '.');
      if (p.grafts.size >= 2) text.push('The salt in your own body flares. You hear the bell of the Seawall Chapel once, far away, and then nothing. The ships come home by your light.');
      else text.push('Ships pass the reef safely by morning. You keep the lamp, as your mother did. You do not sleep well.');
    } else if (kind === 'hymn') {
      text.push('You ring the Saint\'s bell and the Choir answers from below. The hymn comes out of you in a voice that is not yours.');
      text.push('The ribs groan. The skull tilts. Slowly, with the patience of something very large and very old, the Mere-Saint rolls over and slides into the deep, and the island goes with it.');
      text.push('You go too, singing. Agathe watches from the rope ferry. The reef is clear forever.');
    } else {
      text.push('You leave the lamp dark and set fire to the old red beacon. It burns all night, warning every ship away from the reef.');
      text.push('At the ebb you load the rope ferry: ' + survivors().join(', ') + ', Agathe and you. No one is burned. No one is left behind who could be carried.');
      text.push('The mainland is warned. Somewhere behind you, the Brine keeps the island for itself, and waits.');
    }
    g.ended = kind;
    try { localStorage.removeItem('hollowmere_save'); } catch (e) {}
    H.UI.ending({ relight: 'Relight the Lamp', hymn: "The Saint's Hymn", dark: "Keeper's Dark" }[kind], text, g.statsSummary(), H.Narr.fates(kind));
  }

  return { npcName, npcLook, npcPresent, talk, offerTables, collect, ending, survivors, choirFriendly, PAGES, TABLES };
})();
