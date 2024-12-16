using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Classes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PrincipalService;

public static class PrincipalRoute
{
    public static List<ItemPedido> cart = new List<ItemPedido>();
    public static List<Produto> produtos = new List<Produto>();
    public static List<Pedido> pedidos = new List<Pedido>();
    static int idCounter = 1;

    static PrincipalRoute()
    {
        RabbitMqHelper.ConsumerPrincipal(pedidos);
    }
    
    
    public static void PrincipalRoutes(this WebApplication app, HttpClient httpClient)
    {
        // Endpoint para carregar produtos da API externa
        app.MapGet("/produtos", async () =>
        {
            try
            {
                // Realiza a requisição para a API externa
                var response = await httpClient.GetAsync("http://localhost:5275/estoque");

                // Verifica se a resposta foi bem-sucedida
                if (response.IsSuccessStatusCode)
                {
                    produtos = await response.Content.ReadFromJsonAsync<List<Produto>>();
                    return Results.Ok(produtos);
                }
                else
                {
                    return Results.StatusCode((int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                return Results.StatusCode(500);
            }
        });

        app.MapPost("/cart/{produtoId}", async ([FromBody] ItemResponse item) =>
        {
            var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            if (produto == null)
            {
                return Results.NotFound(new { mensagem = "Produto não encontrado." });
            }
            if (produto.Estoque < item.Quantidade)
            {
                return Results.BadRequest(new { mensagem = "Estoque insuficiente." });
            }
            produto.Estoque -= item.Quantidade;
            //chamar a api de estoque para mudar o valor
            //ou usar os eventos
            // Busca se o item já existe no carrinho
            var itemExistente = cart.FirstOrDefault(c => c.ProdutoId == item.ProdutoId);

            if (itemExistente != null)
            {
                itemExistente.Quantidade += item.Quantidade;
            }
            else
            {
                cart.Add(new ItemPedido
                {
                    ProdutoId = item.ProdutoId,
                    NomeProduto = produto.Nome,
                    Quantidade = item.Quantidade,
                    Preco = produto.Preco
                });
            }

            return Results.Ok(new { mensagem = "Produto adicionado ao carrinho." });
        });
        app.MapDelete("/cart/{id}", (int id) =>
        {
            var item = cart.FirstOrDefault(p => p.ProdutoId == id);
            if (item is null) return Results.NotFound("Item não encontrado.");

            cart.Remove(item);
            //PublishToQueue("Pedidos_Cancelados", JsonSerializer.Serialize(pedido));

            return Results.NoContent();
        });
        app.MapGet("/cart", () =>
        {
            return cart;
        });

        app.MapGet("/pedidos", () => pedidos);


        app.MapGet("/pedidos/{id}", (int id) =>
        {
            var pedido = pedidos.FirstOrDefault(p => p.PedidoId == id);
            return pedido is not null ? Results.Ok(pedido) : Results.NotFound("Pedido não encontrado.");
        });


        app.MapPost("/criar-pedido", async (Pedido novoPedido) =>
        {
            //precisa alterar a logica do contador pq ele modifica o id e cria outro intem na lista


            novoPedido.PedidoId = idCounter++;
            novoPedido.DataPedido = DateTime.Now;
            novoPedido.Status = "criado";
            string pedidoJson = JsonSerializer.Serialize(novoPedido);
            cart.Clear();
            await RabbitMqHelper.Publish("Pedidos-Criados", $"{pedidoJson}");

            pedidos.Add(novoPedido);

            // Publica evento no RabbitMQ
            //  PublishToQueue("Pedidos_Criados", JsonSerializer.Serialize(novoPedido));

            return Results.Created($"/api/pedidos/{novoPedido.PedidoId}", novoPedido);
        }).WithName("CreatePedido");

        app.MapPut("/pedidos/{id}", async (int id, Pedido pedidoAtualizado) =>
        {
            var pedido = pedidos.FirstOrDefault(p => p.PedidoId == id);
            if (pedido is null) return Results.NotFound("Pedido não encontrado.");

            pedido.Status = pedidoAtualizado.Status;
            //    PublishToQueue("Pedidos_Atualizados", JsonSerializer.Serialize(pedido));

            return Results.Ok(pedido);
        });

        app.MapDelete("/pedidos/{id}", (int id) =>
        {
            var pedido = pedidos.FirstOrDefault(p => p.PedidoId == id);
            if (pedido is null) return Results.NotFound("Pedido não encontrado.");

            pedidos.Remove(pedido);
            //    PublishToQueue("Pedidos_Cancelados", JsonSerializer.Serialize(pedido));

            return Results.NoContent();
        });
        string ProcessarPagamentoAleatoriamente()
        {
            var random = new Random();
            // 50% de chance de pagamento aprovado ou recusado
            return random.Next(2) == 0 ? "Aprovado" : "Recusado";
        }

        app.MapPost("/pagamentos/{id}", async (int id, [FromBody] Pagamento pagamento) =>
        {
            // Find the corresponding pedido
            var pedidoEncontrado = pedidos.FirstOrDefault(p => p.PedidoId == id);
            if (pedidoEncontrado is null)
                return Results.NotFound(new { mensagem = "Pedido não encontrado." });

            // Randomize payment status
            string statusPagamento = new Random().Next(2) == 0 ? "aprovado" : "recusado";

            // Update pedido status based on payment result
            pedidoEncontrado.Status = statusPagamento;

            // Log received payment details
            Console.WriteLine($"Pagamento recebido:");
            Console.WriteLine($"Nome: {pagamento.Nome}");
            Console.WriteLine($"Endereço: {pagamento.Endereco}");
            Console.WriteLine($"Status: {statusPagamento}");
            Console.WriteLine($"Valor: {pagamento.Valor}");

            //string pedidoJson = JsonSerializer.Serialize(pedidoEncontrado);
            //cart.Clear();
            //await RabbitMqHelper.Publish("Pedidos-Criados", $"{pedidoJson}");

            String pedidoJson = JsonSerializer.Serialize(pedidoEncontrado);
            //cart.Clear();
            Task.Delay(2000).Wait();

            if (statusPagamento == "aprovado")
            {
                await RabbitMqHelper.Publish("Pagamentos-Aprovados", $"{pedidoJson}");

            }
            else if (statusPagamento == "recusado")
            {
                await RabbitMqHelper.Publish("Pagamentos-Recusados", $"{pedidoJson}");

            }


            return Results.Ok(new
            {
                PedidoId = pedidoEncontrado.PedidoId,
                Status = pedidoEncontrado.Status,
                Valor = pagamento.Valor
            });
        });

        app.MapPost("/webhook/pagamento/{id}", async (HttpContext context) =>
        {
            
            var webhookRequest = await context.Request.ReadFromJsonAsync<Pagamento>();

            if (webhookRequest == null)
                return Results.BadRequest("Dados inválidos no webhook");

            Console.WriteLine($"Webhook Recebido:");
            Console.WriteLine($"Pedido ID: {webhookRequest.PedidoId}");
            Console.WriteLine($"Status: {webhookRequest.Status}");
            var pedidoEncontrado = pedidos.FirstOrDefault(p => p.PedidoId == webhookRequest.PedidoId);
            string pedidoJson = JsonSerializer.Serialize(pedidoEncontrado);

            await Task.Delay(2000);
            // Publica o pagamento no RabbitMQ
            if (webhookRequest.Status == "aprovado")
            {
                await RabbitMqHelper.Publish("Pagamentos-Aprovados", pedidoJson);
            }
            else if (webhookRequest.Status == "recusado")
            {
                await RabbitMqHelper.Publish("Pagamentos-Recusados", pedidoJson);
            }
            return Results.Ok(new { mensagem = "Webhook processado com sucesso." });
        });


    }
}

public class Pagamento
{
    public int PedidoId { get; set; }
    public string Status { get; set; }
    public decimal Valor { get; set; }
    public string Nome { get; set; }
    public string Endereco { get; set; }
}
public class ItemResponse
{
    public int Quantidade { get; set; }
    public int ProdutoId { get; set; }

}


