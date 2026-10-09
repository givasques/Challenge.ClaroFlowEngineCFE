// Preferências de acessibilidade do canal (FASE 4.5, itens A.2). Carregado síncrono no <head>
// para aplicar os atributos em <html> antes da primeira pintura. Cópia por canal: não há pasta
// compartilhada servida nos dois modos (ver padroes-sprint4.md, P38).
(function () {
  var STORAGE_KEY = 'cfe_panel_a11y';
  var DEFAULTS = {
    textSize: '100',
    contrast: 'default',
    colorVision: 'default',
    textSpacing: 'default',
    reduceMotion: false,
    focusHighlight: false,
  };
  var ATTRS = {
    textSize: 'data-a11y-text',
    contrast: 'data-a11y-contrast',
    colorVision: 'data-a11y-color',
    textSpacing: 'data-a11y-spacing',
    reduceMotion: 'data-a11y-motion',
    focusHighlight: 'data-a11y-focus',
  };
  var ATTR_VALUES = {
    textSize: function (v) { return v === '100' ? null : v; },
    contrast: function (v) { return v === 'high' ? 'high' : null; },
    colorVision: function (v) { return v === 'colorblind' ? 'colorblind' : null; },
    textSpacing: function (v) { return v === 'wide' ? 'wide' : null; },
    reduceMotion: function (v) { return v ? 'reduce' : null; },
    focusHighlight: function (v) { return v ? 'strong' : null; },
  };

  function systemDefaults() {
    var prefs = Object.assign({}, DEFAULTS);
    if (window.matchMedia) {
      prefs.reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      prefs.contrast = window.matchMedia('(prefers-contrast: more)').matches ? 'high' : 'default';
    }
    return prefs;
  }

  function readStored() {
    try {
      var raw = window.localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      return null;
    }
  }

  function writeStored(prefs) {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
    } catch (e) {
      // localStorage bloqueado: o menu continua funcionando na sessão, só não lembra.
    }
  }

  function clearStored() {
    try {
      window.localStorage.removeItem(STORAGE_KEY);
    } catch (e) {
      // ver writeStored
    }
  }

  function current() {
    var stored = readStored();
    if (!stored) return systemDefaults();
    return Object.assign({}, DEFAULTS, stored);
  }

  function apply(prefs) {
    var root = document.documentElement;
    Object.keys(ATTRS).forEach(function (key) {
      var value = ATTR_VALUES[key](prefs[key]);
      if (value === null) root.removeAttribute(ATTRS[key]);
      else root.setAttribute(ATTRS[key], value);
    });
  }

  function set(key, value) {
    var prefs = current();
    prefs[key] = value;
    writeStored(prefs);
    apply(prefs);
    return prefs;
  }

  function reset() {
    clearStored();
    var prefs = systemDefaults();
    apply(prefs);
    return prefs;
  }

  window.CfeA11y = { get: current, set: set, reset: reset, apply: apply, storageKey: STORAGE_KEY };
  apply(current());
})();
