import { criarFila } from './fila.js';
import { criarServidorHttp } from './http.js';
import { criarLog } from './log.js';
import { criarConexaoSimulada } from './simulado.js';
import { criarConexaoWhatsapp } from './whatsapp.js';

// Servidor local do WhatsApp, iniciado e supervisionado pelo FestAgenda.exe. Configuração por variáveis de ambiente:
//   WHATSAPP_TOKEN     token Bearer exigido em todas as rotas (obrigatório; o app gera um a cada início)
//   WHATSAPP_PORTA     porta em 127.0.0.1 (0 ou vazio = escolhe uma livre)
//   WHATSAPP_SESSAO    pasta da sessão (Whatsapp\sessao)
//   WHATSAPP_ARQUIVOS  pasta Arquivos\ do sistema (anexos só de dentro dela)
//   WHATSAPP_LOG       arquivo de log (Log\whatsapp.log)
//   WHATSAPP_LIMITE    máximo de envios por dia (RN29)
//   WHATSAPP_MOCK=1    modo simulado, sem conectar no WhatsApp
// Quando está ouvindo, escreve "PORTA=<n>" na saída padrão: é assim que o app descobre a porta.

const ambiente = process.env;
const token = ambiente.WHATSAPP_TOKEN;
if (!token || token.length < 16) {
  console.error('WHATSAPP_TOKEN ausente ou curto demais.');
  process.exit(2);
}

const log = criarLog(ambiente.WHATSAPP_LOG || null);
const simulado = ambiente.WHATSAPP_MOCK === '1';
const conexao = simulado
  ? criarConexaoSimulada({ log })
  : criarConexaoWhatsapp({ pastaSessao: ambiente.WHATSAPP_SESSAO || 'sessao', log });
const fila = criarFila({
  limiteDiario: Math.max(1, Number(ambiente.WHATSAPP_LIMITE) || 10),
  ...(simulado ? { intervaloMinimo: 0, intervaloMaximo: 0 } : {}),
});

const servidor = criarServidorHttp({ token, conexao, fila, pastaArquivos: ambiente.WHATSAPP_ARQUIVOS || 'Arquivos', log });
servidor.listen(Number(ambiente.WHATSAPP_PORTA) || 0, '127.0.0.1', () => {
  const { port } = servidor.address();
  log.info(`Servidor iniciado na porta ${port}${simulado ? ' (simulado)' : ''}.`);
  console.log(`PORTA=${port}`);

  // Sessão salva: reconecta sozinho, sem novo QR
  if (conexao.temSessao()) {
    conexao.conectar().catch((erro) => log.erro(`Falha ao reconectar: ${erro.message}`));
  }
});

function encerrar() {
  conexao.encerrar();
  servidor.close();
  process.exit(0);
}

process.on('SIGTERM', encerrar);
process.on('SIGINT', encerrar);
// O app mantém a entrada padrão aberta: se ele fechar (ou travar e for morto), a entrada fecha e o servidor sai junto
process.stdin.on('end', encerrar);
process.stdin.resume();
process.on('uncaughtException', (erro) => log.erro(`Erro não tratado: ${erro.message}`));
process.on('unhandledRejection', (erro) => log.erro(`Promessa rejeitada: ${erro?.message ?? erro}`));
