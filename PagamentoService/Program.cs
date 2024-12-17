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

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

}

List<Pedido> pedidosChanel =new List<Pedido>();


RabbitMqHelper.ConsumePedidosCriados(pedidosChanel);


// Endpoint para processar o pagamento
app.MapPost("/pagamento/{id}", async (int id, [FromBody] Pagamento pagamento) =>
{
    // Buscar o pedido com base no ID
    var pedidoEncontrado = pedidosChanel.FirstOrDefault(p => p.PedidoId == id);
    if (pedidoEncontrado is null)
        return Results.NotFound(new { mensagem = "Pedido não encontrado." });

    // Randomiza o status do pagamento
    string statusPagamento = new Random().Next(2) == 0 ? "aprovado" : "recusado";

    // Atualiza o status do pedido
    pedidoEncontrado.Status = statusPagamento;

    // Loga os detalhes do pagamento
    Console.WriteLine($"Pagamento recebido:");
    Console.WriteLine($"Nome: {pagamento.Nome}");
    Console.WriteLine($"Endereço: {pagamento.Endereco}");
    Console.WriteLine($"Status: {statusPagamento}");
    Console.WriteLine($"Valor: {pagamento.Valor}");
    string pedidoJson = JsonSerializer.Serialize(pedidoEncontrado);

    // Chama o webhook para notificar o sistema externo
    using (var client = new HttpClient())
    {
        var webhookUrl = $"http://localhost:5053/webhook/pagamento/{id}";  // URL do webhook

        // Cria o objeto para enviar no corpo da requisição
        var webhookData = new
        {
            PedidoId = pedidoEncontrado.PedidoId,
            Status = statusPagamento,
            Valor = pagamento.Valor,
        };

        var content = new StringContent(JsonSerializer.Serialize(webhookData), Encoding.UTF8, "application/json");

        // Envia a requisição HTTP POST para o webhook
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

    // Retorna a resposta ao cliente
    return Results.Ok(new
    {
        PedidoId = pedidoEncontrado.PedidoId,
        Status = pedidoEncontrado.Status,
        Valor = pagamento.Valor
    });
});

app.UseHttpsRedirection();
app.Run();

