// Configuração do canal App Minha Claro simulado.
// CHANNEL_TOKEN é mockado (ver spec-tecnica §7.2 e §11.1) — não é segurança real, só identifica o canal para o CFE.
const CFE_CONFIG = {
  apiBaseUrl: 'http://localhost:5104',
  channelToken: 'fake-app-token',
  whatsappSimUrl: 'http://localhost:5171',
};
