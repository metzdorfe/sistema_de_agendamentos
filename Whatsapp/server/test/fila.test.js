import assert from 'node:assert/strict';
import { test } from 'node:test';
import { criarFila } from '../src/fila.js';

/** Relógio falso: esperar() só avança o tempo. */
function relogio(inicio = new Date(2026, 9, 18, 10, 0, 0)) {
  let agora = inicio.getTime();
  const esperas = [];
  return {
    agora: () => new Date(agora),
    esperar: async (ms) => {
      esperas.push(ms);
      agora += ms;
    },
    avancar: (ms) => {
      agora += ms;
    },
    esperas,
  };
}

test('envia um por vez, na ordem', async () => {
  const r = relogio();
  const fila = criarFila({ limiteDiario: 10, agora: r.agora, esperar: r.esperar, aleatorio: () => 0 });
  const ordem = [];
  let emAndamento = 0;
  const tarefa = (n) => async () => {
    emAndamento++;
    assert.equal(emAndamento, 1, 'dois envios ao mesmo tempo');
    await Promise.resolve();
    ordem.push(n);
    emAndamento--;
    return { ok: true };
  };

  await Promise.all([fila.adicionar(tarefa(1)), fila.adicionar(tarefa(2)), fila.adicionar(tarefa(3))]);
  assert.deepEqual(ordem, [1, 2, 3]);
});

test('espera o intervalo entre envios, nunca antes do primeiro', async () => {
  const r = relogio();
  const fila = criarFila({ limiteDiario: 10, intervaloMinimo: 6000, intervaloMaximo: 15000, agora: r.agora, esperar: r.esperar, aleatorio: () => 0.5 });
  await fila.adicionar(async () => ({ ok: true }));
  await fila.adicionar(async () => ({ ok: true }));
  assert.deepEqual(r.esperas, [10500]);
});

test('limite diário recusa com LIMITE e zera no dia seguinte', async () => {
  const r = relogio();
  const fila = criarFila({ limiteDiario: 2, intervaloMinimo: 0, intervaloMaximo: 0, agora: r.agora, esperar: r.esperar });
  assert.equal((await fila.adicionar(async () => ({ ok: true }))).ok, true);
  assert.equal((await fila.adicionar(async () => ({ ok: true }))).ok, true);
  assert.equal((await fila.adicionar(async () => ({ ok: true }))).codigo, 'LIMITE');

  r.avancar(24 * 60 * 60 * 1000);
  assert.equal((await fila.adicionar(async () => ({ ok: true }))).ok, true);
});

test('falha não conta para o limite e erro da tarefa vira FALHA', async () => {
  const r = relogio();
  const fila = criarFila({ limiteDiario: 1, intervaloMinimo: 0, intervaloMaximo: 0, agora: r.agora, esperar: r.esperar });
  assert.equal((await fila.adicionar(async () => ({ ok: false, codigo: 'NUMERO_SEM_WHATSAPP' }))).ok, false);
  const quebrou = await fila.adicionar(async () => {
    throw new Error('caiu');
  });
  assert.equal(quebrou.codigo, 'FALHA');
  assert.equal((await fila.adicionar(async () => ({ ok: true }))).ok, true);
  assert.equal(fila.enviadosHoje, 1);
});
