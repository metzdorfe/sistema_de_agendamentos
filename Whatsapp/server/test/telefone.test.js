import assert from 'node:assert/strict';
import { test } from 'node:test';
import { alternativasNonoDigito, normalizarTelefone } from '../src/telefone.js';

test('celular com máscara ganha 55', () => {
  assert.deepEqual(normalizarTelefone('(47) 99999-1234'), { ok: true, numero: '5547999991234' });
});

test('número que já tem 55 não ganha outro', () => {
  assert.deepEqual(normalizarTelefone('+55 47 99999-1234'), { ok: true, numero: '5547999991234' });
});

test('fixo com 8 dígitos é aceito', () => {
  assert.deepEqual(normalizarTelefone('4733334444'), { ok: true, numero: '554733334444' });
});

test('DDD inexistente é recusado', () => {
  assert.equal(normalizarTelefone('2099999999').ok, false);
});

test('celular de 9 dígitos que não começa com 9 é recusado', () => {
  assert.equal(normalizarTelefone('47899991234').ok, false);
});

test('sem DDD, vazio ou nulo é recusado', () => {
  assert.equal(normalizarTelefone('999991234').ok, false);
  assert.equal(normalizarTelefone('').ok, false);
  assert.equal(normalizarTelefone(null).ok, false);
});

test('nono dígito: tenta com e sem o 9', () => {
  assert.deepEqual(alternativasNonoDigito('5547999991234'), ['5547999991234', '554799991234']);
  assert.deepEqual(alternativasNonoDigito('554799991234'), ['554799991234', '5547999991234']);
  assert.deepEqual(alternativasNonoDigito('554733334444'), ['554733334444']);
});
