import fs from 'node:fs';
import path from 'node:path';

/**
 * Resolve um caminho relativo à pasta base (Arquivos\ do sistema) sem deixar sair dela
 * (nada de "..\..\Windows" nem caminho absoluto). Devolve o caminho completo ou null.
 */
export function resolverDentro(base, relativo) {
  if (typeof relativo !== 'string' || relativo.trim() === '' || path.isAbsolute(relativo)) {
    return null;
  }

  const raiz = path.resolve(base);
  const alvo = path.resolve(raiz, relativo);
  const diferenca = path.relative(raiz, alvo);
  if (diferenca === '' || diferenca.startsWith('..') || path.isAbsolute(diferenca)) {
    return null;
  }

  return alvo;
}

/** PDF dentro da pasta base e que existe. */
export function documentoValido(base, relativo) {
  const completo = resolverDentro(base, relativo);
  if (!completo || path.extname(completo).toLowerCase() !== '.pdf') {
    return null;
  }

  try {
    return fs.statSync(completo).isFile() ? completo : null;
  } catch {
    return null;
  }
}
