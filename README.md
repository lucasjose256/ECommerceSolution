E-Commerce Distribuído
Projeto de avaliação da disciplina Sistemas Distribuídos - UTFPR Curitiba
Professora: Ana Cristina Barreiras Kochem Vendramin

✨ Descrição
Este projeto implementa um sistema de e-commerce utilizando arquitetura baseada em microsserviços com comunicação assíncrona através de um sistema de mensageria.

A aplicação é composta por:

Frontend Web (linguagem diferente do backend)

5 Microsserviços Backend

Sistema de Pagamento Externo com Webhook

Notificações em tempo real via SSE (Server-Sent Events)

🛠 Tecnologias utilizadas
Frontend
[Tecnologia usada, por exemplo: React.js]

Consome a API principal via REST ou gRPC

Recebe atualizações via SSE (Server-Sent Events)

Backend
Cada microsserviço é desenvolvido de forma independente:


Microsserviço	Descrição
Principal	Exposição de API REST/gRPC para o Frontend. Publica eventos de novos pedidos, atualiza status.
Estoque	Gerencia o estoque. Atualiza estoque conforme pedidos criados ou excluídos.
Pagamento	Integração com sistema externo via Webhook. Publica eventos de pagamento aprovado/recusado.
Entrega	Emite nota fiscal e gerencia o envio de produtos.
Notificação	Envia atualizações para o frontend usando SSE.
Sistema de Mensageria
[Tecnologia usada, por exemplo: RabbitMQ, Kafka]

Comunicação assíncrona entre microsserviços através de tópicos.

📑 Fluxo Resumido
Cliente interage com o Frontend (visualizar produtos, adicionar ao carrinho, finalizar pedido).

Frontend envia requisições para o Microsserviço Principal.

Microsserviço Principal publica eventos de Pedidos_Criados.

Microsserviços Estoque e Pagamento consomem o evento.

Sistema de pagamento externo processa o pagamento e envia retorno via Webhook.

Microsserviço Pagamento publica eventos de Pagamentos_Aprovados ou Pagamentos_Recusados.

Microsserviço Entrega gerencia a emissão e envio dos produtos.

Microsserviço Notificação envia atualizações via SSE para o Frontend.

📦 Endpoints Principais
Frontend
/api/produtos — Lista produtos disponíveis.

/api/carrinho — Adiciona, atualiza ou remove produtos no carrinho.

/api/pedidos — Cria, consulta ou exclui pedidos.

Webhook
/webhook/pagamento — Recebe notificações do sistema externo de pagamento.

SSE
/sse/notificacoes — Canal para receber eventos em tempo real sobre o status dos pedidos.

