// Chat simulado (canal WhatsApp) — máquina de estados do bot conduzindo a intenção "troca de plano".
// Estado do bot fica em localStorage (canal simulado, aceitável para o MVP — ver spec-tecnica §11.4).
// O CFE em si NUNCA guarda estado da jornada em memória: o journey_id é a única fonte de verdade
// persistida no servidor; o que fica aqui é só a "UI state machine" do bot conversacional.

const SESSION_KEY = 'cfe_whatsapp_session';

const BOT_STATES = {
  NOT_STARTED: 'not_started',
  AWAITING_INTENT: 'awaiting_intent',
  AWAITING_CPF: 'awaiting_cpf',
  AWAITING_NAME: 'awaiting_name',
  AWAITING_PLAN_CHOICE: 'awaiting_plan_choice',
  AWAITING_INVOICE_CHOICE: 'awaiting_invoice_choice',
  AWAITING_DISPUTE_REASON: 'awaiting_dispute_reason',
  AWAITING_PROBLEM_DESCRIPTION: 'awaiting_problem_description',
  COMPLETED: 'completed',
};

const INTENTS = {
  CHANGE_PLAN: 'change_plan',
  DISPUTE_CHARGE: 'dispute_charge',
};

// Motivos pré-definidos de contestação (FASE 3, Bloco A) — ids espelham Common/Contracts/DisputeReason.cs.
const DISPUTE_REASONS = [
  { id: 'service_not_contracted', label: 'Cobrança de serviço que não contratei' },
  { id: 'higher_than_expected', label: 'Valor cobrado maior que o esperado' },
  { id: 'duplicate_charge', label: 'Cobrança em duplicidade' },
  { id: 'cancelled_service_still_charged', label: 'Serviço cancelado ainda sendo cobrado' },
  { id: 'after_portability', label: 'Cobrança após portabilidade' },
  { id: 'other', label: 'Outro motivo' },
];

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
    state: BOT_STATES.NOT_STARTED,
    messages: [],
    cpf: null,
    customerId: null,
    customerName: null,
    journeyId: null,
    intent: null,
    currentPlan: null,
    plans: null,
    invoices: null,
    disputeReason: null,
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
    // Igual numa conversa real: o chat abre vazio, sem mensagem automática do bot.
    // A saudação com os botões só aparece como resposta à primeira mensagem do usuário
    // (ver handleFirstMessage) — não é o assistente que puxa assunto sozinho.
    session = createFreshSession();
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

function detectsDisputeChargeIntent(text) {
  const t = text.toLowerCase();
  const mentionsBilling = ['cobrança', 'cobranca', 'fatura', 'conta'].some(k => t.includes(k));
  const mentionsProblem = ['indevid', 'errad', 'contest', 'duvid', 'estranh'].some(k => t.includes(k));
  return mentionsBilling && mentionsProblem;
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

function matchInvoice(text, invoices) {
  const t = text.toLowerCase();
  return (invoices || []).find(inv => t.includes(inv.reference_label.split('/')[0].toLowerCase()));
}

function matchDisputeReason(text) {
  const t = text.toLowerCase();
  const byId = id => DISPUTE_REASONS.find(r => r.id === id);
  if (t.includes('nao contratei') || t.includes('não contratei') || t.includes('nunca contratei')) return byId('service_not_contracted');
  if (t.includes('maior') || t.includes('valor cobrado') || t.includes('cobraram mais')) return byId('higher_than_expected');
  if (t.includes('duplicid') || t.includes('duplicad') || t.includes('duas vezes')) return byId('duplicate_charge');
  if (t.includes('cancel')) return byId('cancelled_service_still_charged');
  if (t.includes('portabilidade')) return byId('after_portability');
  if (t.includes('outro')) return byId('other');
  return null;
}

function formatCents(cents) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function formatMessageTime(ts) {
  return new Date(ts).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function formatDateShort(isoDate) {
  const [y, m, d] = isoDate.split('-');
  return `${d}/${m}/${y}`;
}

// ---------- Máquina de estados ----------

async function handleUserMessage(text) {
  addMessage('user', text);
  persistSession();

  setInputEnabled(false);
  try {
    switch (session.state) {
      case BOT_STATES.NOT_STARTED:
        await handleFirstMessage();
        break;
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
      case BOT_STATES.AWAITING_INVOICE_CHOICE:
        await handleAwaitingInvoiceChoice(text);
        break;
      case BOT_STATES.AWAITING_DISPUTE_REASON:
        await handleAwaitingDisputeReason(text);
        break;
      case BOT_STATES.AWAITING_PROBLEM_DESCRIPTION:
        await handleAwaitingProblemDescription(text);
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
async function botSay(text, interactive) {
  showTyping(true);
  await sleep(randomBetween(800, 1500));
  showTyping(false);
  addBotMessage(text, interactive);
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

/**
 * Primeira mensagem do usuário na conversa (estado NOT_STARTED). Igual a um atendimento real:
 * o conteúdo em si não é interpretado aqui (isso já acontece em handleAwaitingIntent a partir da
 * próxima mensagem) — a primeira mensagem só "abre" a conversa e recebe a saudação padrão com os
 * botões de intenção, que antes era enviada sozinha assim que a página carregava.
 */
async function handleFirstMessage() {
  await botSay('Olá! 👋 Sou o assistente virtual da Claro. Como posso te ajudar hoje?', {
    type: 'buttons',
    options: [
      { id: INTENTS.CHANGE_PLAN, label: 'Trocar de plano' },
      { id: INTENTS.DISPUTE_CHARGE, label: 'Contestar cobrança' },
    ],
  });
  session.state = BOT_STATES.AWAITING_INTENT;
}

async function handleAwaitingIntent(text) {
  // Camada oculta de heurística por texto livre — os botões são o caminho primário (Passo A da ETAPA 2),
  // mas texto residual como "quero trocar de plano" ainda funciona.
  if (detectsChangePlanIntent(text)) {
    session.intent = INTENTS.CHANGE_PLAN;
    await proceedToCpfCollection();
    return;
  }
  if (detectsDisputeChargeIntent(text)) {
    session.intent = INTENTS.DISPUTE_CHARGE;
    await proceedToCpfCollection();
    return;
  }
  await botSay('Por favor, use uma das opções acima. 🙂');
}

async function proceedToCpfCollection() {
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
  session.currentPlan = identity.customer.current_plan || null;

  addCfeBadge('Identidade resolvida — cliente vinculado');

  let journey;
  try {
    journey = await withProcessing('abrindo sua solicitação...', () =>
      apiCall('/context/open', {
        method: 'POST',
        body: {
          customer_id: session.customerId,
          origin_channel: 'whatsapp',
          intent: session.intent,
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

  if (session.intent === INTENTS.DISPUTE_CHARGE) {
    await presentInvoices();
  } else {
    await presentPlans();
  }
}

async function presentPlans() {
  let plansResponse;
  try {
    plansResponse = await withProcessing('carregando opções...', () => apiCall('/plans'));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  // Plano atual não entra na lista de opções — evita o cliente "trocar" para o mesmo plano (FASE 3, item B.1).
  session.plans = session.currentPlan
    ? plansResponse.plans.filter(p => p.code !== session.currentPlan.code)
    : plansResponse.plans;

  if (session.currentPlan) {
    await botSay(`Seu plano atual é o ${session.currentPlan.name} — ${formatCents(session.currentPlan.monthly_price_cents)}/mês.`);
  }

  await botSay('Estes são os planos que você pode escolher:', {
    type: 'list',
    options: session.plans.map(p => ({
      id: p.code,
      label: p.name,
      description: `${p.data_gb}GB — ${formatCents(p.monthly_price_cents)}/mês`,
    })),
  });
  session.state = BOT_STATES.AWAITING_PLAN_CHOICE;
}

async function handleAwaitingPlanChoice(text) {
  // Camada oculta de heurística por texto livre — a lista é o caminho primário.
  const plan = matchPlan(text, session.plans);
  if (!plan) {
    await botSay('Não reconheci esse plano. Use uma das opções da lista acima.');
    return;
  }
  await proceedWithPlanSelection(plan);
}

async function proceedWithPlanSelection(plan) {
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
  addLinkCard(handoff.deep_link_url, handoff.expires_at, 'Continuar troca de plano', 'Seus dados já estão preenchidos no Meu Claro');

  session.state = BOT_STATES.COMPLETED;
}

// ---------- Fluxo de contestação de cobrança (ETAPA 2, Passo C) ----------

async function presentInvoices() {
  let invoicesResponse;
  try {
    invoicesResponse = await withProcessing('consultando suas faturas...', () =>
      apiCall(`/invoices/customer/${session.customerId}?limit=3`));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  session.invoices = invoicesResponse.invoices;

  if (session.invoices.length === 0) {
    await botSay('Não encontrei nenhuma fatura recente no seu cadastro. Vou encerrar por aqui — tente novamente mais tarde ou fale com um atendente. 🙏');
    session.state = BOT_STATES.COMPLETED;
    return;
  }

  await botSay('Estas são suas últimas faturas. Qual delas você quer contestar?', {
    type: 'list',
    options: session.invoices.map(inv => ({
      id: inv.id,
      label: inv.reference_label,
      description: `${formatCents(inv.total_cents)} — vencimento ${formatDateShort(inv.due_date)}`,
    })),
  });
  session.state = BOT_STATES.AWAITING_INVOICE_CHOICE;
}

async function handleAwaitingInvoiceChoice(text) {
  // Camada oculta de heurística por texto livre — a lista é o caminho primário.
  const invoice = matchInvoice(text, session.invoices);
  if (!invoice) {
    await botSay('Não reconheci essa fatura. Use uma das opções da lista acima.');
    return;
  }
  await proceedWithInvoiceSelection(invoice);
}

async function proceedWithInvoiceSelection(invoice) {
  try {
    await withProcessing('atualizando seus dados...', () =>
      apiCall(`/context/${session.journeyId}`, {
        method: 'PATCH',
        body: { current_step: 'invoice_selected', payload_merge: { invoice_id: invoice.id } },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`Contexto atualizado — fatura selecionada: ${invoice.reference_label}`);

  await presentDisputeReasons();
}

async function presentDisputeReasons() {
  await botSay('Qual desses motivos melhor descreve o problema?', {
    type: 'list',
    options: DISPUTE_REASONS.map(r => ({ id: r.id, label: r.label })),
  });
  session.state = BOT_STATES.AWAITING_DISPUTE_REASON;
}

async function handleAwaitingDisputeReason(text) {
  // Camada oculta de heurística por texto livre — a lista é o caminho primário.
  const reason = matchDisputeReason(text);
  if (!reason) {
    await botSay('Não reconheci esse motivo. Use uma das opções da lista acima.');
    return;
  }
  await proceedWithDisputeReason(reason);
}

async function proceedWithDisputeReason(reason) {
  try {
    await withProcessing('atualizando seus dados...', () =>
      apiCall(`/context/${session.journeyId}`, {
        method: 'PATCH',
        body: { current_step: 'dispute_reason_selected', payload_merge: { dispute_reason: reason.id } },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(`Contexto atualizado — motivo selecionado: ${reason.label}`);

  session.disputeReason = reason.id;

  if (reason.id === 'other') {
    await botSay('Por favor, descreva o que aconteceu — nesse caso a descrição é obrigatória.');
  } else {
    await botSay('Se quiser, descreva mais detalhes sobre o problema. Ou digite "pular" para continuar sem descrição.');
  }
  session.state = BOT_STATES.AWAITING_PROBLEM_DESCRIPTION;
}

async function handleAwaitingProblemDescription(text) {
  const raw = text.trim();
  const isOtherReason = session.disputeReason === 'other';
  const saidPular = raw.toLowerCase() === 'pular';

  // FASE 3.1, item A.1: "pular" nunca é uma descrição válida para o motivo "Outro" — mesmo tendo
  // 5+ caracteres, precisa ser rejeitado explicitamente antes da checagem de tamanho mínimo.
  if (isOtherReason && saidPular) {
    await botSay('Para o motivo "Outro motivo", preciso de uma descrição real do problema (mínimo 5 caracteres).');
    return;
  }

  const wantsToSkip = !isOtherReason && saidPular;

  let description = null;
  if (!wantsToSkip) {
    if (raw.length < 5) {
      await botSay(
        isOtherReason
          ? 'Pode descrever com um pouco mais de detalhe o que aconteceu? Esse campo é obrigatório para "Outro motivo".'
          : 'Pode descrever com um pouco mais de detalhe, ou digitar "pular" para continuar sem descrição?'
      );
      return;
    }
    description = raw;
  }

  try {
    await withProcessing('atualizando seus dados...', () =>
      apiCall(`/context/${session.journeyId}`, {
        method: 'PATCH',
        body: { current_step: 'description_provided', payload_merge: { customer_description: description } },
      }));
  } catch (err) {
    if (err instanceof CfeUnavailableError) return showDegraded(err);
    return handleDomainError(err);
  }

  addCfeBadge(description ? 'Contexto atualizado — descrição do problema registrada' : 'Contexto atualizado — sem descrição adicional');

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

  await botSay('Prontinho! Já deixei tudo preparado para você continuar sua contestação. 🎉');
  addLinkCard(handoff.deep_link_url, handoff.expires_at, 'Continuar contestação', 'Sua fatura e descrição já estão preenchidas no Meu Claro');

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

function addBotMessage(text, interactive) {
  addMessage('bot', text, interactive ? { interactive, answered: null } : undefined);
}
function addSystemMessage(text)  { addMessage('system', text); }
function addCfeBadge(text)       { addMessage('badge', text); }
function addLinkCard(url, expiresAt, title, subtitle) { addMessage('link-card', url, { expiresAt, title, subtitle }); }

/** Chamado ao clicar num botão/item de lista de uma mensagem interativa do bot (Passo A da ETAPA 2). */
async function handleInteractiveClick(msg, option, wrapEl) {
  if (msg.answered) return; // evita duplo clique / reprocessamento de mensagens antigas já respondidas

  msg.answered = option.id;
  wrapEl.querySelectorAll('button').forEach(btn => {
    btn.disabled = true;
    if (btn.dataset.optionId === option.id) btn.classList.add('interactive-selected');
  });

  addMessage('user', option.label);
  persistSession();

  setInputEnabled(false);
  try {
    if (session.state === BOT_STATES.AWAITING_INTENT
      && (option.id === INTENTS.CHANGE_PLAN || option.id === INTENTS.DISPUTE_CHARGE)) {
      session.intent = option.id;
      await proceedToCpfCollection();
    } else if (session.state === BOT_STATES.AWAITING_PLAN_CHOICE) {
      const plan = session.plans.find(p => p.code === option.id);
      if (plan) await proceedWithPlanSelection(plan);
    } else if (session.state === BOT_STATES.AWAITING_INVOICE_CHOICE) {
      const invoice = session.invoices.find(inv => inv.id === option.id);
      if (invoice) await proceedWithInvoiceSelection(invoice);
    } else if (session.state === BOT_STATES.AWAITING_DISPUTE_REASON) {
      const reason = DISPUTE_REASONS.find(r => r.id === option.id);
      if (reason) await proceedWithDisputeReason(reason);
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
          <div class="link-card-title">${escapeHtml(msg.title || 'Continuar no App')}</div>
          <div class="link-card-subtitle">${escapeHtml(msg.subtitle || 'Seus dados já estão preenchidos no Meu Claro')}</div>
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

    if (msg.sender === 'bot' && msg.interactive) {
      el.appendChild(renderInteractive(msg));
    }
  }

  container.appendChild(el);
}

function renderInteractive(msg) {
  const { type, options } = msg.interactive;
  const wrap = document.createElement('div');
  wrap.className = type === 'list' ? 'interactive-list' : 'interactive-buttons';

  options.forEach(opt => {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = type === 'list' ? 'interactive-list-item' : 'interactive-button';
    btn.dataset.optionId = opt.id;
    btn.disabled = !!msg.answered;
    if (msg.answered === opt.id) btn.classList.add('interactive-selected');

    if (type === 'list') {
      btn.innerHTML = `
        <span class="interactive-list-label">${escapeHtml(opt.label)}</span>
        ${opt.description ? `<span class="interactive-list-desc">${escapeHtml(opt.description)}</span>` : ''}
      `;
    } else {
      btn.textContent = opt.label;
    }

    btn.addEventListener('click', () => handleInteractiveClick(msg, opt, wrap));
    wrap.appendChild(btn);
  });

  return wrap;
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
