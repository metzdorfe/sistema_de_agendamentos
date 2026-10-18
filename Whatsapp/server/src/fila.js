// Fila de envio: uma mensagem por vez, intervalo aleatório entre envios e teto diário (RN29).
// Nunca envio em massa de uma vez: reduz o risco de o número ser bloqueado.

const esperarPadrao = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function chaveDia(data) {
  return `${data.getFullYear()}-${data.getMonth() + 1}-${data.getDate()}`;
}

/**
 * @param {object} opcoes
 * @param {number} opcoes.limiteDiario máximo de envios aceitos por dia
 * @param {number} [opcoes.intervaloMinimo] ms entre o fim de um envio e o início do próximo
 * @param {number} [opcoes.intervaloMaximo]
 * @param {() => Date} [opcoes.agora] relógio (testes)
 * @param {(ms: number) => Promise<void>} [opcoes.esperar] (testes)
 * @param {() => number} [opcoes.aleatorio] 0..1 (testes)
 */
export function criarFila({
  limiteDiario,
  intervaloMinimo = 6000,
  intervaloMaximo = 15000,
  agora = () => new Date(),
  esperar = esperarPadrao,
  aleatorio = Math.random,
}) {
  let cadeia = Promise.resolve();
  let dia = null;
  let enviadosHoje = 0;
  let ultimoEnvio = null;

  /**
   * Põe o envio na fila. A tarefa devolve { ok, ... }; só envio com ok conta para o limite.
   * @param {() => Promise<object>} tarefa
   */
  function adicionar(tarefa) {
    const execucao = cadeia.then(async () => {
      const hoje = chaveDia(agora());
      if (hoje !== dia) {
        dia = hoje;
        enviadosHoje = 0;
      }

      if (enviadosHoje >= limiteDiario) {
        return { ok: false, codigo: 'LIMITE', erro: `Limite de ${limiteDiario} mensagens por dia atingido.` };
      }

      if (ultimoEnvio !== null) {
        const intervalo = intervaloMinimo + aleatorio() * (intervaloMaximo - intervaloMinimo);
        const falta = ultimoEnvio + intervalo - agora().getTime();
        if (falta > 0) {
          await esperar(falta);
        }
      }

      let resultado;
      try {
        resultado = await tarefa();
      } catch (erro) {
        resultado = { ok: false, codigo: 'FALHA', erro: erro?.message ?? String(erro) };
      }

      ultimoEnvio = agora().getTime();
      if (resultado.ok) {
        enviadosHoje++;
      }

      return resultado;
    });

    cadeia = execucao.catch(() => {});
    return execucao;
  }

  return {
    adicionar,
    get enviadosHoje() {
      return enviadosHoje;
    },
  };
}
