// Chat simulado (canal WhatsApp) — máquina de estados do bot conduzindo a intenção "troca de plano".
// Estado do bot fica em localStorage (canal simulado, aceitável para o MVP — ver spec-tecnica §11.4).
// O CFE em si NUNCA guarda estado da jornada em memória: o journey_id é a única fonte de verdade
// persistida no servidor; o que fica aqui é só a "UI state machine" do bot conversacional.

const SESSION_KEY = 'cfe_whatsapp_session';

const BOT_STATES = {
  AWAITING_INTENT: 'awaiting_intent',
  AWAITING_CPF: 'awaiting_cpf',
  AWAITING_NAME: 'awaiting_name',
  AWAITING_PLAN_CHOICE: 'awaiting_plan_choice',
  COMPLETED: 'completed',
};

let session = null;

// ---------- Cliente HTTP com timeout, retry (só GET) e detecção de indisponibilidade (RNF003 / spec-funcional §8.4) ----------

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

    // 5xx é tratado como indisponibilidade, não como erro de negócio.
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
    // Erro de rede (fetch throw) ou timeout (AbortError) — sem resposta do servidor.
    throw new CfeUnavailableError(err.message);
  } finally {
    clearTimeout(timeoutId);
  }
}

/**
 * Chama o CFE. GET retenta uma vez após 2s em caso de indisponibilidade; POST/PATCH não retentam
 * automaticamente (evita duplicação — o usuário reenvia manualmente reformulando a mensagem).
 */
async function apiCall(path, { method = 'GET', body } = {}) {
  const allowRetry = method === 'GET';

  try {
    const result = await rawFetch(path, method, body, 10000);
    onCallSucceeded();
    return result;
  } catch (err) {
    if (err.isApiError) throw err; // erro de negócio — não é indisponibilidade, não retenta aqui

    if (allowRetry) {
      await sleep(2000);
      const result = await rawFetch(path, method, body, 10000); // se falhar de novo, propaga
      onCallSucceeded();
      return result;
    }

    throw err;
  }
}

function onCallSucceeded() {
  if (session && session.degraded) {
    session.degraded = false;
    showDegradedBanner(false);
    addSystemMessage('Conexão com o CFE restabelecida. ✅');
  }
}

// ---------- Sessão (persistência local) ----------

function createFreshSession() {
  return {
    state: BOT_STATES.AWAITING_INTENT,
    messages: [],
    cpf: null,
    customerId: null,
    customerName: null,
    journeyId: null,
    plans: null,
    degraded: false,
  };
}

function persistSession() {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

function loadOrInitSession() {
  const saved = localStorage.getItem(SESSION_KEY);
  if (saved) {
    session = JSON.parse(saved);
    session.messages.forEach(renderMessage);
    showDegradedBanner(!!session.degraded);
  } else {
    session = createFreshSession();
    addBotMessage('Olá! 👋 Sou o assistente virtual da Claro. Como posso te ajudar hoje?');
  }
  persistSession();
  scrollToBottom();
}

function resetSession() {
  localStorage.removeItem(SESSION_KEY);
  document.getElementById('messages').innerHTML = '';
  loadOrInitSession();
}

// ---------- Validações e heurísticas de conversa (bot simulado, sem NLP real) ----------

function detectsChangePlanIntent(text) {
  const t = text.toLowerCase();
  const mentionsPlan = t.includes('plano');
  const mentionsChangeVerb = ['troc', 'mud', 'alter', 'upgrade', 'novo'].some(k => t.includes(k));
  return mentionsPlan && mentionsChangeVerb;
}

function sanitizeCpf(text) {
  return text.replace(/\D/g, '');
}

function isValidCpfFormat(digits) {
  return /^\d{11}$/.test(digits);
}

function matchPlan(text, plans) {
  const t = text.toLowerCase().replace(/\s/g, '');
  return (plans || []).find(p =>
    t.includes(p.code.toLowerCase()) ||
    t.includes(`${p.data_gb}gb`) ||
    t.includes(String(p.data_gb))
  );
}

function formatCents(cents) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function formatTime(isoString) {
  return new Date(isoString).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

// ---------- Máquina de estados ----------

async function handleUserMessage(text) {
  addMessage('user', text);
  persistSession();

  setInputEnabled(false);
  showTyping(true);
  try {
    switch (session.state) {
      case BOT_STATES.AWAITING_INTENT:
        await handleAwaitingIntent(text);
        break;
      case BOT_STATES.AWAITING_CPF:
        await handleAwaitingCpf(text);
        break;
      case BOT_STATES.AWAITING_NAME:
        await handleAwaitingName(text);
        break;
      case BOT_STATES.AWAITING_PLAN_CHOICE:
        await handleAwaitingPlanChoice(text);
        break;
      default:
        addBotMessage('Já concluímos essa solicitação por aqui! Clique em "Reiniciar conversa" para começar de novo. 😊');
    }
  } catch (err) {
    handleUnexpectedError(err);
  } finally {
    showTyping(false);
    setInputEnabled(true);
    persistSession();
  }
}

async function handleAwaitingIntent(text) {
  if (!detectsChangePlanIntent(text)) {
    addBotMessage('No momento, só consigo ajudar com troca de plano. Digite algo como "quero trocar de plano" para continuarmos. 🙂');
    return;
  }
  addBotMessage('Perfeito! Para continuar, preciso confirmar sua identidade. Pode me informar seu CPF (só números)?');
  session.state = BOT_STATES.AWAITING_CPF;
}

async function handleAwaitingCpf(text) {
  const cpf = sanitizeCpf(text);
  if (!isValidCpfFormat(cpf)) {
    addBotMessage('Esse CPF não parece válido — preciso de 11 números, sem letras. Pode digitar novamente?');
    return;
  }
  session.cpf = cpf;

  let identity;
  try {
    identity = await apiCall('/identity/resolve', { method: 'POST', body: { channel: 'cpf', identifier: cpf } });
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    if (err.errorCode === 'cpf_not_found') {
      addBotMessage('Não encontrei esse CPF no nosso cadastro. Parece que você é novo por aqui — qual é o seu nome completo?');
      session.state = BOT_STATES.AWAITING_NAME;
      return;
    }
    return handleDomainError(err);
  }

  await onIdentityResolved(identity);
}

async function handleAwaitingName(text) {
  const name = text.trim();
  if (name.length < 3) {
    addBotMessage('Pode me passar seu nome completo, por favor?');
    return;
  }

  let identity;
  try {
    identity = await apiCall('/identity/resolve', {
      method: 'POST',
      body: { channel: 'cpf', identifier: session.cpf, full_name_hint: name },
    });
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  await onIdentityResolved(identity);
}

async function onIdentityResolved(identity) {
  session.customerId = identity.unified_customer_id;
  session.customerName = identity.customer.full_name;

  addCfeBadge('CFE — identidade resolvida · unified_customer_id vinculado');

  let journey;
  try {
    journey = await apiCall('/context/open', {
      method: 'POST',
      body: {
        customer_id: session.customerId,
        origin_channel: 'whatsapp',
        intent: 'change_plan',
        initial_step: 'identity_resolved',
        payload: {},
      },
    });
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  session.journeyId = journey.id;
  addCfeBadge(`CFE — jornada aberta · status: ${journey.status}`);

  const firstName = session.customerName.split(' ')[0];
  addBotMessage(`Prazer, ${firstName}! Identidade confirmada. ✅`);

  await presentPlans();
}

async function presentPlans() {
  let plansResponse;
  try {
    plansResponse = await apiCall('/plans');
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  session.plans = plansResponse.plans;

  const list = session.plans
    .map(p => `• ${p.name} — ${formatCents(p.monthly_price_cents)}/mês`)
    .join('\n');

  addBotMessage(`Estes são os planos disponíveis:\n${list}\n\nQual você gostaria? Pode digitar o nome (ex: "60GB").`);
  session.state = BOT_STATES.AWAITING_PLAN_CHOICE;
}

async function handleAwaitingPlanChoice(text) {
  const plan = matchPlan(text, session.plans);
  if (!plan) {
    addBotMessage('Não reconheci esse plano. Pode escolher um da lista acima, digitando por exemplo "30GB" ou "100GB"?');
    return;
  }

  try {
    await apiCall(`/context/${session.journeyId}`, {
      method: 'PATCH',
      body: { current_step: 'plan_selected', payload_merge: { selected_plan_code: plan.code } },
    });
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`CFE — jornada atualizada · plano selecionado: ${plan.name}`);

  let handoff;
  try {
    handoff = await apiCall('/handoff/generate', {
      method: 'POST',
      body: { journey_context_id: session.journeyId, target_channel: 'app' },
    });
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`CFE — deep link gerado · válido até ${formatTime(handoff.expires_at)}`);

  addBotMessage(`Prontinho! Já deixei tudo preparado para você confirmar a troca para o plano ${plan.name}. 🎉`);
  addLinkCard(handoff.deep_link_url);

  session.state = BOT_STATES.COMPLETED;
}

function handleDomainError(err) {
  console.error('Erro de negócio do CFE:', err);
  addBotMessage(`Ops, algo não deu certo (${err.errorCode || 'erro'}). Vamos tentar de novo?`);
}

function handleUnexpectedError(err) {
  console.error('Erro inesperado no chat:', err);
  addBotMessage('Ops, aconteceu algo inesperado por aqui. Pode tentar novamente?');
}

function showDegraded(err) {
  console.error('CFE indisponível:', err);
  session.degraded = true;
  showDegradedBanner(true);
  addBotMessage(
    'Estou com uma instabilidade temporária no sistema. Vou continuar te atendendo, mas algumas ' +
    'informações podem levar mais tempo para serem processadas. Você pode tentar novamente em instantes.'
  );
}

// ---------- Renderização ----------

function addMessage(sender, text) {
  const msg = { sender, text, ts: Date.now() };
  session.messages.push(msg);
  renderMessage(msg);
  scrollToBottom();
}

function addBotMessage(text) { addMessage('bot', text); }
function addSystemMessage(text) { addMessage('system', text); }
function addCfeBadge(text) { addMessage('badge', text); }
function addLinkCard(url) { addMessage('link-card', url); }

function renderMessage(msg) {
  const container = document.getElementById('messages');
  const el = document.createElement('div');

  if (msg.sender === 'badge') {
    el.className = 'cfe-badge';
    el.textContent = `⚙️ ${msg.text}`;
  } else if (msg.sender === 'system') {
    el.className = 'system-message';
    el.textContent = msg.text;
  } else if (msg.sender === 'link-card') {
    el.className = 'link-card';
    el.innerHTML = `
      <div class="link-card-title">Continuar troca de plano</div>
      <div class="link-card-subtitle">Seus dados já estão preenchidos</div>
      <a class="link-card-button" href="${msg.text}" target="_blank" rel="noopener">Continuar no App</a>
    `;
  } else {
    el.className = `bubble ${msg.sender === 'user' ? 'bubble-user' : 'bubble-bot'}`;
    el.textContent = msg.text;
  }

  container.appendChild(el);
}

function scrollToBottom() {
  const container = document.getElementById('messages');
  container.scrollTop = container.scrollHeight;
}

function showTyping(visible) {
  document.getElementById('typing-indicator').classList.toggle('hidden', !visible);
  if (visible) scrollToBottom();
}

function showDegradedBanner(visible) {
  document.getElementById('degraded-banner').classList.toggle('hidden', !visible);
}

function setInputEnabled(enabled) {
  document.getElementById('message-input').disabled = !enabled;
  document.getElementById('send-button').disabled = !enabled;
}

// ---------- Bootstrap ----------

document.addEventListener('DOMContentLoaded', () => {
  loadOrInitSession();

  const form = document.getElementById('chat-form');
  const input = document.getElementById('message-input');

  form.addEventListener('submit', event => {
    event.preventDefault();
    const text = input.value.trim();
    if (!text) return;
    input.value = '';
    handleUserMessage(text);
  });

  document.getElementById('reset-button').addEventListener('click', () => {
    if (confirm('Reiniciar a conversa? O histórico atual será perdido.')) {
      resetSession();
    }
  });
});
