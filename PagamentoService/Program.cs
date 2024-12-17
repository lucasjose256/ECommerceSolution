using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Classes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PagamentoService;
using PrincipalService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

if (app.Environment.IsDevelopment())
{

}

List<Pedido> pedidosChanel =new List<Pedido>();


RabbitMqHelper.ConsumePedidosCriados(pedidosChanel);


app.MapPost("/pagamento/{id}", async (int id, [FromBody] Pagamento pagamento) =>
{
    var pedidoEncontrado = pedidosChanel.FirstOrDefault(p => p.PedidoId == id);
    if (pedidoEncontrado is null)
        return Results.NotFound(new { mensagem = "Pedido não encontrado." });

    string statusPagamento = new Random().Next(2) == 0 ? "aprovado" : "recusado";
    pedidoEncontrado.Status = statusPagamento;

    Console.WriteLine($"Pagamento recebido:");
    Console.WriteLine($"Id :{pagamento.PedidoId}");

    Console.WriteLine($"Status: {statusPagamento}");
    Console.WriteLine($"Valor: {pagamento.Valor}");
    string pedidoJson = JsonSerializer.Serialize(pedidoEncontrado);

    using (var client = new HttpClient())
    {
        var webhookUrl = $"http://localhost:5053/webhook/pagamento/{id}"; 

        var webhookData = new
        {
            PedidoId = pedidoEncontrado.PedidoId,
            Status = statusPagamento,
            Valor = pagamento.Valor,
        };

        var content = new StringContent(JsonSerializer.Serialize(webhookData), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(webhookUrl, content);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Webhook chamado com sucesso.");
        }
        else
        {
            Console.WriteLine("Erro ao chamar o webhook.");
        }
    }
    
    return Results.Ok(new
    {
        PedidoId = pedidoEncontrado.PedidoId,
        Status = pedidoEncontrado.Status,
        Valor = pagamento.Valor
    });
});

app.UseHttpsRedirection();
app.Run();

