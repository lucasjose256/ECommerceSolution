using System.Text.Json;
using Classes;
using Microsoft.AspNetCore.Mvc;

namespace PrincipalService.Controllers;
[ApiController]
[Route("api/[controller]")]
public class PrincipalController:Controller
{
    public static List<ItemPedido> Carrinho { get; set; } = new();
    public static List<Pedido> Pedidos { get; set; } = new();
        
        [HttpGet("disponiveis")]
    public async Task<IActionResult> GetProdutosDisponiveis()
    {
 var response = await HttpClient.GetAsync("/api/estoque/produtos");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<dynamic>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });        return Ok(produtos);
    }
    
    
}