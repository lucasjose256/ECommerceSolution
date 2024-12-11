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

public static async Task ConsumeMessageEstoque(string routingKey, List<Produto> produtosLista)
{
    var factory = new ConnectionFactory { HostName = "localhost" };

    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

// declare a server-named queue
   // QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
   // string queueName = queueDeclareResult.QueueName;

   await channel.QueueDeclareAsync(queue: "fila-pedidos", durable: true, exclusive: false, autoDelete: false);

    await channel.QueueBindAsync(queue: "fila-pedidos", exchange: exchangeName, routingKey: "Pedidos-Criados");



    Console.WriteLine(" [*] Aguardando mensagens...");

    var consumer = new AsyncEventingBasicConsumer(channel);

    // Tratamento assíncrono das mensagens recebidas
    consumer.ReceivedAsync += async (model, ea) =>
    {
        byte[] body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Mensagem recebida: {message}");

        try
        {
            // Desserializa o JSON para um objeto Pedido
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

            // Confirmação de processamento da mensagem
            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar a mensagem: {ex.Message}");
        }
    };

    await channel.BasicConsumeAsync(
        queue: "fila-pedidos",
        autoAck: false,
        consumer: consumer
    );
    Console.WriteLine(" Pressione [enter] para sair.");
    Console.ReadLine();
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


}