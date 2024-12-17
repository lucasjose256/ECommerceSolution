using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Classes;

public static class RabbitMqHelper

{
    public static string[] topicos = { "Pedidos-Criados", "Pagamentos-Aprovados", "Pagamentos-Recusados", "Pedidos-Enviados","Pedidos-Excluidos" };

    public static string exchangeName = "pedidos-exchange";
     public static async Task ConsumeMessageEntrega()
    {
    string routing="Pagamentos-Aprovados";
    var factory = new ConnectionFactory { HostName = "localhost" };

    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();
    await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);
    
    string queueName = "fila-pagamentos";
    await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
    await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routing);
    
    Console.WriteLine(" [*] Waiting for messages.");
    var consumer = new AsyncEventingBasicConsumer(channel);
    string message = "";
    consumer.ReceivedAsync += async (model, ea) =>
    {
        byte[] body = ea.Body.ToArray();
         message = Encoding.UTF8.GetString(body);
         Task.Delay(2000).Wait();
        Console.WriteLine($" [x] Received {message}");
         Task.Delay(6000).Wait();
        await Publish("Pedidos-Enviados",message);

    };


    await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();

}
     public static async Task ConsumeMessageEstoque(List<Produto> produtosLista)
    {
        string[] topicos = { "Pedidos-Criados", "Pagamentos-Recusados", "Pedidos-Excluidos" };
        var factory = new ConnectionFactory { HostName = "localhost" };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

        string filaPedidos = "estoque";
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

            Console.WriteLine($" [x] Mensagem rrecebida ({routingKeyReceived}): {message}");

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
                            if (routingKeyReceived == "Pedidos-Criados")
                            {
                                AtualizarEstoque(produto, item.Quantidade, false);
                            }
                            
                            else if (routingKeyReceived == "Pedidos-Excluidos")
                            {
                                AtualizarEstoque(produto, item.Quantidade, true);
                            }

                            Console.WriteLine($"Estoque atualizado: {produto.Nome} - Estoque Atual: {produto.Estoque}");
                        }
                    }
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: filaPedidos, autoAck: false, consumer: consumer);
        Console.WriteLine(" [*] Consumidor iniciado. Pressione Ctrl+C para sair.");
        await Task.Delay(-1); 
    }
     private static void AtualizarEstoque(Produto produto, int quantidade, bool retornar)
    {
        if (retornar)
        {
            produto.Estoque += quantidade;
        }
        else
        {
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

    public static async Task Publish(string routingKey, string message)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);
        var body = Encoding.UTF8.GetBytes(message);
        await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
        Console.WriteLine($" [x] Sent '{routingKey}':'{message}'");
    }
    public static async Task ConsumePedidosCriados(List<Pedido> pedidos)
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
                if (pedido != null)
                {
                    
                     pedidos.Add(pedido);
                  
                }
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer);

            Console.ReadLine();
        }

        public static async Task Notificacoes(Channel<string> notificacaoChannel)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

            var queueDeclare = await channel.QueueDeclareAsync(
                queue: "notificacao",            
                durable: false,            
                exclusive: true,           
                autoDelete: true,           
                arguments: null
            );

            var queueName = queueDeclare.QueueName;
            Console.WriteLine($"Fila automática declarada: {queueName}");

            foreach (var topico in topicos)
            {
                await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: topico);
                Console.WriteLine($"Bind criado para tópico: {topico}");
            }
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var mensagem = Encoding.UTF8.GetString(body);
                Console.WriteLine($"Mensagem recebida do tópico '{ea.RoutingKey}': {mensagem}");
                await notificacaoChannel.Writer.WriteAsync($"[{ea.RoutingKey}] {mensagem}");

                await Task.CompletedTask; 
            };

            await channel.BasicConsumeAsync(
                queue: queueName, 
                autoAck: true,         
                consumer: consumer
            );

            Console.WriteLine("Consumindo mensagens...");
            await Task.Delay(-1); 
        }

        public static async Task ConsumerPrincipal(Channel<Pedido> pedidoChanel)
        { 
            string[] topicos = { "Pagamentos-Aprovados", "Pagamentos-Recusados", "Pedidos-Enviados"};
            
            var factory = new ConnectionFactory() { HostName = "localhost" };
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

            var queueDeclare = await channel.QueueDeclareAsync(
                queue: "principal",              
                durable: false,             
                exclusive: true,          
                autoDelete: true,           
                arguments: null
            );

            var queueName = queueDeclare.QueueName;
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
                Pedido pedido = JsonSerializer.Deserialize<Pedido>(mensagem);

            if (pedido != null)
            {
                switch (ea.RoutingKey)
                {
                    case "Pagamentos-Aprovados":
                        pedido.Status="aprovado";
                        pedidoChanel.Writer.TryWrite(pedido);
                        Console.WriteLine("Processando pagamento aprovado.");
                        break;

                    case "Pagamentos-Recusados":
                        pedido.Status="recusado";
                        pedidoChanel.Writer.TryWrite(pedido);
                        Console.WriteLine("Processando pagamento recusado.");
                        Publish("Pedidos-Excluidos", JsonSerializer.Serialize(pedido));
                        break;

                    case "Pedidos-Enviados":
                        pedido.Status="enviado";
                        pedidoChanel.Writer.TryWrite(pedido);
                        Console.WriteLine("Processando pedido enviado.");
                        break;

                    default:
                        Console.WriteLine($"Tópico desconhecido: {ea.RoutingKey}");
                        break;
                }
            }

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