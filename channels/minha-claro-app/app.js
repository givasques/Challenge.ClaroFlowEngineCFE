// App Minha Claro simulado — resolve o token do deep link e conduz a confirmação da troca de plano (UC06).
// Sem estado de "conversa" aqui (diferente do chat): o fluxo é linear — login -> resolver token -> confirmar/cancelar.

const state = {
  token: null,
  identifier: null,
  journeyId: null,
  intent: null,
  lastFailedAction: null,
};

// ---------- Cliente HTTP com timeout, retry (só GET) e detecção de indisponibilidade (RNF003 / spec-funcional §8.4) ----------
// Mesma lógica usada em channels/whatsapp-sim/app.js — duplicada de propósito: cada canal simulado é
// uma página estática independente, sem um bundler/módulo compartilhado entre elas neste MVP.

class CfeUnavailableError extends Error {}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function rawFetch(path, method, body, timeoutMs) {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const res = await fetch(`${CFE_CONFIG.apiBaseUrl}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        'X-Channel-Token': CFE_CONFIG.channelToken,
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: controller.signal,
    });

    if (res.status >= 500) {
      throw new CfeUnavailableError(`HTTP ${res.status}`);
    }

    const data = await res.json().catch(() => null);

    if (!res.ok) {
      const err = new Error((data && data.message) || `HTTP ${res.status}`);
      err.isApiError = true;
      err.status = res.status;
      err.errorCode = data && data.error_code;
      err.details = data && data.details;
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

async function apiCall(path, { method = 'GET', body } = {}) {
  const allowRetry = method === 'GET';
  try {
    return await rawFetch(path, method, body, 10000);
  } catch (err) {
    if (err.isApiError) throw err;
    if (allowRetry) {
      await sleep(2000);
      return await rawFetch(path, method, body, 10000); // se falhar de novo, propaga
    }
    throw err;
  }
}

// ---------- Navegação entre telas ----------

function showScreen(name) {
  document.querySelectorAll('.screen').forEach(el => el.classList.add('hidden'));
  document.getElementById(`screen-${name}`).classList.remove('hidden');
}

// ---------- Formatação ----------

function formatCents(cents) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function formatCpf(cpf) {
  return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
}

function formatDateShort(isoDate) {
  const [y, m, d] = isoDate.split('-');
  return `${d}/${m}/${y}`;
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// ---------- Fluxo ----------

function handleLoginSubmit(event) {
  event.preventDefault();
  const username = document.getElementById('login-username').value.trim();
  const password = document.getElementById('login-password').value.trim();
  const errorEl = document.getElementById('login-error');

  // Login mockado — valida só formato mínimo, não confere senha contra base real (ver badge na tela).
  if (username.length < 3) {
    errorEl.textContent = 'Nome de usuário deve ter no mínimo 3 caracteres.';
    errorEl.classList.remove('hidden');
    return;
  }

  if (password.length < 6) {
    errorEl.textContent = 'Senha deve ter no mínimo 6 caracteres.';
    errorEl.classList.remove('hidden');
    return;
  }

  errorEl.classList.add('hidden');
  state.identifier = username;
  attemptResolve();
}

async function attemptResolve() {
  state.lastFailedAction = attemptResolve;
  showScreen('resolving');

  try {
    const data = await apiCall(
      `/context/resolve?token=${encodeURIComponent(state.token)}&identifier=${encodeURIComponent(state.identifier)}`
    );
    state.journeyId = data.journey_context.id;
    state.intent = data.journey_context.intent;

    if (state.intent === 'dispute_charge') {
      renderDisputeConfirmation(data);
      showScreen('dispute-confirmation');
    } else {
      renderConfirmation(data);
      showScreen('confirmation');
    }
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      console.error('CFE indisponível ao resolver token:', err);
      showScreen('unavailable');
      return;
    }
    renderSessionExpired(err);
    showScreen('session-expired');
  }
}

function renderConfirmation(data) {
  const { customer, plan_details: planDetails } = data;

  document.getElementById('customer-name').textContent = customer.full_name;
  document.getElementById('customer-cpf').textContent = formatCpf(customer.cpf);

  const current = planDetails && planDetails.current_plan;
  const selected = planDetails && planDetails.selected_plan;

  document.getElementById('current-plan').textContent = current
    ? `${current.name} — ${formatCents(current.monthly_price_cents)}/mês`
    : 'Não informado';

  document.getElementById('selected-plan').textContent = selected
    ? `${selected.name} — ${formatCents(selected.monthly_price_cents)}/mês`
    : 'Não informado';
}

// ---------- Tela de contestação de cobrança (ETAPA 2, Passo C) ----------

function renderDisputeConfirmation(data) {
  const { customer, journey_context: journeyContext, invoice_details: invoiceDetails } = data;

  document.getElementById('dispute-customer-name').textContent = customer.full_name;
  document.getElementById('dispute-customer-cpf').textContent = formatCpf(customer.cpf);

  document.getElementById('dispute-invoice-label').textContent = invoiceDetails ? invoiceDetails.reference_label : 'Não informado';
  document.getElementById('dispute-invoice-due').textContent = invoiceDetails ? formatDateShort(invoiceDetails.due_date) : '—';
  document.getElementById('dispute-invoice-total').textContent = invoiceDetails ? formatCents(invoiceDetails.total_cents) : '—';

  document.getElementById('dispute-description').textContent =
    (journeyContext.payload && journeyContext.payload.customer_description) || 'Não informado';

  const itemsList = document.getElementById('dispute-items-list');
  itemsList.innerHTML = '';

  const items = (invoiceDetails && invoiceDetails.items) || [];
  for (const item of items) {
    const row = document.createElement('label');
    row.className = 'dispute-item-row';
    row.innerHTML = `
      <input type="checkbox" class="dispute-item-checkbox" data-item-id="${escapeHtml(item.id)}" />
      <span class="dispute-item-desc">${escapeHtml(item.description)}</span>
      <span class="dispute-item-amount">${formatCents(item.amount_cents)}</span>
    `;
    itemsList.appendChild(row);
  }

  itemsList.querySelectorAll('.dispute-item-checkbox').forEach(cb => {
    cb.addEventListener('change', updateFormalizeButtonState);
  });
  updateFormalizeButtonState();
}

function updateFormalizeButtonState() {
  const anyChecked = document.querySelectorAll('.dispute-item-checkbox:checked').length > 0;
  document.getElementById('formalize-dispute-button').disabled = !anyChecked;
}

function generateProtocolNumber() {
  const year = new Date().getFullYear();
  const random = Math.random().toString(36).slice(2, 8).toUpperCase();
  return `DISPUTE-${year}-${random}`;
}

async function formalizeDispute() {
  const contestedItemIds = Array.from(document.querySelectorAll('.dispute-item-checkbox:checked'))
    .map(cb => cb.dataset.itemId);
  if (contestedItemIds.length === 0) return;

  const protocolNumber = generateProtocolNumber();
  state.lastFailedAction = formalizeDispute;
  showScreen('closing');

  try {
    await apiCall(`/context/${state.journeyId}`, {
      method: 'PATCH',
      body: {
        current_step: 'dispute_formalized',
        payload_merge: { contested_item_ids: contestedItemIds, protocol_number: protocolNumber },
      },
    });
    await apiCall(`/context/${state.journeyId}/close`, { method: 'POST', body: { outcome: 'concluded', channel: 'app' } });
    renderCompletedScreen(protocolNumber);
    showScreen('completed');
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      console.error('CFE indisponível ao formalizar contestação:', err);
      showScreen('unavailable');
      return;
    }
    console.error('Erro de negócio ao formalizar contestação:', err);
    alert(`Não foi possível concluir agora: ${err.message}`);
    showScreen('dispute-confirmation');
  }
}

function renderCompletedScreen(protocolNumber) {
  const protocolEl = document.getElementById('completed-protocol');

  if (state.intent === 'dispute_charge') {
    document.getElementById('completed-title').textContent = 'Contestação registrada!';
    document.getElementById('completed-message').textContent =
      'Um analista vai revisar em até 5 dias úteis. Você será notificado por WhatsApp.';
    protocolEl.textContent = `Protocolo: ${protocolNumber}`;
    protocolEl.classList.remove('hidden');
  } else {
    document.getElementById('completed-title').textContent = 'Troca de plano confirmada!';
    document.getElementById('completed-message').textContent = 'Sua solicitação foi concluída com sucesso.';
    protocolEl.classList.add('hidden');
  }
}

function renderSessionExpired(err) {
  const byErrorCode = {
    token_not_found: {
      title: 'Link inválido',
      message: 'Este link não é válido. Verifique se copiou o endereço corretamente ou inicie uma nova conversa.',
    },
    token_expired: {
      title: 'Link expirado',
      message: 'Sua sessão expirou. Por segurança, links de retomada são válidos por 30 minutos.',
    },
    token_already_used: {
      title: 'Link já utilizado',
      message: 'Esta sessão já foi retomada anteriormente. Se precisar continuar, inicie uma nova conversa no WhatsApp.',
    },
    journey_expired: {
      title: 'Solicitação expirada',
      message: 'Sua solicitação expirou por inatividade. Iniciamos uma nova sessão para você continuar.',
    },
    journey_closed: {
      title: 'Solicitação finalizada',
      message: 'Esta solicitação já foi finalizada. Se precisar de algo, é só nos chamar novamente.',
    },
  };
  const fallback = { title: 'Sessão expirada', message: 'Não foi possível recuperar sua sessão.' };
  const { title, message } = byErrorCode[err.errorCode] || fallback;

  document.getElementById('session-expired-title').textContent = title;
  document.getElementById('session-expired-message').textContent = message;
}

async function closeJourney(outcome) {
  state.lastFailedAction = () => closeJourney(outcome);
  showScreen('closing');

  try {
    await apiCall(`/context/${state.journeyId}/close`, {
      method: 'POST',
      body: { outcome, channel: 'app' },
    });
    if (outcome === 'concluded') renderCompletedScreen(null);
    showScreen(outcome === 'concluded' ? 'completed' : 'cancelled');
  } catch (err) {
    if (err instanceof CfeUnavailableError) {
      console.error('CFE indisponível ao encerrar jornada:', err);
      showScreen('unavailable');
      return;
    }
    console.error('Erro de negócio ao encerrar jornada:', err);
    alert(`Não foi possível concluir agora: ${err.message}`);
    showScreen('confirmation');
  }
}

// ---------- Bootstrap ----------

document.addEventListener('DOMContentLoaded', () => {
  const params = new URLSearchParams(window.location.search);
  state.token = params.get('token');

  if (!state.token) {
    showScreen('no-token');
    return;
  }

  showScreen('login');

  document.getElementById('login-form').addEventListener('submit', handleLoginSubmit);
  document.getElementById('confirm-button').addEventListener('click', () => closeJourney('concluded'));
  document.getElementById('cancel-button').addEventListener('click', () => closeJourney('abandoned'));
  document.getElementById('formalize-dispute-button').addEventListener('click', () => formalizeDispute());
  document.getElementById('cancel-dispute-button').addEventListener('click', () => closeJourney('abandoned'));
  document.getElementById('retry-button').addEventListener('click', () => state.lastFailedAction && state.lastFailedAction());
  document.getElementById('forgot-password-link').addEventListener('click', event => event.preventDefault()); // link visual, sem ação real (login é mockado)
});
