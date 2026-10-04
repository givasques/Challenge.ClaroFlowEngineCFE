// Interface do menu de acessibilidade e gestão de foco de modais (FASE 4.5, itens B.1 e B.2).
// Cópia por canal, como a.11y.js. Depende de CfeA11y (a11y.js) carregado antes.
(function () {
  var MODAL_SELECTOR = '.panel-modal-overlay, .modal-overlay, .a11y-menu';
  var FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
  var openers = new WeakMap();
  var wasOpen = new WeakMap();
  var liveRegion = document.getElementById('a11y-live');

  function announce(message) {
    if (!liveRegion) return;
    liveRegion.textContent = '';
    setTimeout(function () { liveRegion.textContent = message; }, 40);
  }

  function isOpen(el) {
    return !el.classList.contains('hidden');
  }

  function focusables(root) {
    return Array.prototype.filter.call(root.querySelectorAll(FOCUSABLE), function (el) {
      return el.getClientRects().length > 0;
    });
  }

  function modals() {
    return Array.prototype.slice.call(document.querySelectorAll(MODAL_SELECTOR));
  }

  function topOpenModal() {
    return modals().filter(isOpen).pop() || null;
  }

  function isMenu(el) {
    return el.classList.contains('a11y-menu');
  }

  function closeModal(el) {
    if (isMenu(el)) {
      el.classList.add('hidden');
      return;
    }
    var cancel = el.querySelector('[data-a11y-close]') || el.querySelector('.secondary-button');
    if (cancel) cancel.click();
    else el.classList.add('hidden');
  }

  function onOpen(el) {
    openers.set(el, document.activeElement);
    if (isMenu(el)) renderMenuState();
    var first = el.querySelector('[data-a11y-initial-focus]') || el.querySelector('input[name="a11y-text"]:checked') || focusables(el)[0];
    if (first) first.focus();
  }

  function onClose(el) {
    var opener = openers.get(el);
    if (opener && document.contains(opener) && typeof opener.focus === 'function') {
      opener.focus();
    }
    openers.delete(el);
  }

  modals().forEach(function (el) {
    wasOpen.set(el, isOpen(el));
    new MutationObserver(function () {
      var nowOpen = isOpen(el);
      if (nowOpen === wasOpen.get(el)) return;
      wasOpen.set(el, nowOpen);
      if (nowOpen) onOpen(el);
      else onClose(el);
    }).observe(el, { attributes: true, attributeFilter: ['class'] });
  });

  document.addEventListener('keydown', function (event) {
    var el = topOpenModal();
    if (!el) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      closeModal(el);
      return;
    }
    if (event.key !== 'Tab') return;
    var items = focusables(el);
    if (!items.length) return;
    var first = items[0];
    var last = items[items.length - 1];
    var active = document.activeElement;
    if (event.shiftKey && (active === first || !el.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !el.contains(active))) {
      event.preventDefault();
      first.focus();
    }
  });

  // ---------- Menu de acessibilidade ----------

  var menu = document.getElementById('a11y-menu');
  var TEXT_LABELS = { '100': 'Padrão', '115': 'Grande', '130': 'Maior', '150': 'Muito grande' };
  var SWITCHES = {
    contrast: { on: 'high', off: 'default', label: 'Alto contraste' },
    colorVision: { on: 'colorblind', off: 'default', label: 'Cores para daltonismo' },
    textSpacing: { on: 'wide', off: 'default', label: 'Espaçamento de texto' },
    reduceMotion: { on: true, off: false, label: 'Reduzir animações' },
    focusHighlight: { on: true, off: false, label: 'Destacar foco do teclado' },
  };

  function renderMenuState() {
    if (!menu || !window.CfeA11y) return;
    var prefs = window.CfeA11y.get();
    menu.querySelectorAll('input[name="a11y-text"]').forEach(function (radio) {
      radio.checked = radio.value === prefs.textSize;
    });
    menu.querySelectorAll('[role="switch"][data-a11y-key]').forEach(function (sw) {
      var key = sw.getAttribute('data-a11y-key');
      var on = prefs[key] === SWITCHES[key].on;
      sw.setAttribute('aria-checked', on ? 'true' : 'false');
    });
  }

  function setPref(key, value, message) {
    window.CfeA11y.set(key, value);
    renderMenuState();
    announce(message);
  }

  function toggleMenu() {
    if (!menu) return;
    if (isOpen(menu)) closeModal(menu);
    else menu.classList.remove('hidden');
  }

  document.addEventListener('keydown', function (event) {
    if (event.altKey && event.shiftKey && event.code === 'KeyA') {
      event.preventDefault();
      toggleMenu();
    }
  });

  document.querySelectorAll('.a11y-toggle').forEach(function (btn) {
    btn.addEventListener('click', toggleMenu);
    btn.setAttribute('aria-expanded', 'false');
  });

  if (menu) {
    var closeBtn = menu.querySelector('[data-a11y-close]');
    if (closeBtn) closeBtn.addEventListener('click', function () { closeModal(menu); });

    new MutationObserver(function () {
      document.querySelectorAll('.a11y-toggle').forEach(function (btn) {
        btn.setAttribute('aria-expanded', isOpen(menu) ? 'true' : 'false');
      });
    }).observe(menu, { attributes: true, attributeFilter: ['class'] });

    menu.querySelectorAll('input[name="a11y-text"]').forEach(function (radio) {
      radio.addEventListener('change', function () {
        setPref('textSize', radio.value, 'Tamanho do texto: ' + TEXT_LABELS[radio.value]);
      });
    });

    menu.querySelectorAll('[role="switch"][data-a11y-key]').forEach(function (sw) {
      sw.addEventListener('click', function () {
        var key = sw.getAttribute('data-a11y-key');
        var spec = SWITCHES[key];
        var turnOn = window.CfeA11y.get()[key] !== spec.on;
        setPref(key, turnOn ? spec.on : spec.off, spec.label + (turnOn ? ' ativado' : ' desativado'));
      });
    });

    var reset = document.getElementById('a11y-reset-button');
    if (reset) {
      reset.addEventListener('click', function () {
        window.CfeA11y.reset();
        renderMenuState();
        announce('Preferências de acessibilidade restauradas ao padrão');
      });
    }

    var vlibras = document.getElementById('a11y-vlibras-button');
    if (vlibras) {
      vlibras.addEventListener('click', function () {
        // O widget do VLibras não abre por código de forma confiável neste ambiente (ver relatório do Bloco B):
        // o botão leva o foco ao ícone do tradutor e explica como abri-lo.
        var icon = document.querySelector('[vw-access-button]');
        if (icon) {
          icon.focus();
          announce('Foco no ícone do VLibras, no canto da tela. Pressione Enter para abrir o tradutor.');
        } else {
          announce('Tradutor VLibras indisponível neste momento.');
        }
      });
    }

    var access = document.querySelector('[vw-access-button]');
    if (access) {
      access.setAttribute('tabindex', '0');
      access.setAttribute('role', 'button');
      access.setAttribute('aria-label', 'Abrir tradutor VLibras');
      access.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          access.click();
        }
      });
    }
  }
})();
