// Player settings kept between sessions: volume and controller options.
'use strict';
H.Settings = (() => {
  const KEY = 'hollowmere_settings';
  const S = {
    volume: 0.7,
    vibration: 1,          // 0, 0.5 or 1
    aimAssist: true,
    prompts: 'auto',       // auto, xbox, playstation, switch
    nintendoConfirm: true, // Switch controllers confirm with A (the right button)
    hints: true,
    load() {
      try { Object.assign(S, JSON.parse(localStorage.getItem(KEY)) || {}); } catch (e) { /* defaults */ }
    },
    save() {
      const o = {};
      for (const k of ['volume', 'vibration', 'aimAssist', 'prompts', 'nintendoConfirm', 'hints']) o[k] = S[k];
      try { localStorage.setItem(KEY, JSON.stringify(o)); } catch (e) { /* storage unavailable */ }
    },
  };
  S.load();
  return S;
})();
