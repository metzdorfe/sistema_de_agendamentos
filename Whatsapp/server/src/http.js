import crypto from 'node:crypto';
import http from 'node:http';
import { documentoValido } from './caminho.js';
import { normalizarTelefone } from './telefone.js';

// Contrato HTTP com o Fest Agenda (só 127.0.0.1, token Bearer em todas as rotas).
// Resposta padrão: { ok, id, erro, codigo }. Códigos: NAO_CONECTADO, NUMERO_SEM_WHATSAPP, ARQUIVO_INVALIDO,
// LIMITE, FALHA, mais TELEFONE_INVALIDO e PEDIDO_INVALIDO.

const LIMITE_CORPO = 64 * 1024;
const LIMITE_TEXTO = 4000;

function tokenConfere(cabecalho, token) {
  const esperado = Buffer.from(`Bearer ${token}`);
  const recebido = Buffer.from(String(cabecalho ?? ''));
  return recebido.length === esperado.length && crypto.timingSafeEqual(recebido, esperado);
}

function lerCorpo(req) {
  return new Promise((resolve, reject) => {
    let tamanho = 0;
    const partes = [];
    // Passou do limite: descarta o resto (sem guardar) e responde 413 no fim
    req.on('data', (parte) => {
      tamanho += parte.length;
      if (tamanho <= LIMITE_CORPO) {
        partes.push(parte);
      }
    });
    req.on('end', () => {
      if (tamanho > LIMITE_CORPO) {
        reject(Object.assign(new Error('Pedido grande demais.'), { status: 413 }));
        return;
      }

      if (partes.length === 0) {
        resolve({});
        return;
      }

      try {
        resolve(JSON.parse(Buffer.concat(partes).toString('utf8')));
      } catch {
        reject(Object.assign(new Error('JSON inválido.'), { status: 400 }));
      }
    });
    req.on('error', reject);
  });
}

/**
 * @param {object} opcoes
 * @param {string} opcoes.token
 * @param {ReturnType<import('./whatsapp.js').criarConexaoWhatsapp>} opcoes.conexao
 * @param {ReturnType<import('./fila.js').criarFila>} opcoes.fila
 * @param {string} opcoes.pastaArquivos pasta Arquivos\ do sistema: anexos só podem vir de dentro dela
 * @param {ReturnType<import('./log.js').criarLog>} opcoes.log
 */
export function criarServidorHttp({ token, conexao, fila, pastaArquivos, log }) {
  const erro = (codigo, mensagem) => ({ ok: false, id: null, codigo, erro: mensagem });

  async function tratar(req) {
    const rota = `${req.method} ${req.url}`;
    if (rota === 'GET /health') {
      return { ok: true };
    }

    if (rota === 'GET /status') {
      return { ok: true, ...conexao.status() };
    }

    if (rota === 'POST /conectar') {
      await conexao.conectar();
      return { ok: true, ...conexao.status() };
    }

    if (rota === 'POST /desconectar') {
      await conexao.desconectar();
      return { ok: true };
    }

    if (rota !== 'POST /enviar/texto' && rota !== 'POST /enviar/documento') {
      return Object.assign(erro('PEDIDO_INVALIDO', 'Rota não encontrada.'), { status: 404 });
    }

    const corpo = await lerCorpo(req);
    const telefone = normalizarTelefone(corpo.telefone);
    if (!telefone.ok) {
      return erro('TELEFONE_INVALIDO', telefone.erro);
    }

    if (rota === 'POST /enviar/texto') {
      const texto = String(corpo.texto ?? '').trim();
      if (texto === '' || texto.length > LIMITE_TEXTO) {
        return erro('PEDIDO_INVALIDO', 'Informe o texto (até 4000 caracteres).');
      }

      return registrar('texto', telefone.numero, await fila.adicionar(() => conexao.enviarTexto(telefone.numero, texto)));
    }

    const arquivo = documentoValido(pastaArquivos, corpo.caminhoArquivo);
    if (!arquivo) {
      return erro('ARQUIVO_INVALIDO', 'O arquivo precisa ser um PDF dentro da pasta Arquivos do sistema.');
    }

    const nome = String(corpo.nomeArquivo ?? 'documento.pdf').slice(0, 120);
    const legenda = String(corpo.legenda ?? '').slice(0, LIMITE_TEXTO);
    return registrar('documento', telefone.numero,
      await fila.adicionar(() => conexao.enviarDocumento(telefone.numero, arquivo, nome, legenda)));
  }

  function registrar(tipo, numero, resultado) {
    if (resultado.ok) {
      log.info(`Enviado ${tipo} para ${numero}.`);
    } else {
      log.aviso(`Falha ao enviar ${tipo} para ${numero}: ${resultado.codigo}.`);
    }

    return { id: null, erro: null, codigo: null, ...resultado };
  }

  return http.createServer(async (req, res) => {
    let status = 200;
    let resposta;
    try {
      if (!tokenConfere(req.headers.authorization, token)) {
        status = 401;
        resposta = erro('PEDIDO_INVALIDO', 'Token inválido.');
      } else {
        resposta = await tratar(req);
        if (resposta.status) {
          status = resposta.status;
          delete resposta.status;
        }
      }
    } catch (falha) {
      status = falha.status ?? 500;
      resposta = erro(status === 500 ? 'FALHA' : 'PEDIDO_INVALIDO', falha.message);
      if (status === 500) {
        log.erro(`Erro em ${req.method} ${req.url}: ${falha.message}`);
      }
    }

    res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' });
    res.end(JSON.stringify(resposta));
  });
}
