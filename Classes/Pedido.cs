namespace Classes;

public class Pedido
{
    public int PedidoId { get; set; }
    public DateTime DataPedido { get; set; }
    public string Status { get; set; }//criado,enviado,processando pagamento
    public List<ItemPedido> Itens { get; set; } 
}
