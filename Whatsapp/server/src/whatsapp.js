import fs from 'node:fs';
import path from 'node:path';
import makeWASocket, { Browsers, DisconnectReason, useMultiFileAuthState } from '@whiskeysockets/baileys';
import { alternativasNonoDigito } from './telefone.js';

// Conexão com o WhatsApp pelo Baileys (não oficial): sessão por QR code, salva em Whatsapp\sessao\.
// Mesma interface do simulado.js.

const MAXIMO_RECONEXOES = 5;
const esperar = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * @param {object} opcoes
 * @param {string} opcoes.pastaSessao
 * @param {ReturnType<import('./log.js').criarLog>} opcoes.log
 */
export function criarConexaoWhatsapp({ pastaSessao, log }) {
  let sock = null;
  let estado = 'desconectado';
  let qr = null;
  let conta = null;
  let ultimoErro = null;
  let reconexoes = 0;
  let encerrando = false;

  const temSessao = () => fs.existsSync(path.join(pastaSessao, 'creds.json'));

  function apagarSessao() {
    fs.rmSync(pastaSessao, { recursive: true, force: true });
  }

  async function conectar() {
    if (sock) {
      return;
    }

    encerrando = false;
    estado = 'conectando';
    ultimoErro = null;
    fs.mkdirSync(pastaSessao, { recursive: true });
    const { state, saveCreds } = await useMultiFileAuthState(pastaSessao);
    const atual = makeWASocket({
      auth: state,
      logger: log.baileys(),
      browser: Browsers.windows('Fest Agenda'),
      markOnlineOnConnect: false,
      syncFullHistory: false,
    });
    sock = atual;

    atual.ev.on('creds.update', saveCreds);
    atual.ev.on('connection.update', (atualizacao) => {
      if (sock !== atual) {
        return;
      }

      if (atualizacao.qr) {
        qr = atualizacao.qr;
        estado = 'aguardando_qr';
      }

      if (atualizacao.connection === 'open') {
        estado = 'conectado';
        qr = null;
        reconexoes = 0;
        const id = atual.user?.id ?? '';
        conta = { numero: id.split(':')[0].split('@')[0], nome: atual.user?.name ?? null };
        log.info('Conectado ao WhatsApp.');
      }

      if (atualizacao.connection === 'close') {
        const codigo = atualizacao.lastDisconnect?.error?.output?.statusCode;
        sock = null;
        conta = null;
        qr = null;
        if (encerrando) {
          estado = 'desconectado';
          return;
        }

        if (codigo === DisconnectReason.loggedOut) {
          // Desconectado pelo celular: a sessão não vale mais, precisa de novo QR
          apagarSessao();
          estado = 'desconectado';
          log.aviso('Sessão encerrada pelo celular.');
          return;
        }

        if (reconexoes >= MAXIMO_RECONEXOES) {
          estado = 'erro';
          ultimoErro = 'Não foi possível reconectar ao WhatsApp. Confira a internet e tente conectar de novo.';
          log.erro(`Desistiu de reconectar (código ${codigo}).`);
          return;
        }

        reconexoes++;
        estado = 'conectando';
        log.aviso(`Conexão caiu (código ${codigo}); reconectando (${reconexoes}/${MAXIMO_RECONEXOES}).`);
        setTimeout(() => conectar().catch((erro) => {
          estado = 'erro';
          ultimoErro = erro.message;
        }), codigo === DisconnectReason.restartRequired ? 0 : Math.min(30000, 2000 * reconexoes));
      }
    });
  }

  /** JID real do número (com ou sem o nono dígito), ou null se não tem WhatsApp. */
  async function resolverJid(numero) {
    for (const alternativa of alternativasNonoDigito(numero)) {
      const [resultado] = (await sock.onWhatsApp(alternativa)) ?? [];
      if (resultado?.exists) {
        return resultado.jid;
      }
    }

    return null;
  }

  async function enviar(numero, conteudo) {
    if (estado !== 'conectado' || !sock) {
      return { ok: false, codigo: 'NAO_CONECTADO', erro: 'O WhatsApp não está conectado.' };
    }

    const jid = await resolverJid(numero);
    if (!jid) {
      return { ok: false, codigo: 'NUMERO_SEM_WHATSAPP', erro: 'Este número não tem WhatsApp.' };
    }

    // "Digitando..." antes de enviar, como uma pessoa
    await sock.sendPresenceUpdate('composing', jid);
    await esperar(1500 + Math.random() * 1500);
    await sock.sendPresenceUpdate('paused', jid);
    const mensagem = await sock.sendMessage(jid, conteudo);
    return { ok: true, id: mensagem?.key?.id ?? null };
  }

  return {
    temSessao,
    conectar,

    status() {
      return { estado, qr, numero: conta?.numero ?? null, nome: conta?.nome ?? null, erro: ultimoErro };
    },

    enviarTexto: (numero, texto) => enviar(numero, { text: texto }),

    enviarDocumento: (numero, caminhoCompleto, nomeArquivo, legenda) =>
      enviar(numero, {
        document: { url: caminhoCompleto },
        mimetype: 'application/pdf',
        fileName: nomeArquivo,
        caption: legenda || undefined,
      }),

    /** Logout no WhatsApp e apaga a sessão: a próxima conexão pede QR de novo. */
    async desconectar() {
      encerrando = true;
      try {
        await sock?.logout();
      } catch {
        // Já estava fora: segue apagando a sessão
      }

      sock = null;
      conta = null;
      qr = null;
      apagarSessao();
      estado = 'desconectado';
      log.info('Desconectado pelo sistema.');
    },

    /** Fecha a conexão sem apagar a sessão (o app está fechando). */
    encerrar() {
      encerrando = true;
      sock?.end(undefined);
      sock = null;
    },
  };
}
