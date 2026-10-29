// The game's data files, in load order: the core tables, then one file per region of the full island
// (each adds its rooms with room() and its carvings to H.LORE). index.html loads the same list.
'use strict';
module.exports = [
  'js/util.js', 'js/data/items.js', 'js/data/rooms.js', 'js/data/lore.js', 'js/data/bestiary.js',
  'js/data/rooms_wrackline.js', 'js/data/rooms_saltworks.js', 'js/data/rooms_cloister.js', 'js/data/rooms_choir.js', 'js/data/rooms_ribvault.js',
];
