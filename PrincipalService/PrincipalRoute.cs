using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Classes;

namespace PrincipalService;

public static class PrincipalRoute
{
    public static List<ItemPedido> cart = new List<ItemPedido>();

    public static List<Produto> produtos = new List<Produto>();
    public static List<Pedido> pedidos = new List<Pedido>();
    static int idCounter = 1;
/*
    static void PublishToQueue(string queueName, string message)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
    }*/
    public static void PrincipalRoutes(this WebApplication app,  HttpClient httpClient)
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
                    // Lê o conteúdo da resposta e faz o parse para uma lista de produtos
                     produtos = await response.Content.ReadFromJsonAsync<List<Produto>>();
                    return Results.Ok(produtos); // Retorna os produtos para o cliente
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
            
        // Endpoint para adicionar um item ao carrinho
        app.MapPost("/cart/{produtoId}", async ([FromBody] ItemResponse item) =>
        {
            // Busca o produto pelo ID
            var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            if (produto == null)
            {
                return Results.NotFound(new { mensagem = "Produto não encontrado." });
            }

            // Verifica se há estoque suficiente
            if (produto.Estoque < item.Quantidade)
            {
                return Results.BadRequest(new { mensagem = "Estoque insuficiente." });
            }

            // Atualiza o estoque do produto
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
                // Se o item não existe no carrinho, adiciona um novo item
                cart.Add(new ItemPedido
                {
                    ProdutoId = item.ProdutoId,
                    NomeProduto = produto.Nome,
                    Quantidade = item.Quantidade,
                    Preco = produto.Preco
                });
            }

            // Retorna sucesso com a mensagem
            return Results.Ok(new { mensagem = "Produto adicionado ao carrinho." });
        });
        app.MapDelete("/cart/{id}", (int id) =>
        {
            var item = cart.FirstOrDefault(p => p.ProdutoId == id);
            if (item is null) return Results.NotFound("Item não encontrado.");

            cart.Remove(item);
            //    PublishToQueue("Pedidos_Cancelados", JsonSerializer.Serialize(pedido));

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

        app.MapPost("/criar-pedido",async (Pedido novoPedido) =>
        {
            novoPedido.PedidoId = idCounter++;
            novoPedido.DataPedido = DateTime.Now;
            novoPedido.Status = "criado";
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


    }
}
public class ItemResponse
{
    public int Quantidade { get; set; }
    public int ProdutoId { get; set; }

}
