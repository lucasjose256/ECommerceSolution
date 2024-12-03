namespace Classes;

public class ItemPedido
{
    public int ItemPedidoId { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public Produto Produto { get; set; } 
    
}