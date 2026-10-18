import fs from 'node:fs';
import path from 'node:path';

// Log em arquivo (Log\whatsapp.log), com rotação a cada 1 MB, guardando os 3 últimos.
// Nunca grava texto de mensagem; números com 8 dígitos ou mais saem mascarados (só os 4 últimos).

const TAMANHO_MAXIMO = 1024 * 1024;
const GUARDADOS = 3;

export function mascararTelefone(telefone) {
  const digitos = String(telefone ?? '').replace(/\D/g, '');
  return digitos.length <= 4 ? '****' : '*'.repeat(digitos.length - 4) + digitos.slice(-4);
}

export function limparTexto(texto) {
  return String(texto).replace(/\d{8,}/g, mascararTelefone).replace(/\s+/g, ' ').trim();
}

/** @param {string | null} arquivo sem arquivo, o log é descartado */
export function criarLog(arquivo) {
  function rotacionar() {
    let tamanho;
    try {
      tamanho = fs.statSync(arquivo).size;
    } catch {
      return;
    }

    if (tamanho < TAMANHO_MAXIMO) {
      return;
    }

    const antigo = (n) => arquivo.replace(/\.log$/i, `.${n}.log`);
    fs.rmSync(antigo(GUARDADOS), { force: true });
    for (let n = GUARDADOS - 1; n >= 1; n--) {
      if (fs.existsSync(antigo(n))) {
        fs.renameSync(antigo(n), antigo(n + 1));
      }
    }

    fs.renameSync(arquivo, antigo(1));
  }

  function escrever(nivel, texto) {
    if (!arquivo) {
      return;
    }

    try {
      fs.mkdirSync(path.dirname(arquivo), { recursive: true });
      rotacionar();
      const agora = new Date().toLocaleString('sv-SE');
      fs.appendFileSync(arquivo, `${agora} | ${nivel.padEnd(5)} | ${limparTexto(texto)}\n`, 'utf8');
    } catch {
      // Sem permissão, disco cheio etc.: segue sem log
    }
  }

  const log = {
    info: (texto) => escrever('INFO', texto),
    aviso: (texto) => escrever('AVISO', texto),
    erro: (texto) => escrever('ERRO', texto),

    /** Logger no formato que o Baileys espera: só avisos e erros, sem os objetos (podem ter números e conteúdo). */
    baileys() {
      const mensagem = (obj, msg) => msg ?? (obj instanceof Error ? obj.message : typeof obj === 'string' ? obj : '');
      const logger = {
        level: 'warn',
        child: () => logger,
        trace: () => {},
        debug: () => {},
        info: () => {},
        warn: (obj, msg) => escrever('AVISO', `baileys: ${mensagem(obj, msg)}`),
        error: (obj, msg) => escrever('ERRO', `baileys: ${mensagem(obj, msg)}`),
      };
      return logger;
    },
  };

  return log;
}
