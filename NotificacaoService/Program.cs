using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Channels;
using Classes;

var builder = WebApplication.CreateBuilder(args);
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
var notificacaoChannel = Channel.CreateUnbounded<string>();

Task.Run(async () =>
{
    while (true)
    {
      await  RabbitMqHelper.Notificacoes(notificacaoChannel); 
      await Task.Delay(1000);
    }
});

app.MapGet("/sse", async (HttpContext context) =>
{

    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive");

    var writer = context.Response.BodyWriter;
    var cancellationToken = context.RequestAborted;

    try
    {
        await foreach (var mensagem in notificacaoChannel.Reader.ReadAllAsync(cancellationToken))
        {
            var data = $"data: {mensagem}\n\n";
            await writer.WriteAsync(Encoding.UTF8.GetBytes(data), cancellationToken);
            await writer.FlushAsync(cancellationToken);
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