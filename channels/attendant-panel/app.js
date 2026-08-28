// Painel do atendente — busca por CPF/telefone, exibe dados do cliente, status da jornada e histórico
// completo (UC09). Somente leitura: nenhuma escrita é feita no CFE a partir daqui.

const EVENT_ICONS = {
  journey_started: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><circle cx="12" cy="12" r="9.5" stroke="currentColor" stroke-width="1.8"/><path d="M10 8.5l6 3.5-6 3.5v-7z" fill="currentColor"/></svg>',
  journey_reopen_attempted: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><path d="M20 11A8 8 0 104.5 14.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M20 5v6h-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  identity_resolved: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><circle cx="9" cy="8" r="3.2" stroke="currentColor" stroke-width="1.8"/><path d="M3.5 19c1.3-3.1 3.6-4.7 5.5-4.7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M14.5 13l2 2 4-4.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  step_updated: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><path d="M16.5 3.5a2.1 2.1 0 013 3L7 19l-4 1 1-4L16.5 3.5z" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  deep_link_generated: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><path d="M9.5 14.5l5-5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M11 7l1-1a3.5 3.5 0 015 5l-1 1" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M13 17l-1 1a3.5 3.5 0 01-5-5l1-1" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  journey_resumed: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><path d="M8 16l-4-4 4-4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><path d="M4 12h9a5 5 0 015 5v2" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  journey_closed: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><circle cx="12" cy="12" r="9.5" stroke="currentColor" stroke-width="1.8"/><path d="M8 12.5l2.5 2.5 5-5.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  journey_expired: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><circle cx="12" cy="12" r="9.5" stroke="currentColor" stroke-width="1.8"/><path d="M12 7v5l3.5 2" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  journey_abandoned: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><line x1="6" y1="6" x2="18" y2="18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><line x1="18" y1="6" x2="6" y2="18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  panel_accessed: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="15" height="15"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.8"/></svg>',
};

// Classe de cor por tipo de evento — ver variáveis --event-* em styles.css.
const EVENT_COLOR_CLASS = {
  journey_started: 'event-start',
  journey_reopen_attempted: 'event-start',
  identity_resolved: 'event-identity',
  step_updated: 'event-update',
  deep_link_generated: 'event-handoff',
  journey_resumed: 'event-resumed',
  journey_closed: 'event-closed',
  journey_expired: 'event-expired',
  journey_abandoned: 'event-abandoned',
  panel_accessed: 'event-panel',
};

const EVENT_LABELS = {
  journey_started: 'Jornada iniciada',
  journey_reopen_attempted: 'Reabertura tentada',
  identity_resolved: 'Identidade resolvida',
  step_updated: 'Etapa atualizada',
  deep_link_generated: 'Deep link gerado',
  journey_resumed: 'Jornada retomada em outro canal',
  journey_closed: 'Jornada encerrada',
  journey_expired: 'Jornada expirada por inatividade',
  journey_abandoned: 'Jornada abandonada',
  panel_accessed: 'Painel consultou esta jornada',
};

const STATUS_LABELS = {
  open: 'Em andamento',
  concluded: 'Concluída',
  abandoned: 'Abandonada',
  expired: 'Expirada',
};

// Ícones pequenos por canal, usados na timeline e no bloco de status.
const CHANNEL_ICONS = {
  whatsapp: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="11" height="11"><path d="M21 11.5a8.5 8.5 0 01-12.3 7.6L4 20l1-4.5A8.5 8.5 0 1121 11.5z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/></svg>',
  app: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="11" height="11"><rect x="7" y="2" width="10" height="20" rx="2" stroke="currentColor" stroke-width="1.8"/><line x1="11" y1="18.5" x2="13" y2="18.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  call: '<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="11" height="11"><path d="M4 5c0-1 1-2 2-2h2l2 5-2 1.5c1 2.5 2.5 4 5 5l1.5-2 5 2v2c0 1-1 2-2 2C10 18.5 4.5 13 4 5z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/></svg>',
};
const CHANNEL_ICON_DEFAULT = '<svg viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg" width="8" height="8"><circle cx="12" cy="12" r="8"/></svg>';

const CHANNEL_LABELS = {
  whatsapp: 'WhatsApp',
  app: 'App Minha Claro',
  call: 'Central telefônica',
};

const INTENT_LABELS = {
  change_plan: 'Troca de plano',
};

const state = {
  customerId: null,
  journeyId: null,
  pollingHandle: null,
  degraded: false,
  lastUpdatedAt: null,
};

// ---------- Cliente HTTP (mesmo padrão dos outros canais — ver whatsapp-sim/app.js) ----------

class CfeUnavailableError extends Error {}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function rawFetch(path, timeoutMs) {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const res = await fetch(`${CFE_CONFIG.apiBaseUrl}${path}`, {
      headers: { 'X-Channel-Token': CFE_CONFIG.channelToken },
      signal: controller.signal,
    });

    if (res.status >= 500) throw new CfeUnavailableError(`HTTP ${res.status}`);

    const data = await res.json().catch(() => null);

    if (!res.ok) {
      const err = new Error((data && data.message) || `HTTP ${res.status}`);
      err.isApiError = true;
      err.status = res.status;
      err.errorCode = data && data.error_code;
      throw err;
    }

    return data;
  } catch (err) {
    if (err.isApiError || err instanceof CfeUnavailableError) throw err;
    throw new CfeUnavailableError(err.message);
  } finally {
    clearTimeout(timeoutId);
  }
}

// Todo endpoint do painel é GET — retenta uma vez após 2s antes de considerar indisponível.
async function apiCall(path) {
  try {
    const result = await rawFetch(path, 10000);
    clearDegraded();
    return result;
  } catch (err) {
    if (err.isApiError) throw err;
    await sleep(2000);
    const result = await rawFetch(path, 10000); // se falhar de novo, propaga
    clearDegraded();
    return result;
  }
}

function clearDegraded() {
  if (state.degraded) {
    state.degraded = false;
    showDegradedBanner(false);
  }
}

// ---------- Formatação ----------

function formatCents(cents) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function formatCpf(cpf) {
  return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
}

function formatPhone(digits) {
  // DDI + DDD + número (12-13 dígitos) — formata só a parte final para leitura (DDD) NNNNN-NNNN.
  const local = digits.slice(-11);
  return local.replace(/(\d{2})(\d{5})(\d{4})/, '($1) $2-$3');
}

function relativeTime(isoString) {
  const diffSec = Math.floor((Date.now() - new Date(isoString).getTime()) / 1000);
  if (diffSec < 5) return 'agora mesmo';
  if (diffSec < 60) return `há ${diffSec}s`;
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `há ${diffMin} min`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `há ${diffHour}h`;
  return `há ${Math.floor(diffHour / 24)}d`;
}

function formatDateShort(isoString) {
  return new Date(isoString).toLocaleDateString('pt-BR');
}

/** "Cliente desde" em granularidade grosseira, para o badge no topo do bloco (ETAPA 2, Passo B, item 5.2). */
function formatTenure(isoString) {
  const since = new Date(isoString);
  const now = new Date();
  let months = (now.getFullYear() - since.getFullYear()) * 12 + (now.getMonth() - since.getMonth());
  if (now.getDate() < since.getDate()) months--;

  if (months < 1) return 'Cliente novo';
  if (months < 12) return `Cliente há ${months} ${months === 1 ? 'mês' : 'meses'}`;
  const years = Math.floor(months / 12);
  return `Cliente há ${years} ${years === 1 ? 'ano' : 'anos'}`;
}

function detectSearchChannel(digits) {
  if (digits.length === 11) return 'cpf';
  if (digits.length === 12 || digits.length === 13) return 'whatsapp';
  return null;
}

function getInitials(fullName) {
  const parts = fullName.trim().split(/\s+/);
  const first = parts[0]?.[0] || '';
  const last = parts.length > 1 ? parts[parts.length - 1][0] : '';
  return (first + last).toUpperCase();
}

// ---------- Busca ----------

async function handleSearch(event) {
  event.preventDefault();
  stopPolling();
  hideAllResultBlocks();
  document.getElementById('empty-state').classList.add('hidden');

  const raw = document.getElementById('search-input').value.trim();
  const digits = raw.replace(/\D/g, '');
  const channel = detectSearchChannel(digits);

  if (!channel) {
    showMessage('Digite um CPF (11 dígitos) ou telefone com DDI+DDD (12-13 dígitos).', 'error');
    return;
  }

  showMessage('Buscando...', 'info');

  try {
    const identity = await apiCall(`/identity/resolve?channel=${channel}&identifier=${digits}`);
    state.customerId = identity.unified_customer_id;
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      showMessage('Não foi possível buscar agora — sistema de contexto indisponível. Tente novamente em instantes.', 'error');
      return;
    }
    if (err.errorCode === 'identity_not_found') {
      showMessage('Cliente não localizado.', 'error');
      return;
    }
    showMessage(`Não foi possível buscar: ${err.message}`, 'error');
    return;
  }

  await loadCustomerJourney();
}

async function loadCustomerJourney() {
  let data;
  try {
    data = await apiCall(`/context/customer/${state.customerId}?include_history=true`);
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      showMessage('Sistema de contexto indisponível — tente novamente em instantes.', 'error');
      return;
    }
    showMessage(`Não foi possível carregar a jornada: ${err.message}`, 'error');
    return;
  }

  clearMessage();

  renderPreviousJourneys(data.recent_journeys || []);

  if (!data.journey) {
    renderCustomerBlock(null);
    showMessage('Sem jornadas em andamento para este cliente.', 'info');
    return;
  }

  state.journeyId = data.journey.id;
  renderCustomerBlock(data.journey.customer);
  renderJourneyStatus(data.journey);

  await refreshTransitions();

  if (data.journey.status === 'open') {
    startPolling();
  }
}

// ---------- Polling ----------

function startPolling() {
  stopPolling();
  state.pollingHandle = setInterval(pollJourney, CFE_CONFIG.pollingIntervalMs);
}

function stopPolling() {
  if (state.pollingHandle) {
    clearInterval(state.pollingHandle);
    state.pollingHandle = null;
  }
}

async function pollJourney() {
  if (!state.journeyId) return;

  try {
    const journey = await apiCall(`/context/${state.journeyId}`);
    state.lastUpdatedAt = new Date();
    renderCustomerBlock(journey.customer);
    renderJourneyStatus(journey);
    await refreshTransitions();

    if (journey.status !== 'open') {
      stopPolling(); // spec-tecnica §7.4: jornada em status final -> para o polling
    }
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      console.error('Polling falhou — CFE indisponível:', err);
      state.degraded = true;
      showDegradedBanner(true);
      return; // mantém os últimos dados carregados visíveis, só avisa
    }
    console.error('Erro inesperado no polling:', err);
  }
}

async function refreshTransitions() {
  try {
    const data = await apiCall(`/context/${state.journeyId}/transitions`);
    renderTimeline(data.transitions);
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      state.degraded = true;
      showDegradedBanner(true);
    }
  }
}

// ---------- Renderização ----------

function hideAllResultBlocks() {
  document.getElementById('customer-block').classList.add('hidden');
  document.getElementById('status-block').classList.add('hidden');
  document.getElementById('history-block').classList.add('hidden');
  document.getElementById('interactions-summary-block').classList.add('hidden');
  document.getElementById('previous-journeys-block').classList.add('hidden');
  clearMessage();
}

function showMessage(text, kind) {
  const el = document.getElementById('search-message');
  el.textContent = text;
  el.className = `search-message ${kind}`;
  el.classList.remove('hidden');
}

function clearMessage() {
  document.getElementById('search-message').classList.add('hidden');
}

function renderCustomerBlock(customer) {
  const block = document.getElementById('customer-block');
  if (!customer) {
    block.classList.add('hidden');
    renderInteractionsSummary(null);
    return;
  }

  document.getElementById('customer-avatar').textContent = getInitials(customer.full_name);
  document.getElementById('customer-name').textContent = customer.full_name;
  document.getElementById('customer-cpf').textContent = formatCpf(customer.cpf);
  document.getElementById('customer-phone').textContent = customer.phone ? formatPhone(customer.phone) : 'Não informado';
  document.getElementById('customer-plan').textContent = customer.current_plan
    ? `${customer.current_plan.name} — ${formatCents(customer.current_plan.monthly_price_cents)}/mês`
    : 'Não informado';

  // Campos agregados do cliente (ETAPA 2, Passo B, item 5.2)
  document.getElementById('customer-tenure-badge').textContent = formatTenure(customer.customer_since);
  document.getElementById('customer-since').textContent = formatDateShort(customer.customer_since);
  document.getElementById('customer-preferred-channel').textContent = customer.preferred_channel
    ? (CHANNEL_LABELS[customer.preferred_channel] || customer.preferred_channel)
    : 'Não informado';

  block.classList.remove('hidden');
  renderInteractionsSummary(customer.journey_counts);
}

function renderInteractionsSummary(counts) {
  const block = document.getElementById('interactions-summary-block');
  if (!counts || counts.total === 0) {
    block.classList.add('hidden');
    return;
  }

  document.getElementById('interactions-summary-text').textContent =
    `${counts.total} jornada${counts.total === 1 ? '' : 's'} no total — ` +
    `${counts.concluded} concluída${counts.concluded === 1 ? '' : 's'}, ` +
    `${counts.abandoned} abandonada${counts.abandoned === 1 ? '' : 's'}, ` +
    `${counts.expired} expirada${counts.expired === 1 ? '' : 's'}.`;

  block.classList.remove('hidden');
}

function renderJourneyStatus(journey) {
  const block = document.getElementById('status-block');

  const badge = document.getElementById('journey-status-badge');
  badge.textContent = STATUS_LABELS[journey.status] || journey.status;
  badge.className = `status-badge status-${journey.status}`;

  document.getElementById('journey-origin-channel').textContent = journey.origin_channel;
  document.getElementById('journey-origin-channel-icon').innerHTML = CHANNEL_ICONS[journey.origin_channel] || CHANNEL_ICON_DEFAULT;
  document.getElementById('journey-intent').textContent = journey.intent;
  document.getElementById('journey-last-update').textContent = relativeTime(journey.updated_at);

  block.classList.remove('hidden');
}

function renderTimeline(transitions) {
  const block = document.getElementById('history-block');
  const list = document.getElementById('history-list');
  list.innerHTML = '';

  // "Canal atual" (spec-funcional §8.3): o canal da transição mais recente, não o de origem.
  // Se coincidir com o canal de origem, esconde o segundo chip + seta (evita "WhatsApp -> WhatsApp").
  if (transitions.length > 0) {
    const current = transitions[0].channel;
    const origin = document.getElementById('journey-origin-channel').textContent;
    document.getElementById('journey-current-channel').textContent = current;
    document.getElementById('journey-current-channel-icon').innerHTML = CHANNEL_ICONS[current] || CHANNEL_ICON_DEFAULT;
    document.getElementById('channel-flow-arrow').classList.toggle('hidden', current === origin);
    document.getElementById('journey-current-channel-chip').classList.toggle('hidden', current === origin);
  }

  appendTimelineItems(list, transitions);

  block.classList.remove('hidden');
}

function appendTimelineItems(list, transitions) {
  for (const t of transitions) {
    const li = document.createElement('li');
    li.className = 'history-item';
    const colorClass = EVENT_COLOR_CLASS[t.event_type] || 'event-closed';
    li.innerHTML = `
      <div class="history-icon history-icon--${colorClass}">${EVENT_ICONS[t.event_type] || '•'}</div>
      <div class="history-body">
        <div class="history-title">${EVENT_LABELS[t.event_type] || t.event_type}</div>
        <div class="history-description">${t.description || ''}</div>
        <div class="history-meta">${CHANNEL_ICONS[t.channel] || CHANNEL_ICON_DEFAULT} ${t.channel} · ${relativeTime(t.occurred_at)}</div>
      </div>
    `;
    list.appendChild(li);
  }
}

/** Bloco "Histórico de jornadas anteriores" (ETAPA 2, Passo B, item 5.3) — separado da timeline da jornada ativa. */
function renderPreviousJourneys(journeys) {
  const block = document.getElementById('previous-journeys-block');
  const list = document.getElementById('previous-journeys-list');
  list.innerHTML = '';

  if (!journeys || journeys.length === 0) {
    block.classList.add('hidden');
    return;
  }

  for (const j of journeys) {
    list.appendChild(buildPreviousJourneyItem(j));
  }

  block.classList.remove('hidden');
}

function buildPreviousJourneyItem(journey) {
  const li = document.createElement('li');
  li.className = 'previous-journey-item';

  const summary = document.createElement('button');
  summary.type = 'button';
  summary.className = 'previous-journey-summary';
  summary.innerHTML = `
    <span class="status-badge status-${journey.status}">${STATUS_LABELS[journey.status] || journey.status}</span>
    <span class="previous-journey-intent">${INTENT_LABELS[journey.intent] || journey.intent}</span>
    <span class="previous-journey-meta">
      ${CHANNEL_ICONS[journey.origin_channel] || CHANNEL_ICON_DEFAULT}
      ${CHANNEL_LABELS[journey.origin_channel] || journey.origin_channel} ·
      ${relativeTime(journey.updated_at)} · última etapa: ${journey.current_step}
    </span>
    <span class="previous-journey-chevron" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="14" height="14"><polyline points="6 9 12 15 18 9" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>
    </span>
  `;

  const detail = document.createElement('div');
  detail.className = 'previous-journey-detail hidden';
  let loaded = false;

  summary.addEventListener('click', async () => {
    const isExpanded = li.classList.contains('expanded');
    if (isExpanded) {
      li.classList.remove('expanded');
      detail.classList.add('hidden');
      return;
    }

    li.classList.add('expanded');
    detail.classList.remove('hidden');

    if (!loaded) {
      detail.innerHTML = '<p class="coming-soon">Carregando histórico...</p>';
      try {
        const data = await apiCall(`/context/${journey.id}/transitions`);
        detail.innerHTML = '';
        const ul = document.createElement('ul');
        ul.className = 'history-list';
        appendTimelineItems(ul, data.transitions);
        detail.appendChild(ul);
        loaded = true;
      } catch (err) {
        detail.innerHTML = '<p class="coming-soon">Não foi possível carregar o histórico desta jornada.</p>';
      }
    }
  });

  li.appendChild(summary);
  li.appendChild(detail);
  return li;
}

function showDegradedBanner(visible) {
  const banner = document.getElementById('degraded-banner');
  if (visible) {
    const ts = state.lastUpdatedAt ? state.lastUpdatedAt.toLocaleTimeString('pt-BR') : '—';
    document.getElementById('degraded-banner-text').textContent =
      `⚠️ Sistema de contexto indisponível — os dados exibidos podem estar defasados. Última atualização: ${ts}.`;
    banner.classList.remove('hidden');
  } else {
    banner.classList.add('hidden');
  }
  updatePollingFooter(visible);
}

function updatePollingFooter(degraded) {
  const dot = document.getElementById('polling-dot');
  const text = document.getElementById('polling-footer-text');
  if (degraded) {
    dot.classList.add('polling-dot-degraded');
    text.textContent = 'Aguardando reconexão...';
  } else {
    dot.classList.remove('polling-dot-degraded');
    text.textContent = 'Atualização automática ativa · a cada 4 segundos';
  }
}

// ---------- Relógio ao vivo (só visual — não afeta lógica de negócio) ----------

function updateHeaderClock() {
  const now = new Date();
  const time = now.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  const date = now.toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'short' });
  document.getElementById('header-clock').textContent = `${time} · ${date}`;
}

// ---------- Bootstrap ----------

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('search-form').addEventListener('submit', handleSearch);

  updateHeaderClock();
  setInterval(updateHeaderClock, 60000);
});
