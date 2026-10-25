// Procedural sound: everything is synthesised with WebAudio, no asset files.
'use strict';
H.Audio = (() => {
  let ctx = null, master = null, sfxBus = null, ambBus = null;
  let noiseBuf = null;
  let ambWater = null, ambDrone = null;
  let volume = 0.7;

  function ensure() {
    if (ctx) return true;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return false;
    ctx = new AC();
    master = ctx.createGain(); master.gain.value = volume; master.connect(ctx.destination);
    sfxBus = ctx.createGain(); sfxBus.gain.value = 0.9; sfxBus.connect(master);
    ambBus = ctx.createGain(); ambBus.gain.value = 0.5; ambBus.connect(master);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    startAmbience();
    return true;
  }

  function resume() {
    if (!ensure()) return;
    if (ctx.state === 'suspended') ctx.resume();
  }

  function out(pan = 0, vol = 1) {
    const g = ctx.createGain(); g.gain.value = vol;
    if (ctx.createStereoPanner) {
      const p = ctx.createStereoPanner(); p.pan.value = H.clamp(pan, -1, 1);
      g.connect(p); p.connect(sfxBus);
    } else g.connect(sfxBus);
    return g;
  }

  function env(g, t0, a, peak, d) {
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(peak, t0 + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + a + d);
  }

  function tone(type, freq, dur, vol, pan, glide) {
    if (!ctx) return;
    const t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = type; o.frequency.setValueAtTime(freq, t);
    if (glide) o.frequency.exponentialRampToValueAtTime(Math.max(20, glide), t + dur);
    const g = ctx.createGain(); env(g, t, 0.005, vol, dur);
    o.connect(g); g.connect(out(pan));
    o.start(t); o.stop(t + dur + 0.05);
  }

  function noise(dur, vol, pan, filterType = 'lowpass', freq = 1200, q = 0.7, attack = 0.005) {
    if (!ctx) return;
    const t = ctx.currentTime;
    const s = ctx.createBufferSource(); s.buffer = noiseBuf;
    s.playbackRate.value = H.rand(0.8, 1.2);
    const f = ctx.createBiquadFilter(); f.type = filterType; f.frequency.value = freq; f.Q.value = q;
    const g = ctx.createGain(); env(g, t, attack, vol, dur);
    s.connect(f); f.connect(g); g.connect(out(pan));
    s.start(t, Math.random()); s.stop(t + dur + attack + 0.05);
  }

  // A bell is a stack of inharmonic partials with a long decay.
  function bell(freq, vol = 0.4, pan = 0, decay = 3.5) {
    if (!ctx) return;
    const t = ctx.currentTime;
    const partials = [[0.5, 0.6], [1, 1], [1.19, 0.5], [1.56, 0.35], [2, 0.4], [2.74, 0.2], [3.76, 0.12]];
    const o2 = out(pan, vol);
    for (const [m, a] of partials) {
      const o = ctx.createOscillator(); o.type = 'sine'; o.frequency.value = freq * m;
      const g = ctx.createGain(); env(g, t, 0.003, a * 0.4, decay / (0.6 + m * 0.4));
      o.connect(g); g.connect(o2);
      o.start(t); o.stop(t + decay + 0.1);
    }
  }

  function startAmbience() {
    // Surf: looping filtered noise whose level follows the tide.
    const s = ctx.createBufferSource(); s.buffer = noiseBuf; s.loop = true;
    const f = ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 500;
    const g = ctx.createGain(); g.gain.value = 0.05;
    const lfo = ctx.createOscillator(); lfo.frequency.value = 0.12;
    const lg = ctx.createGain(); lg.gain.value = 0.03;
    lfo.connect(lg); lg.connect(g.gain);
    s.connect(f); f.connect(g); g.connect(ambBus);
    s.start(); lfo.start();
    ambWater = { g, f };
    // Low drone: two detuned saws through a dark filter.
    const dg = ctx.createGain(); dg.gain.value = 0.0;
    const df = ctx.createBiquadFilter(); df.type = 'lowpass'; df.frequency.value = 180;
    [55, 55.6, 82.4].forEach((fr) => {
      const o = ctx.createOscillator(); o.type = 'sawtooth'; o.frequency.value = fr;
      const og = ctx.createGain(); og.gain.value = 0.05;
      o.connect(og); og.connect(df); o.start();
    });
    df.connect(dg); dg.connect(ambBus);
    ambDrone = { g: dg, f: df };
  }

  return {
    resume,
    get ready() { return !!ctx; },
    setVolume(v) { volume = v; if (master) master.gain.value = v; },
    ambience(waterLevel01, tension01, underwater) {
      if (!ctx) return;
      const t = ctx.currentTime;
      ambWater.g.gain.setTargetAtTime(0.03 + waterLevel01 * 0.07, t, 0.5);
      ambWater.f.frequency.setTargetAtTime(underwater ? 250 : 500 + waterLevel01 * 300, t, 0.3);
      ambDrone.g.gain.setTargetAtTime(0.15 + tension01 * 0.5, t, 1.0);
      ambDrone.f.frequency.setTargetAtTime(120 + tension01 * 300, t, 1.0);
    },
    step(metal, vol = 0.12, pan = 0) {
      noise(0.05, vol, pan, 'bandpass', metal ? 2600 : 700, metal ? 6 : 1.5);
      if (metal) tone('triangle', 900 + Math.random() * 200, 0.08, vol * 0.4, pan);
    },
    splash(vol = 0.25, pan = 0) { noise(0.35, vol, pan, 'lowpass', 1400, 0.5, 0.01); },
    jump(pan = 0) { noise(0.08, 0.08, pan, 'bandpass', 500, 1); },
    rivet(pan = 0) { noise(0.06, 0.4, pan, 'highpass', 2000, 1); tone('square', 220, 0.07, 0.15, pan, 80); },
    harpoon(pan = 0) { noise(0.15, 0.3, pan, 'bandpass', 900, 2); tone('sawtooth', 160, 0.2, 0.15, pan, 60); },
    sling(pan = 0) { noise(0.2, 0.15, pan, 'bandpass', 1500, 3, 0.04); },
    reload() { tone('square', 600, 0.04, 0.08); setTimeout(() => tone('square', 900, 0.04, 0.08), 120); },
    empty() { tone('square', 1200, 0.03, 0.06); },
    tink(pan = 0) { tone('triangle', 2400, 0.08, 0.12, pan, 1800); },
    thud(pan = 0) { noise(0.12, 0.25, pan, 'lowpass', 300); },
    crack(pan = 0) { noise(0.18, 0.5, pan, 'highpass', 1500, 0.8); tone('triangle', 1700, 0.25, 0.2, pan, 500); },
    gaff(pan = 0) { noise(0.12, 0.15, pan, 'bandpass', 2000, 2, 0.02); },
    hook(pan = 0) { tone('triangle', 1400, 0.1, 0.12, pan, 900); },
    hurt() { tone('sawtooth', 180, 0.25, 0.25, 0, 70); noise(0.15, 0.2, 0, 'lowpass', 800); },
    heal() { tone('sine', 520, 0.3, 0.12); setTimeout(() => tone('sine', 780, 0.4, 0.1), 150); },
    pickup() { tone('triangle', 880, 0.08, 0.1); setTimeout(() => tone('triangle', 1320, 0.1, 0.08), 70); },
    craft() { noise(0.1, 0.12, 0, 'bandpass', 3000, 4); tone('triangle', 660, 0.12, 0.08); },
    groan(pan = 0, vol = 0.18) { tone('sawtooth', H.rand(70, 95), 0.9, vol, pan, H.rand(50, 60)); },
    inhale(pan = 0) { noise(1.5, 0.25, pan, 'bandpass', 600, 1.5, 1.2); },
    scream(pan = 0) {
      tone('sawtooth', 420, 1.2, 0.3, pan, 900); tone('square', 610, 1.2, 0.15, pan, 1300);
      noise(1.2, 0.3, pan, 'bandpass', 2500, 1);
    },
    grab() { noise(0.2, 0.3, 0, 'lowpass', 600); tone('sawtooth', 110, 0.3, 0.2, 0, 70); },
    fire(pan = 0) { noise(0.6, 0.2, pan, 'bandpass', 900, 0.5, 0.1); },
    burst(pan = 0) { noise(0.25, 0.25, pan, 'bandpass', 400, 1, 0.01); },
    bubble() { tone('sine', H.rand(500, 900), 0.06, 0.05, 0, 1400); },
    bell,
    buoy(vol, pan) { bell(98, vol, pan, 4); },
    tideBell() { bell(330, 0.25, 0, 3); },
    roomBell() { bell(392, 0.35, 0, 5); setTimeout(() => bell(392 * 1.5, 0.15, 0, 4), 300); },
    toll() { bell(110, 0.6, 0, 6); },
    ui() { tone('triangle', 700, 0.05, 0.06); },
    deny() { tone('square', 160, 0.12, 0.1); },
    rumble() { noise(1.5, 0.4, 0, 'lowpass', 120, 0.5, 0.2); },
    flare(pan = 0) { noise(0.5, 0.2, pan, 'highpass', 3000, 0.5, 0.02); },
  };
})();
