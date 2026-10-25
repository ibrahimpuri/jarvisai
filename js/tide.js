// The tide clock: one global cycle that every other system hangs on.
'use strict';
H.Tide = (() => {
  const PHASES = [
    { id: 'low', name: 'Low', len: 180 },
    { id: 'rising', name: 'Rising', len: 120 },
    { id: 'high', name: 'High', len: 180 },
    { id: 'ebbing', name: 'Ebbing', len: 120 },
  ];
  // World-space water surfaces (in tiles, y grows downward).
  const SEA_LOW = 109, SEA_HIGH = 101;

  const T = {
    t: 0,              // seconds into the current cycle, already scaled
    cycle: 0,
    scale: 1,          // tide-length multiplier (1, 1.5, 2) times difficulty
    spring: 0,         // Act III spring tides raise every line
    paused: false,
    listeners: [],
    PHASES, SEA_LOW, SEA_HIGH,

    len(i) { return PHASES[i].len * this.scale; },
    total() { return PHASES.reduce((s, p, i) => s + this.len(i), 0); },
    index() {
      let acc = 0;
      for (let i = 0; i < 4; i++) { acc += this.len(i); if (this.t < acc) return i; }
      return 3;
    },
    phase() { return PHASES[this.index()].id; },
    phaseName() { return PHASES[this.index()].name; },
    phaseStart(i = this.index()) { let acc = 0; for (let k = 0; k < i; k++) acc += this.len(k); return acc; },
    phaseRemaining() { const i = this.index(); return this.phaseStart(i) + this.len(i) - this.t; },
    phaseProgress() { const i = this.index(); return (this.t - this.phaseStart(i)) / this.len(i); },

    seaLevel(atT = this.t) {
      const lo = SEA_LOW - this.spring, hi = SEA_HIGH - this.spring;
      let acc = 0;
      for (let i = 0; i < 4; i++) {
        const L = this.len(i);
        if (atT < acc + L || i === 3) {
          const p = H.clamp((atT - acc) / L, 0, 1);
          const s = p * p * (3 - 2 * p);
          if (i === 0) return lo;
          if (i === 1) return H.lerp(lo, hi, s);
          if (i === 2) return hi;
          return H.lerp(hi, lo, s);
        }
        acc += L;
      }
      return lo;
    },
    // 0 at low water, 1 at high water.
    level01() { return H.clamp((SEA_LOW - this.spring - this.seaLevel()) / (SEA_LOW - SEA_HIGH), 0, 1); },

    on(fn) { this.listeners.push(fn); },
    update(dt) {
      if (this.paused) return;
      const before = this.index();
      this.t += dt;
      const total = this.total();
      if (this.t >= total) { this.t -= total; this.cycle++; }
      const after = this.index();
      if (after !== before) this.listeners.forEach((fn) => fn(PHASES[after].id, PHASES[before].id));
    },
    // Jump the clock forward, firing every phase change on the way.
    advanceTo(phaseId) {
      const target = PHASES.findIndex((p) => p.id === phaseId);
      let guard = 0;
      do {
        const i = this.index();
        const step = this.phaseStart(i) + this.len(i) - this.t + 0.01;
        this.update(step);
        guard++;
      } while (this.index() !== target && guard < 8);
    },
    setPhase(phaseId, intoSeconds = 0) {
      const i = PHASES.findIndex((p) => p.id === phaseId);
      this.t = this.phaseStart(i) + intoSeconds;
    },
  };
  return T;
})();
