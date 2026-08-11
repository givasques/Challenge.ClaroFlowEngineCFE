// App Minha Claro simulado — resolve o token do deep link e conduz a confirmação da troca de plano (UC06).
// Sem estado de "conversa" aqui (diferente do chat): o fluxo é linear — login -> resolver token -> confirmar/cancelar.

const state = {
  token: null,
  identifier: null,
  journeyId: null,
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

// ---------- Fluxo ----------

function handleLoginSubmit(event) {
  event.preventDefault();
  const username = document.getElementById('login-username').value.trim();
  const password = document.getElementById('login-password').value.trim();
  const errorEl = document.getElementById('login-error');

  // Login mockado — qualquer credencial "com cara de credencial" serve; não valida senha de verdade.
  if (username.length < 3 || password.length < 3) {
    errorEl.textContent = 'Usuário e senha devem ter pelo menos 3 caracteres.';
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
    renderConfirmation(data);
    showScreen('confirmation');
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

function renderSessionExpired(err) {
  const messages = {
    token_not_found: 'Não encontramos essa sessão. O link pode estar incorreto.',
    token_expired: 'Esse link expirou. Volte ao WhatsApp e peça um novo.',
    token_already_used: 'Esse link já foi utilizado. Se ainda precisar continuar, peça um novo no WhatsApp.',
    journey_expired: 'Sua sessão expirou por inatividade. Volte ao WhatsApp para começar de novo.',
    journey_closed: 'Essa solicitação já foi concluída ou cancelada anteriormente.',
  };
  document.getElementById('session-expired-message').textContent =
    messages[err.errorCode] || 'Não foi possível recuperar sua sessão.';
}

async function closeJourney(outcome) {
  state.lastFailedAction = () => closeJourney(outcome);
  showScreen('closing');

  try {
    await apiCall(`/context/${state.journeyId}/close`, {
      method: 'POST',
      body: { outcome, channel: 'app' },
    });
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
  document.getElementById('retry-button').addEventListener('click', () => state.lastFailedAction && state.lastFailedAction());
});
