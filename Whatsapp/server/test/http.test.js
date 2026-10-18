import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { criarFila } from '../src/fila.js';
import { criarServidorHttp } from '../src/http.js';
import { criarLog, limparTexto } from '../src/log.js';
import { criarConexaoSimulada } from '../src/simulado.js';

const TOKEN = 'token-de-teste-com-32-caracteres';
const pastaArquivos = fs.mkdtempSync(path.join(os.tmpdir(), 'festagenda-http-'));
fs.mkdirSync(path.join(pastaArquivos, 'WhatsApp'));
fs.writeFileSync(path.join(pastaArquivos, 'WhatsApp', 'catalogo.pdf'), '%PDF-1.7');
const arquivoLog = path.join(pastaArquivos, 'whatsapp.log');

let servidor;
let base;

before(async () => {
  const log = criarLog(arquivoLog);
  const conexao = criarConexaoSimulada({ log });
  const fila = criarFila({ limiteDiario: 3, intervaloMinimo: 0, intervaloMaximo: 0 });
  servidor = criarServidorHttp({ token: TOKEN, conexao, fila, pastaArquivos, log });
  await new Promise((resolve) => servidor.listen(0, '127.0.0.1', resolve));
  base = `http://127.0.0.1:${servidor.address().port}`;
});

after(() => servidor.close());

async function chamar(metodo, rota, corpo, token = TOKEN) {
  const resposta = await fetch(base + rota, {
    method: metodo,
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  return { status: resposta.status, corpo: await resposta.json() };
}

test('sem token ou com token errado: 401', async () => {
  assert.equal((await chamar('GET', '/health', undefined, 'errado')).status, 401);
  const semToken = await fetch(`${base}/health`);
  assert.equal(semToken.status, 401);
});

test('health e status respondem', async () => {
  assert.equal((await chamar('GET', '/health')).corpo.ok, true);
  assert.equal((await chamar('GET', '/status')).corpo.estado, 'desconectado');
});

test('enviar sem conexão: NAO_CONECTADO', async () => {
  const { corpo } = await chamar('POST', '/enviar/texto', { telefone: '47999991234', texto: 'Olá' });
  assert.equal(corpo.codigo, 'NAO_CONECTADO');
});

test('telefone inválido: TELEFONE_INVALIDO', async () => {
  const { corpo } = await chamar('POST', '/enviar/texto', { telefone: '123', texto: 'Olá' });
  assert.equal(corpo.codigo, 'TELEFONE_INVALIDO');
});

test('conecta, envia texto e documento; número sem WhatsApp e arquivo fora da pasta são recusados', async () => {
  await chamar('POST', '/conectar');
  // O simulado mostra o QR e conecta sozinho em alguns segundos
  for (let i = 0; i < 50 && (await chamar('GET', '/status')).corpo.estado !== 'conectado'; i++) {
    await new Promise((resolve) => setTimeout(resolve, 100));
  }

  assert.equal((await chamar('GET', '/status')).corpo.estado, 'conectado');
  assert.equal((await chamar('POST', '/enviar/texto', { telefone: '47999991234', texto: 'Olá' })).corpo.ok, true);
  assert.equal((await chamar('POST', '/enviar/texto', { telefone: '4733330000', texto: 'Olá' })).corpo.codigo, 'NUMERO_SEM_WHATSAPP');

  const documento = await chamar('POST', '/enviar/documento',
    { telefone: '47999991234', caminhoArquivo: 'WhatsApp\\catalogo.pdf', nomeArquivo: 'Catálogo.pdf', legenda: 'Segue' });
  assert.equal(documento.corpo.ok, true);

  const fora = await chamar('POST', '/enviar/documento', { telefone: '47999991234', caminhoArquivo: '..\\segredo.pdf' });
  assert.equal(fora.corpo.codigo, 'ARQUIVO_INVALIDO');

  // Limite de 3 por dia: já foram 2 enviados
  assert.equal((await chamar('POST', '/enviar/texto', { telefone: '47999991234', texto: 'Terceira' })).corpo.ok, true);
  assert.equal((await chamar('POST', '/enviar/texto', { telefone: '47999991234', texto: 'Quarta' })).corpo.codigo, 'LIMITE');
});

test('corpo grande demais: 413', async () => {
  const { status } = await chamar('POST', '/enviar/texto', { telefone: '47999991234', texto: 'x'.repeat(70 * 1024) });
  assert.equal(status, 413);
});

test('rota desconhecida: 404', async () => {
  assert.equal((await chamar('GET', '/nada')).status, 404);
});

test('log não guarda telefone completo nem texto da mensagem', () => {
  const conteudo = fs.readFileSync(arquivoLog, 'utf8');
  assert.ok(!conteudo.includes('47999991234'));
  assert.ok(!conteudo.includes('Terceira'));
  assert.equal(limparTexto('ligar para 5547999991234'), 'ligar para *********1234');
});
