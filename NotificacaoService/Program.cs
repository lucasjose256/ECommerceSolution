using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Channels;
using Classes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Canal de comunicação para SSE
var notificacaoChannel = Channel.CreateUnbounded<string>();

// Configuração do RabbitMQ
const string rabbitMqHost = "localhost";
string[] topicos = { "Pedidos_Criados", "Pagamentos_Aprovados", "Pagamentos_Recusados", "Pedidos_Enviados" };

// Conexão assíncrona com RabbitMQ e consumo de mensagens
Task.Run(async () =>
{
    RabbitMqHelper.Notificacoes();

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
