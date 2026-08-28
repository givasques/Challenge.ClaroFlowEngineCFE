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

// SVGs reutilizados na renderização
const SVG_CHECKS_READ = `<svg class="bubble-checks" viewBox="0 0 16 15" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path d="M15.01 3.316l-.478-.372a.365.365 0 0 0-.51.063L8.666 9.879a.32.32 0 0 1-.484.033l-.358-.325a.319.319 0 0 0-.484.032l-.378.483a.418.418 0 0 0 .036.541l1.32 1.266c.143.14.361.125.484-.033l6.272-8.048a.366.366 0 0 0-.064-.512zm-4.1 0l-.478-.372a.365.365 0 0 0-.51.063L4.566 9.879a.32.32 0 0 1-.484.033L1.891 7.769a.366.366 0 0 0-.515.006l-.423.433a.364.364 0 0 0 .006.514l3.258 3.185c.143.14.361.125.484-.033l6.272-8.048a.365.365 0 0 0-.063-.51z"/></svg>`;

const SVG_CFE_ICON = `<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M12 15a3 3 0 100-6 3 3 0 000 6z" stroke="currentColor" stroke-width="2"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-2 2 2 2 0 01-2-2v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83 0 2 2 0 010-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 01-2-2 2 2 0 012-2h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 010-2.83 2 2 0 012.83 0l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 012-2 2 2 0 012 2v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 0 2 2 0 010 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 012 2 2 2 0 01-2 2h-.09a1.65 1.65 0 00-1.51 1z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>`;

const SVG_CLOCK = `<svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" width="10" height="10"><circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/><polyline points="12 6 12 12 16 14" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>`;

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
    if (err.isApiError) throw err;

    if (allowRetry) {
      await sleep(2000);
      const result = await rawFetch(path, method, body, 10000);
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
    addSystemMessage('Conexão com o CFE restabelecida.');
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

async function loadOrInitSession() {
  const saved = localStorage.getItem(SESSION_KEY);
  if (saved) {
    session = JSON.parse(saved);
    // Compatibilidade com sessões antigas que não tinham timestamp
    session.messages.forEach(m => { if (!m.ts) m.ts = Date.now(); });
    session.messages.forEach((m, i) => renderMessage(m, session.messages[i - 1]));
    showDegradedBanner(!!session.degraded);
  } else {
    session = createFreshSession();
    await botSay('Olá! 👋 Sou o assistente virtual da Claro. Como posso te ajudar hoje?');
  }
  persistSession();
  scrollToBottom();
}

async function resetSession() {
  if (session && session.journeyId) {
    try {
      await apiCall(`/context/${session.journeyId}/close`, {
        method: 'POST',
        body: { outcome: 'abandoned', channel: 'whatsapp', reason: 'Reiniciado pelo usuário' },
      });
    } catch (err) {
      // Resiliente de propósito: CFE indisponível ou jornada já fechada não pode travar o reinício da conversa.
      console.error('Falha ao encerrar jornada anterior ao reiniciar:', err);
    }
  }

  localStorage.removeItem(SESSION_KEY);
  // Preserva o separador "HOJE", limpa só as mensagens
  const container = document.getElementById('messages');
  const dayLabel = container.querySelector('.day-separator');
  container.innerHTML = '';
  if (dayLabel) container.appendChild(dayLabel);
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

function formatMessageTime(ts) {
  return new Date(ts).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

// ---------- Máquina de estados ----------

async function handleUserMessage(text) {
  addMessage('user', text);
  persistSession();

  setInputEnabled(false);
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
        await botSay('Já concluímos essa solicitação por aqui! Toque no menu ⋮ e escolha "Reiniciar conversa" para começar de novo. 😊');
    }
  } catch (err) {
    handleUnexpectedError(err);
  } finally {
    showTyping(false);
    showProcessing(false);
    setInputEnabled(true);
    persistSession();
  }
}

// ---------- Indicadores de processamento simulado (reforço de realismo da demo) ----------

function randomBetween(minMs, maxMs) {
  return minMs + Math.random() * (maxMs - minMs);
}

/** Indicador "bot digitando" — usado antes de mensagens do bot não ligadas a uma chamada de API. */
async function botSay(text) {
  showTyping(true);
  await sleep(randomBetween(800, 1500));
  showTyping(false);
  addBotMessage(text);
}

/** Indicador "processando" — usado ao redor de chamadas à API, com texto contextual. */
async function withProcessing(text, fn) {
  showProcessing(text);
  try {
    return await fn();
  } finally {
    showProcessing(false);
  }
}

async function handleAwaitingIntent(text) {
  if (!detectsChangePlanIntent(text)) {
    await botSay('No momento, só consigo ajudar com troca de plano. Digite algo como "quero trocar de plano" para continuarmos. 🙂');
    return;
  }
  await botSay('Perfeito! Para continuar, preciso confirmar sua identidade. Pode me informar seu CPF (só números)?');
  session.state = BOT_STATES.AWAITING_CPF;
}

async function handleAwaitingCpf(text) {
  const cpf = sanitizeCpf(text);
  if (!isValidCpfFormat(cpf)) {
    await botSay('Esse CPF não parece válido — preciso de 11 números, sem letras. Pode digitar novamente?');
    return;
  }
  session.cpf = cpf;

  let identity;
  try {
    identity = await withProcessing('verificando cadastro...', () =>
      apiCall('/identity/resolve', { method: 'POST', body: { channel: 'cpf', identifier: cpf } }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    if (err.errorCode === 'cpf_not_found') {
      await botSay('Não encontrei esse CPF no nosso cadastro. Parece que você é novo por aqui — qual é o seu nome completo?');
      session.state = BOT_STATES.AWAITING_NAME;
      return;
    }
    if (err.errorCode === 'invalid_cpf') {
      await botSay('Esse CPF não é válido — confira os números e tente novamente.');
      return;
    }
    return handleDomainError(err);
  }

  await onIdentityResolved(identity);
}

async function handleAwaitingName(text) {
  const name = text.trim();
  if (name.length < 3) {
    await botSay('Pode me passar seu nome completo, por favor?');
    return;
  }

  let identity;
  try {
    identity = await withProcessing('verificando cadastro...', () =>
      apiCall('/identity/resolve', {
        method: 'POST',
        body: { channel: 'cpf', identifier: session.cpf, full_name_hint: name },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  await onIdentityResolved(identity);
}

async function onIdentityResolved(identity) {
  session.customerId = identity.unified_customer_id;
  session.customerName = identity.customer.full_name;

  addCfeBadge('Identidade resolvida — cliente vinculado');

  let journey;
  try {
    journey = await withProcessing('abrindo sua solicitação...', () =>
      apiCall('/context/open', {
        method: 'POST',
        body: {
          customer_id: session.customerId,
          origin_channel: 'whatsapp',
          intent: 'change_plan',
          initial_step: 'identity_resolved',
          payload: {},
        },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  session.journeyId = journey.id;
  addCfeBadge(`Jornada aberta — status: ${journey.status}`);

  const firstName = session.customerName.split(' ')[0];
  await botSay(`Prazer, ${firstName}! Identidade confirmada. ✅`);

  await presentPlans();
}

async function presentPlans() {
  let plansResponse;
  try {
    plansResponse = await withProcessing('carregando opções...', () => apiCall('/plans'));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  session.plans = plansResponse.plans;

  const list = session.plans
    .map(p => `• ${p.name} — ${formatCents(p.monthly_price_cents)}/mês`)
    .join('\n');

  await botSay(`Estes são os planos disponíveis:\n${list}\n\nQual você gostaria? Pode digitar o nome (ex: "60GB").`);
  session.state = BOT_STATES.AWAITING_PLAN_CHOICE;
}

async function handleAwaitingPlanChoice(text) {
  const plan = matchPlan(text, session.plans);
  if (!plan) {
    await botSay('Não reconheci esse plano. Pode escolher um da lista acima, digitando por exemplo "30GB" ou "100GB"?');
    return;
  }

  try {
    await withProcessing('atualizando seus dados...', () =>
      apiCall(`/context/${session.journeyId}`, {
        method: 'PATCH',
        body: { current_step: 'plan_selected', payload_merge: { selected_plan_code: plan.code } },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`Contexto atualizado — plano selecionado: ${plan.name}`);

  let handoff;
  try {
    handoff = await withProcessing('preparando continuação...', () =>
      apiCall('/handoff/generate', {
        method: 'POST',
        body: { journey_context_id: session.journeyId, target_channel: 'app' },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`Deep link gerado — válido até ${formatMessageTime(handoff.expires_at)}`);

  await botSay(`Prontinho! Já deixei tudo preparado para você confirmar a troca para o plano ${plan.name}. 🎉`);
  addLinkCard(handoff.deep_link_url, handoff.expires_at);

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

function addMessage(sender, text, extra) {
  const msg = { sender, text, ts: Date.now(), ...extra };
  const previous = session.messages[session.messages.length - 1];
  session.messages.push(msg);
  renderMessage(msg, previous);
  scrollToBottom();
}

function addBotMessage(text)     { addMessage('bot', text); }
function addSystemMessage(text)  { addMessage('system', text); }
function addCfeBadge(text)       { addMessage('badge', text); }
function addLinkCard(url, expiresAt) { addMessage('link-card', url, { expiresAt }); }

function renderMessage(msg, previous) {
  const container = document.getElementById('messages');
  const el = document.createElement('div');
  const timeText = formatMessageTime(msg.ts);

  if (msg.sender === 'badge') {
    el.className = 'cfe-badge';
    el.innerHTML = `${SVG_CFE_ICON}<span>${escapeHtml(msg.text)}</span>`;
  } else if (msg.sender === 'system') {
    el.className = 'system-message';
    el.textContent = msg.text;
  } else if (msg.sender === 'link-card') {
    el.className = 'link-card';
    const expiresText = msg.expiresAt
      ? `válido até ${formatMessageTime(msg.expiresAt)}`
      : 'válido por tempo limitado';
    el.innerHTML = `
      <div class="link-card-header">
        <div class="link-card-icon" aria-hidden="true">M</div>
        <div class="link-card-info">
          <div class="link-card-title">Continuar troca de plano</div>
          <div class="link-card-subtitle">Seus dados já estão preenchidos no Meu Claro</div>
        </div>
      </div>
      <a class="link-card-button" href="${escapeAttr(msg.text)}" target="_blank" rel="noopener">
        Continuar no App
      </a>
      <div class="link-card-time">${SVG_CLOCK}<span>${expiresText}</span></div>
    `;
  } else {
    const isUser = msg.sender === 'user';
    const isGrouped = previous
      && previous.sender === msg.sender
      && (msg.sender === 'user' || msg.sender === 'bot');
    el.className = `bubble ${isUser ? 'bubble-user' : 'bubble-bot'}${isGrouped ? ' grouped' : ''}`;
    el.innerHTML = `
      <div class="bubble-text">${escapeHtml(msg.text)}</div>
      <div class="bubble-meta">
        <span class="bubble-time">${timeText}</span>
        ${isUser ? SVG_CHECKS_READ : ''}
      </div>
    `;
  }

  container.appendChild(el);
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function escapeAttr(text) {
  return String(text).replace(/"/g, '&quot;').replace(/</g, '&lt;');
}

function scrollToBottom() {
  const container = document.getElementById('messages');
  container.scrollTop = container.scrollHeight;
}

function showTyping(visible) {
  document.getElementById('typing-indicator').classList.toggle('hidden', !visible);
  if (visible) scrollToBottom();
}

function showProcessing(textOrFalse) {
  const el = document.getElementById('processing-indicator');
  const visible = !!textOrFalse;
  if (visible) document.getElementById('processing-text').textContent = textOrFalse;
  el.classList.toggle('hidden', !visible);
  if (visible) scrollToBottom();
}

function showDegradedBanner(visible) {
  document.getElementById('degraded-banner').classList.toggle('hidden', !visible);
}

function setInputEnabled(enabled) {
  document.getElementById('message-input').disabled = !enabled;
  const btn = document.getElementById('send-button');
  btn.disabled = !enabled;
}

// ---------- Menu dropdown (3 pontos) ----------

function setupMenu() {
  const toggle = document.getElementById('menu-toggle');
  const dropdown = document.getElementById('menu-dropdown');

  toggle.addEventListener('click', event => {
    event.stopPropagation();
    const isOpen = !dropdown.classList.contains('hidden');
    dropdown.classList.toggle('hidden');
    toggle.setAttribute('aria-expanded', String(!isOpen));
  });

  document.addEventListener('click', event => {
    if (!dropdown.contains(event.target) && !toggle.contains(event.target)) {
      dropdown.classList.add('hidden');
      toggle.setAttribute('aria-expanded', 'false');
    }
  });
}

// ---------- Bootstrap ----------

document.addEventListener('DOMContentLoaded', () => {
  loadOrInitSession();
  setupMenu();

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
    document.getElementById('menu-dropdown').classList.add('hidden');
    if (confirm('Reiniciar a conversa? O histórico atual será perdido.')) {
      resetSession();
    }
  });
});
