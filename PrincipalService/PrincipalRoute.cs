using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Classes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PrincipalService;

public static class PrincipalRoute
{
    public static List<ItemPedido> cart = new List<ItemPedido>();
    public static List<NotaFiscal> notasFiscais = new List<NotaFiscal>();
    public static List<Produto> produtos = new List<Produto>();
    public static List<Pedido> pedidos = new List<Pedido>();
    public static Channel<Pedido> pedidosChannel = Channel.CreateUnbounded<Pedido>();
    static int idCounter = 1;
    static PrincipalRoute()
    {
        RabbitMqHelper.ConsumerPrincipal(pedidosChannel);
        ProcessarPedidos(pedidosChannel,pedidos);
    }
    
    public static async Task ProcessarPedidos(Channel<Pedido> notificacaoChannel, List<Pedido> listaDePedidos)
    {
        await foreach (var pedido in notificacaoChannel.Reader.ReadAllAsync())
        {
            Pedido pedidoExistente = listaDePedidos.FirstOrDefault(p => p.PedidoId == pedido.PedidoId);
            
            if (pedidoExistente != null)
            {
                pedidoExistente.Status = pedido.Status;  
                Console.WriteLine($"Pedido {pedido.PedidoId} atualizado com o status {pedido.Status}. Total de pedidos na lista: {listaDePedidos.Count}");
            }
            else
            {
                listaDePedidos.Add(pedido);
                Console.WriteLine($"Novo pedido {pedido.PedidoId} adicionado. Total de pedidos na lista: {listaDePedidos.Count}");
            }
        }
    }

    public static void PrincipalRoutes(this WebApplication app, HttpClient httpClient)
    {
        app.MapGet("/produtos", async () =>
        {
            try
            {
                var response = await httpClient.GetAsync("http://localhost:5275/estoque");
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

        app.MapDelete("/pedidos/{id}", async (int id) =>
        {
            var pedido = pedidos.FirstOrDefault(p => p.PedidoId == id);
            if (pedido is null) return Results.NotFound("Pedido não encontrado.");


            string pedidoJson = JsonSerializer.Serialize(pedido);
            await Task.Delay(2000);
            await RabbitMqHelper.Publish("Pedidos-Excluidos", pedidoJson);

            pedidos.Remove(pedido);


            return Results.NoContent();
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
                notasFiscais.Add(new NotaFiscal
                {
                    Id = pedidoEncontrado.PedidoId,
                    Nome = "Rodrigo",
                    Preco = webhookRequest.Valor,
                    Endereco = "Avenida Sete de Setembro 3195",
                    CNPJ = "75.101.873/0008-66"
                });
                await RabbitMqHelper.Publish("Pagamentos-Aprovados", pedidoJson);
            }
            else if (webhookRequest.Status == "recusado")
            {
                await RabbitMqHelper.Publish("Pagamentos-Recusados", pedidoJson);
            }
            return Results.Ok(new { mensagem = "Webhook processado com sucesso." });
        });

        app.MapGet("/notasfiscais", () =>
        {
            Console.WriteLine("Notas Fiscais:");
            foreach (var nota in notasFiscais)
            {
                Console.WriteLine(JsonSerializer.Serialize(nota));
            }
            return notasFiscais;
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


