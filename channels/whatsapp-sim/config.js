// Configuração do canal WhatsApp simulado.
// CHANNEL_TOKEN é mockado (ver spec-tecnica §7.2 e §11.1) — não é segurança real, só identifica o canal para o CFE.
const CFE_CONFIG = {
  apiBaseUrl: 'http://localhost:5104',
  channelToken: 'fake-whatsapp-token',
  appSimUrl: 'http://localhost:5173',
};
