// Modo simulado (WHATSAPP_MOCK=1): responde como o WhatsApp sem conectar em nada.
// Para testar o sistema e os testes automáticos. Número terminado em 0000 "não tem WhatsApp".

const ATRASO_QR_MS = 3000;

export function criarConexaoSimulada({ log }) {
  let estado = 'desconectado';
  let sessao = false;
  let enviados = 0;
  let temporizador = null;

  function conectarAgora() {
    estado = 'conectado';
    sessao = true;
  }

  async function enviar(numero) {
    if (estado !== 'conectado') {
      return { ok: false, codigo: 'NAO_CONECTADO', erro: 'O WhatsApp não está conectado.' };
    }

    if (numero.endsWith('0000')) {
      return { ok: false, codigo: 'NUMERO_SEM_WHATSAPP', erro: 'Este número não tem WhatsApp.' };
    }

    enviados++;
    log.info(`Simulado: mensagem ${enviados} enviada.`);
    return { ok: true, id: `SIMULADO-${enviados}` };
  }

  return {
    temSessao: () => sessao,

    async conectar() {
      if (sessao) {
        conectarAgora();
        return;
      }

      // Mostra um QR por alguns segundos e "lê" sozinho
      estado = 'aguardando_qr';
      clearTimeout(temporizador);
      temporizador = setTimeout(conectarAgora, ATRASO_QR_MS);
    },

    status() {
      return {
        estado,
        qr: estado === 'aguardando_qr' ? 'FEST-AGENDA-QR-SIMULADO' : null,
        numero: estado === 'conectado' ? '5547999990000' : null,
        nome: estado === 'conectado' ? 'WhatsApp simulado' : null,
        erro: null,
      };
    },

    enviarTexto: (numero) => enviar(numero),
    enviarDocumento: (numero) => enviar(numero),

    async desconectar() {
      clearTimeout(temporizador);
      sessao = false;
      estado = 'desconectado';
    },

    encerrar() {
      clearTimeout(temporizador);
    },
  };
}
