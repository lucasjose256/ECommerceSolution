using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Canal de comunicação para SSE
var notificacaoChannel = Channel.CreateUnbounded<string>();

// Configuração do RabbitMQ
const string rabbitMqHost = "localhost";
string[] topicos = { "pedidos_criados", "pagamentos_aprovados", "pagamentos_recusados", "pedidos_enviados" };

// Conexão assíncrona com RabbitMQ e consumo de mensagens
Task.Run(async () =>
{
    var factory = new ConnectionFactory() { HostName = rabbitMqHost };

    // Conexão assíncrona com RabbitMQ
    var connection = await factory.CreateConnectionAsync();
    var channel = await connection.CreateChannelAsync();

    foreach (var topico in topicos)
    {
        // Declaração de filas
        await channel.QueueDeclareAsync(
            queue: topico,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var mensagem = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Mensagem recebida do tópico '{topico}': {mensagem}");

            // Adiciona a mensagem ao canal SSE
            await notificacaoChannel.Writer.WriteAsync(mensagem);
        };

        await channel.BasicConsumeAsync(
            queue: topico,
            autoAck: true,
            consumer: consumer);

        Console.WriteLine($"Consumindo mensagens do tópico: {topico}");
    }

    Console.WriteLine("RabbitMQ consumidor ativo...");
    await Task.Delay(-1); // Mantém a tarefa ativa indefinidamente
});

// Endpoint SSE para envio de notificações ao frontend
app.MapGet("/sse", async (HttpContext context) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    var writer = context.Response.BodyWriter;

    await foreach (var mensagem in notificacaoChannel.Reader.ReadAllAsync())
    {
        var data = $"data: {mensagem}\n\n";
        await writer.WriteAsync(Encoding.UTF8.GetBytes(data));
        await writer.FlushAsync();
    }
});

app.Run();
