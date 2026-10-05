-- ============================================================================
-- Fest Agenda — Dados iniciais (rodar depois do schema.sql, num banco novo)
-- CONFIGURACAO com a linha única (ID = 1) e as espécies de pagamento padrão.
-- O nome e os dados da empresa ficam em branco: ela preenche em Configurações → Empresa.
-- ============================================================================

INSERT INTO CONFIGURACAO (ID, BLOQUEIO_UM_POR_DIA, PERCENTUAL_JUROS_MES, PERCENTUAL_MULTA, PRAZO_CANCELAMENTO_DIAS, TEMA)
VALUES (1, TRUE, 2.00, 2.00, 7, 'C');

INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Dinheiro', FALSE, TRUE);
INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Pix', FALSE, TRUE);
INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Cartão de débito', FALSE, TRUE);
INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Cartão de crédito', TRUE, TRUE);
INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Transferência bancária', FALSE, TRUE);
INSERT INTO ESPECIE (DESCRICAO, PERMITE_PARCELAMENTO, ATIVO) VALUES ('Boleto', TRUE, TRUE);

COMMIT;
