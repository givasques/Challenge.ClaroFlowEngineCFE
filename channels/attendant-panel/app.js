// Painel do atendente — busca por CPF/telefone, exibe dados do cliente, status da jornada e histórico
// completo (UC09). Somente leitura: nenhuma escrita é feita no CFE a partir daqui.

const EVENT_ICONS = {
  journey_started: '🚀',
  journey_reopen_attempted: '🔁',
  step_updated: '✏️',
  deep_link_generated: '🔗',
  journey_resumed: '📲',
  journey_closed: '🏁',
  journey_expired: '⏱️',
  panel_accessed: '👁️',
};

const EVENT_LABELS = {
  journey_started: 'Jornada iniciada',
  journey_reopen_attempted: 'Reabertura tentada',
  step_updated: 'Etapa atualizada',
  deep_link_generated: 'Deep link gerado',
  journey_resumed: 'Jornada retomada em outro canal',
  journey_closed: 'Jornada encerrada',
  journey_expired: 'Jornada expirada por inatividade',
  panel_accessed: 'Painel consultou esta jornada',
};

const STATUS_LABELS = {
  open: 'Em andamento',
  concluded: 'Concluída',
  abandoned: 'Abandonada',
  expired: 'Expirada',
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

function detectSearchChannel(digits) {
  if (digits.length === 11) return 'cpf';
  if (digits.length === 12 || digits.length === 13) return 'whatsapp';
  return null;
}

// ---------- Busca ----------

async function handleSearch(event) {
  event.preventDefault();
  stopPolling();
  hideAllResultBlocks();

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

  if (!data.journey) {
    renderCustomerBlock(null);
    showMessage('Sem jornadas em andamento para este cliente.', 'info');
    renderHistoryOnly(data.recent_journeys || []);
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
    return;
  }

  document.getElementById('customer-name').textContent = customer.full_name;
  document.getElementById('customer-cpf').textContent = formatCpf(customer.cpf);
  document.getElementById('customer-phone').textContent = customer.phone ? formatPhone(customer.phone) : 'Não informado';
  document.getElementById('customer-plan').textContent = customer.current_plan
    ? `${customer.current_plan.name} — ${formatCents(customer.current_plan.monthly_price_cents)}/mês`
    : 'Não informado';

  block.classList.remove('hidden');
}

function renderJourneyStatus(journey) {
  const block = document.getElementById('status-block');

  const badge = document.getElementById('journey-status-badge');
  badge.textContent = STATUS_LABELS[journey.status] || journey.status;
  badge.className = `status-badge status-${journey.status}`;

  document.getElementById('journey-origin-channel').textContent = journey.origin_channel;
  document.getElementById('journey-intent').textContent = journey.intent;
  document.getElementById('journey-last-update').textContent = relativeTime(journey.updated_at);

  block.classList.remove('hidden');
}

function renderTimeline(transitions) {
  const block = document.getElementById('history-block');
  const list = document.getElementById('history-list');
  list.innerHTML = '';

  // "Canal atual" (spec-funcional §8.3): o canal da transição mais recente, não o de origem.
  if (transitions.length > 0) {
    document.getElementById('journey-current-channel').textContent = transitions[0].channel;
  }

  for (const t of transitions) {
    const li = document.createElement('li');
    li.className = 'history-item';
    li.innerHTML = `
      <div class="history-icon">${EVENT_ICONS[t.event_type] || '•'}</div>
      <div class="history-body">
        <div class="history-title">${EVENT_LABELS[t.event_type] || t.event_type}</div>
        <div class="history-description">${t.description || ''}</div>
        <div class="history-meta">${t.channel} · ${relativeTime(t.occurred_at)}</div>
      </div>
    `;
    list.appendChild(li);
  }

  block.classList.remove('hidden');
}

function renderHistoryOnly(recentJourneys) {
  const block = document.getElementById('history-block');
  const list = document.getElementById('history-list');
  list.innerHTML = '';

  if (recentJourneys.length === 0) {
    block.classList.add('hidden');
    return;
  }

  for (const j of recentJourneys) {
    const li = document.createElement('li');
    li.className = 'history-item';
    li.innerHTML = `
      <div class="history-icon">${EVENT_ICONS.journey_closed}</div>
      <div class="history-body">
        <div class="history-title">${j.intent} — ${STATUS_LABELS[j.status] || j.status}</div>
        <div class="history-description">Canal de origem: ${j.origin_channel}</div>
        <div class="history-meta">${relativeTime(j.updated_at)}</div>
      </div>
    `;
    list.appendChild(li);
  }

  block.classList.remove('hidden');
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
}

// ---------- Bootstrap ----------

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('search-form').addEventListener('submit', handleSearch);
});
