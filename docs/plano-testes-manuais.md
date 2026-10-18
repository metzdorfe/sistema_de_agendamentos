# Plano de testes manuais – Fest Agenda

Roteiro para a cliente ou o grupo validarem o sistema numa instalação limpa. Marque cada item com ✔ (ok) ou ✘ (anote o que aconteceu). Use um computador com Windows 10 ou 11 **sem** .NET e **sem** Firebird instalados.

## 1. Instalação

1. Rodar `FestAgenda-Setup-1.0.0.exe` e instalar na pasta padrão (`C:\FestAgenda`).
2. Abrir pelo atalho. A tela de abertura aparece e depois a Agenda (vazia), com a janela maximizada.
3. Abrir o sistema de novo pelo atalho com ele já aberto: **não** abre outra janela; a que estava aberta vem para a frente.
4. Fechar e abrir de novo: abre normalmente.

## 2. Configurações

1. Configurações → Empresa: preencher nome, CNPJ, telefone, e-mail e endereço; escolher uma logo. Salvar. O nome e a logo aparecem no canto da barra lateral.
2. CNPJ inválido (ex.: 11.111.111/1111-11): mostra erro junto do campo e não salva.
3. Aparência: trocar Claro → Escuro → Seguir o Windows. A troca vale na hora, em todas as telas.
4. Parâmetros: conferir juros 2%, multa 2%, prazo de cancelamento 7 dias e "um agendamento por dia" ligado.

## 3. Cadastros

1. Clientes → Novo: cadastrar um cliente pessoa física (com CPF) e outro pessoa jurídica (com CNPJ).
2. CPF repetido ou inválido: mostra erro e não salva.
3. Editar um cliente e inativar outro. O inativo não aparece para novos agendamentos.
4. Serviços → Novo: um serviço com 3 faixas de preço e 2 adicionais (um cobrado por quantidade, outro fixo). Reordenar as faixas.

## 4. Agendamento e faturamento

1. Na Agenda, clicar num dia → Novo agendamento. Criar um **orçamento sem sinal**. O dia não fica bloqueado para o serviço.
2. Criar outro orçamento **com sinal** (R$ 200, Pix) em outro dia. O dia fica bloqueado para esse serviço.
3. Tentar agendar o mesmo serviço no dia bloqueado: o sistema avisa e não deixa.
4. Tentar um sinal maior que o total: o sistema avisa e não deixa.
5. Abrir o orçamento com sinal → Agendar. Depois **Faturar**: Pix hoje + cartão em 3x. A soma das parcelas fecha exatamente com o valor a faturar.
6. Abrir o agendamento faturado: tudo fica só para leitura e aparece o cartão **Lucro do evento**.
7. Evento das 23:00 às 02:00: salva normalmente e aparece no dia em que começa.

## 5. Financeiro

1. Contas a receber → dar baixa numa parcela em dia: entra no Caixa.
2. Mudar a data de vencimento de uma parcela para o passado (ou esperar vencer) e dar baixa: multa e juros aparecem calculados. Exemplo de conferência: parcela de R$ 1.000,00, 15 dias de atraso → total R$ 1.030,00.
3. Baixa parcial com "gerar nova parcela com a diferença": a diferença aparece como nova parcela.
4. **Estornar baixa** dessa parcela: ela volta a ficar em aberto, a diferença é cancelada e sai o valor no Caixa.
5. Caixa → **Novo lançamento**: uma despesa do evento (garçom), um suprimento e uma sangria. O lucro do evento desconta a despesa; suprimento e sangria não mudam o "Recebido" da Visão geral.
6. Caixa → **Estornar** o sinal de um orçamento: o dia deixa de ficar bloqueado.

## 6. Cancelamento

1. Cancelar um agendamento com o evento a mais de 7 dias: pede o motivo; o que foi pago sai do Caixa como devolução e as parcelas em aberto ficam canceladas.
2. Cancelar um com o evento a menos de 7 dias: não há devolução.
3. Tentar cancelar um evento que já passou: o sistema não deixa.

## 7. Relatórios

1. Gerar cada relatório em PDF e em Excel (Agendamentos, Caixa por cliente, Faturamento). O arquivo vai para Downloads.
2. Conferir os totais com uma conta manual de 2 ou 3 agendamentos.
3. Catálogo em PDF: preços atuais, logo, dados da empresa e acentos corretos.

## 8. WhatsApp (usar um número só do negócio)

1. Configurações → WhatsApp: com o WhatsApp desligado, o QR aparece borrado.
2. Ligar o interruptor → **Conectar**: o QR fica nítido. Ler pelo celular (WhatsApp → Aparelhos conectados → Conectar um aparelho).
3. Conectado: o QR fica borrado com um check verde e aparece o número.
4. **Mensagens**: editar o texto do orçamento, inserir o campo "Data do evento", conferir a prévia, salvar.
5. Abrir um orçamento → **Enviar por WhatsApp**: revisar o texto e enviar. A mensagem chega no celular do cliente.
6. Relatórios → Catálogo → **Enviar por WhatsApp**: o PDF chega.
7. Enviar para um número sem WhatsApp: aparece o motivo e a mensagem fica em **Envios** com falha; **Reenviar** funciona depois.
8. Desmarcar "Aceita receber mensagens pelo WhatsApp" num cliente: a tela de envio explica que ele não aceita.
9. Fechar o sistema: no Gerenciador de Tarefas não sobra nenhum `node.exe`.
10. Abrir de novo: continua conectado, sem novo QR.
11. Desligar a internet e tentar enviar: aparece o aviso e o resto do sistema funciona normalmente.

## 9. Atualização e desinstalação

1. Com dados cadastrados, instalar uma versão nova por cima: os dados continuam, o WhatsApp continua conectado.
2. Desinstalar: as pastas `BD`, `Arquivos`, `Log`, `Whatsapp\sessao` e o `config.ini` continuam na pasta do sistema.

## 10. Robustez

1. Renomear o `config.ini` e abrir: a tela de abertura mostra "Arquivo de configuração não encontrado" e o botão para criar.
2. Apagar o arquivo da logo em `Arquivos\Empresa`: o sistema abre e mostra as iniciais no lugar da logo.
3. Usuário comum do Windows (não administrador): consegue cadastrar, gerar relatórios e enviar pelo WhatsApp.
