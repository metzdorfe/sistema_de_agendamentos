import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { documentoValido, resolverDentro } from '../src/caminho.js';

const base = fs.mkdtempSync(path.join(os.tmpdir(), 'festagenda-arquivos-'));
fs.mkdirSync(path.join(base, 'WhatsApp'));
fs.writeFileSync(path.join(base, 'WhatsApp', 'catalogo.pdf'), '%PDF-1.7');
fs.writeFileSync(path.join(base, 'WhatsApp', 'nota.txt'), 'texto');

test('caminho relativo dentro da pasta é aceito', () => {
  assert.equal(resolverDentro(base, 'WhatsApp\\catalogo.pdf'), path.join(base, 'WhatsApp', 'catalogo.pdf'));
});

test('sair da pasta com .. é recusado', () => {
  assert.equal(resolverDentro(base, '..\\..\\Windows\\win.ini'), null);
  assert.equal(resolverDentro(base, 'WhatsApp\\..\\..\\fora.pdf'), null);
});

test('caminho absoluto e vazio são recusados', () => {
  assert.equal(resolverDentro(base, 'C:\\Windows\\win.ini'), null);
  assert.equal(resolverDentro(base, ''), null);
  assert.equal(resolverDentro(base, undefined), null);
});

test('documento precisa ser PDF existente', () => {
  assert.ok(documentoValido(base, 'WhatsApp\\catalogo.pdf'));
  assert.equal(documentoValido(base, 'WhatsApp\\nota.txt'), null);
  assert.equal(documentoValido(base, 'WhatsApp\\nao-existe.pdf'), null);
});
