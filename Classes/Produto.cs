namespace Classes;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public decimal Preco { get; set; }
    public int Estoque { get; set; }  
    public string ImagemUrl { get; set; }  

}


public class NotaFiscal
{

    public int Id { get; set; }
    public string Nome { get; set; }
    public decimal Preco { get; set; }
    public string Endereco { get; set; }
    public string CNPJ { get; set; }

}