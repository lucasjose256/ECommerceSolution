namespace PagamentoService.models;

public class ProductModel
{
    public  Guid id { get; set; }
    public string name { get; set; }=String.Empty;
    public float price  { get; set; }
}