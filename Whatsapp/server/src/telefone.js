// Telefone brasileiro: só dígitos, com 55 e DDD válido.

const DDDS = new Set([
  11, 12, 13, 14, 15, 16, 17, 18, 19,
  21, 22, 24, 27, 28,
  31, 32, 33, 34, 35, 37, 38,
  41, 42, 43, 44, 45, 46, 47, 48, 49,
  51, 53, 54, 55,
  61, 62, 63, 64, 65, 66, 67, 68, 69,
  71, 73, 74, 75, 77, 79,
  81, 82, 83, 84, 85, 86, 87, 88, 89,
  91, 92, 93, 94, 95, 96, 97, 98, 99,
]);

/**
 * Normaliza para 55 + DDD + número (12 ou 13 dígitos).
 * Aceita com ou sem 55, com máscara. Celular (11 dígitos locais) precisa começar com 9.
 * @returns {{ ok: true, numero: string } | { ok: false, erro: string }}
 */
export function normalizarTelefone(entrada) {
  let digitos = String(entrada ?? '').replace(/\D/g, '');
  if ((digitos.length === 12 || digitos.length === 13) && digitos.startsWith('55')) {
    digitos = digitos.slice(2);
  }

  if (digitos.length !== 10 && digitos.length !== 11) {
    return { ok: false, erro: 'Telefone precisa ter DDD e 8 ou 9 dígitos.' };
  }

  const ddd = Number(digitos.slice(0, 2));
  if (!DDDS.has(ddd)) {
    return { ok: false, erro: `DDD ${digitos.slice(0, 2)} não existe.` };
  }

  if (digitos.length === 11 && digitos[2] !== '9') {
    return { ok: false, erro: 'Celular com 9 dígitos precisa começar com 9.' };
  }

  return { ok: true, numero: `55${digitos}` };
}

/**
 * Formas do mesmo número para consultar no WhatsApp, por causa do nono dígito:
 * contas antigas de celular podem estar registradas sem o 9.
 */
export function alternativasNonoDigito(numero) {
  const local = numero.slice(4);
  if (numero.length === 13 && local.startsWith('9')) {
    return [numero, numero.slice(0, 4) + local.slice(1)];
  }

  if (numero.length === 12 && /^[6-9]/.test(local)) {
    return [numero, `${numero.slice(0, 4)}9${local}`];
  }

  return [numero];
}
