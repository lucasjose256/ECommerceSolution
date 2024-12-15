using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Classes;

public static class RabbitMqHelper

{
    public static string[] topicos = { "Pedidos-Criados", "Pagamentos-Aprovados", "Pagamentos-Recusados", "Pedidos-Enviados" };

    public static string exchangeName = "pedidos-exchange";
     public static async Task ConsumeMessageEntrega()
{
    string queueName="Pagamentos-Aprovados";
    var factory = new ConnectionFactory { HostName = "localhost" };

    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, 
        autoDelete: false, arguments: null);

    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

    Console.WriteLine(" [*] Waiting for messages.");

    var consumer = new AsyncEventingBasicConsumer(channel);
    string message = "";
    consumer.ReceivedAsync += async (model, ea) =>
    {
        byte[] body = ea.Body.ToArray();
         message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Received {message}");
    };
    await Publish("Pedidos-Enviados",message);
    // Iniciando o consumo da fila
    await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

    // Aguardando o usuário pressionar Enter para sair
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();

}
     public static async Task ConsumeMessageEstoque(List<Produto> produtosLista)
    {
        string[] topicos = { "Pedidos-Criados", "Pagamentos-Recusados" };
        var factory = new ConnectionFactory { HostName = "localhost" };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declara o Exchange
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

        // Declara a fila e vincula a fila ao tópico correto
        string filaPedidos = "fila-pedidos";
        await channel.QueueDeclareAsync(queue: filaPedidos, durable: true, exclusive: false, autoDelete: false);

        foreach (var topico in topicos)
        {
            await channel.QueueBindAsync(queue: filaPedidos, exchange: exchangeName, routingKey: topico);
        }

        Console.WriteLine(" [*] Aguardando mensagens...");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            string routingKeyReceived = ea.RoutingKey;

            Console.WriteLine($" [x] Mensagem recebida ({routingKeyReceived}): {message}");

            try
            {
                Pedido pedido = JsonSerializer.Deserialize<Pedido>(message);
                if (pedido?.Itens != null)
                {
                    foreach (var item in pedido.Itens)
                    {
                        var produto = produtosLista.FirstOrDefault(p => p.Id == item.ProdutoId);
                        if (produto != null)
                        {
                            // Lógica baseada no evento recebido
                            if (routingKeyReceived == "Pedidos-Criados")
                            {
                                AtualizarEstoque(produto, item.Quantidade, false);
                            }
                            else if (routingKeyReceived == "Pagamentos-Recusados")
                            {
                                AtualizarEstoque(produto, item.Quantidade, true);
                            }

                            Console.WriteLine($"Estoque atualizado: {produto.Nome} - Estoque Atual: {produto.Estoque}");
                        }
                    }
                }

                // Confirmação da mensagem
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [!] Erro ao processar a mensagem: {ex.Message}");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: filaPedidos, autoAck: false, consumer: consumer);
        Console.WriteLine(" [*] Consumidor iniciado. Pressione Ctrl+C para sair.");
        await Task.Delay(-1); // Mantém o consumidor ativo
    }
     private static void AtualizarEstoque(Produto produto, int quantidade, bool retornar)
    {
        if (retornar)
        {
            // Retorna a quantidade ao estoque (caso pagamento recusado ou pedido cancelado)
            produto.Estoque += quantidade;
        }
        else
        {
            // Deduz a quantidade, garantindo que o estoque não seja negativo
            if (produto.Estoque >= quantidade)
            {
                produto.Estoque -= quantidade;
            }
            else
            {
                Console.WriteLine($" [!] Estoque insuficiente para {produto.Nome}. Estoque atual: {produto.Estoque}");
            }
        }
    }
     public static async Task Publish(string routingKey,string message)
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    using var connection = await factory.CreateConnectionAsync();
    using var channel = await connection.CreateChannelAsync();

    await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);
    var body = Encoding.UTF8.GetBytes(message);
    await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
    Console.WriteLine($" [x] Sent '{routingKey}':'{message}'");
}

   public static async Task ProcessarPedidoAsync(Pedido pedido)
        {
            Random RandomGenerator = new();

            Console.WriteLine($"Iniciando o processamento do Pedido ID {pedido.PedidoId}...");

            // Simula um atraso no processamento (tempo aleatório entre 1 e 3 segundos)
            int delay = RandomGenerator.Next(1000, 3000);
            await Task.Delay(delay);

            // Processamento aleatório: Aprovação ou Recusa
            bool pagamentoAprovado = RandomGenerator.Next(0, 2) == 1;

            pedido.Status = pagamentoAprovado ? "aprovado" : "recusado";

            var pedidoJson = JsonSerializer.Serialize(pedido);

            // Simula a publicação do evento em uma fila (ou apenas loga no console)
            Console.WriteLine($"Pedido ID {pedido.PedidoId} processado. Status: {pedido.Status}");

            if (pagamentoAprovado)
            {
               Publish("Pagamentos-Aprovados", pedidoJson);
            }
            else
            { 
                Publish("Pagamentos-Recusados", pedidoJson);
            }
        }

        // Método de extensão para configuração de rotas
        public static async Task ConsumePedidosCriados()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            string queue = "fila-pedidos-pagamentos";
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

            await channel.QueueDeclareAsync(queue: queue, durable: true, exclusive: false, autoDelete: false);

            await channel.QueueBindAsync(queue: queue, exchange: exchangeName, routingKey: "Pedidos-Criados");


                

            Console.WriteLine(" [*] Consumidor 1 aguardando mensagens...");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [Consumidor 1] Recebida: {message}");
                Pedido pedido = JsonSerializer.Deserialize<Pedido>(message);

                ProcessarPedidoAsync(pedido);
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer);

            Console.ReadLine();
        }

        public static async Task Notificacoes(Channel<string> notificacaoChannel)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            // Conexão assíncrona com RabbitMQ
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            // Declara o exchange do tipo "Topic"
            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

            // Declara uma fila automática (com nome gerado pelo servidor)
            var queueDeclare = await channel.QueueDeclareAsync(
                queue: "notificacao",                  // Nome vazio para nome automático
                durable: false,             // Fila não persistente
                exclusive: true,            // Fila exclusiva para esta conexão
                autoDelete: true,           // A fila é apagada quando a conexão é encerrada
                arguments: null
            );

            var queueName = queueDeclare.QueueName;
            Console.WriteLine($"Fila automática declarada: {queueName}");

            // Faz o bind da fila para os tópicos
            foreach (var topico in topicos)
            {
                await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: topico);
                Console.WriteLine($"Bind criado para tópico: {topico}");
            }

            // Configura o consumidor
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var mensagem = Encoding.UTF8.GetString(body);
                Console.WriteLine($"Mensagem recebida do tópico '{ea.RoutingKey}': {mensagem}");
                await notificacaoChannel.Writer.WriteAsync($"[{ea.RoutingKey}] {mensagem}");

                await Task.CompletedTask; // Indica que o processamento foi concluído
            };

            // Inicia o consumo da fila
            await channel.BasicConsumeAsync(
                queue: queueName,      // Usa o nome correto da fila gerada automaticamente
                autoAck: true,         // Confirmação automática
                consumer: consumer
            );

            Console.WriteLine("Consumindo mensagens...");
            await Task.Delay(-1); // Mantém o consumidor ativo indefinidamente
        }

        
}