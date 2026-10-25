// The satchel: slot-limited stacks, plus the shared Reliquary chest.
'use strict';
H.Satchel = (() => {
  function slotsFor(item, n) {
    const def = H.ITEMS[item];
    if (def.key) return def.slots;
    return Math.ceil(n / def.perSlot);
  }

  class Bag {
    constructor(size) { this.size = size; this.items = {}; }
    count(item) { return this.items[item] || 0; }
    used() {
      let s = 0;
      for (const k in this.items) if (this.items[k] > 0) s += slotsFor(k, this.items[k]);
      return s;
    }
    // How many of item can still be added.
    room(item) {
      const def = H.ITEMS[item];
      const free = this.size - this.used();
      if (def.key) return this.count(item) > 0 ? 0 : (free >= def.slots ? 1 : 0);
      const have = this.count(item);
      const partial = have % def.perSlot === 0 ? 0 : def.perSlot - (have % def.perSlot);
      return partial + free * def.perSlot;
    }
    add(item, n = 1) {
      const can = Math.min(n, this.room(item));
      if (can > 0) this.items[item] = this.count(item) + can;
      return n - can; // leftover
    }
    remove(item, n = 1) {
      const have = this.count(item);
      const take = Math.min(have, n);
      this.items[item] = have - take;
      if (this.items[item] <= 0) delete this.items[item];
      return take;
    }
    has(cost) { return Object.entries(cost).every(([k, v]) => this.count(k) >= v); }
    pay(cost) { for (const [k, v] of Object.entries(cost)) this.remove(k, v); }
    list() { return Object.entries(this.items).filter(([, n]) => n > 0); }
    clear() { this.items = {}; }
    toJSON() { return { size: this.size, items: this.items }; }
    static from(o) { const b = new Bag(o.size); b.items = Object.assign({}, o.items); return b; }
  }

  const MATERIALS = ['scrap', 'cord', 'kelp', 'salt', 'tallow', 'spirit'];
  return { Bag, slotsFor, MATERIALS };
})();
