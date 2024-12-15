using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Channels;
using Classes;

var builder = WebApplication.CreateBuilder(args);

// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");

// Canal de comunicação para SSE
var notificacaoChannel = Channel.CreateUnbounded<string>();

// Simulação de mensagens chegando do RabbitMQ
Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(5000); // Simula tempo de espera para notificações
        await notificacaoChannel.Writer.WriteAsync($"Mensagem enviada às {DateTime.Now}");
    }
});

app.MapGet("/sse", async (HttpContext context) =>
{
    // Definindo os headers necessários para SSE
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive");

    var writer = context.Response.BodyWriter;

    // Captura o cancelamento da conexão do cliente
    var cancellationToken = context.RequestAborted;

    try
    {
        // Loop para enviar mensagens do canal de notificações
        await foreach (var mensagem in notificacaoChannel.Reader.ReadAllAsync(cancellationToken))
        {
            var data = $"data: {mensagem}\n\n";
            await writer.WriteAsync(Encoding.UTF8.GetBytes(data), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            // Log opcional para debug
            Console.WriteLine($"Enviada: {mensagem}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Conexão encerrada pelo cliente.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro na SSE: {ex.Message}");
    }
});

app.Run();
