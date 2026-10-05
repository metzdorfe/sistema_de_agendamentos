-- ============================================================================
-- Fest Agenda — Alterações de 30/09/2026
-- Atualiza um banco criado antes desta data para o modelo atual. Banco novo: usar schema.sql + dados-iniciais.sql.
--
-- 1. Logo da empresa deixa de ser BLOB no banco e vira arquivo em Arquivos\Empresa;
--    o banco guarda só o caminho relativo (CAMINHO_LOGO_EMPRESA). Novo prefixo CAMINHO_.
--    A logo antiga (se havia) NÃO é migrada: basta escolher de novo em Configurações → Empresa.
-- 2. Tema padrão passa a ser o claro ('C'). A linha existente mantém o tema já escolhido.
-- ============================================================================

CREATE DOMAIN D_CAMINHO_ARQUIVO AS VARCHAR(260);
COMMENT ON DOMAIN D_CAMINHO_ARQUIVO IS 'Caminho de arquivo relativo à pasta Arquivos\ do sistema (ex.: Empresa\logo.png). O arquivo fica fora do banco.';

ALTER TABLE CONFIGURACAO ADD CAMINHO_LOGO_EMPRESA D_CAMINHO_ARQUIVO;
COMMIT;

COMMENT ON COLUMN CONFIGURACAO.CAMINHO_LOGO_EMPRESA IS 'Logo da empresa: caminho relativo à pasta Arquivos\ (ex.: Empresa\logo_20260930142501.png). Opcional; sem logo ou arquivo sumido, a interface mostra só o nome.';

ALTER TABLE CONFIGURACAO DROP LOGO_EMPRESA;
COMMIT;

DROP DOMAIN D_IMAGEM;

ALTER DOMAIN D_TEMA SET DEFAULT 'C';
COMMENT ON DOMAIN D_TEMA IS 'E=Escuro, C=Claro (padrão), S=Seguir o Windows.';
COMMENT ON COLUMN CONFIGURACAO.TEMA IS 'E=Escuro, C=Claro (padrão), S=Seguir o Windows.';

COMMIT;
