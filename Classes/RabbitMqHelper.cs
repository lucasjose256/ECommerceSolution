using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Classes;

public static class RabbitMqHelper
{
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

  public static async Task ConsumeMessageEstoque(string queueName, List<Produto> produtosLista)
{
    var factory = new ConnectionFactory { HostName = "localhost" };

    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, 
        autoDelete: false, arguments: null);

    await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

    Console.WriteLine(" [*] Waiting for messages.");

    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (model, ea) =>
    {
        byte[] body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Received {message}");

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
                        produto.Estoque -= item.Quantidade;
                        Console.WriteLine($"Estoque atualizado: {produto.Nome} - Novo Estoque: {produto.Estoque}");
                    }
                }
            }

            // Confirmação da mensagem
            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar a mensagem: {ex.Message}");
        }
    };

    // Iniciando o consumo da fila
    await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

    // Aguardando o usuário pressionar Enter para sair
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
}


    public static async Task Publishold(string queueName, string json)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        // Cria a conexão e canal de forma assíncrona
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declara a fila
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // Codifica o JSON em bytes
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true // Garantir persistência da mensagem
        };

        // Publica a mensagem de forma assíncrona
        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"JSON enviado para a fila '{queueName}': {json}");
    }

    public static async Task Publish( string routingKey, string message)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        // Cria conexão e canal de forma assíncrona
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declara o exchange do tipo "topic" com durabilidade
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName, // Nome do exchange
            type: "topic",          // Tipo "topic"
            durable: true,          // Garantir que o exchange seja persistente
            autoDelete: false,
            arguments: null);

        // Codifica a mensagem em bytes
        var body = Encoding.UTF8.GetBytes(message);

        // Define propriedades da mensagem (persistência)
        var properties = new RabbitMQ.Client.BasicProperties
        {
            Persistent = true // Mensagens persistentes
        };

        // Publica a mensagem no exchange
        await channel.BasicPublishAsync(
            exchange: exchangeName, // Nome do exchange
            routingKey: routingKey, // Routing key para filtragem no "topic"
            mandatory: false,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"Mensagem publicada no tópico '{routingKey}' no exchange '{exchangeName}': {message}");
    }

}