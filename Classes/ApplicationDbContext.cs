using Microsoft.EntityFrameworkCore;

namespace Classes;

public class ApplicationDbContext : DbContext
{
    public DbSet<Produto> Produtos { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<ItemPedido> ItensPedidos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=Ecomerce.sqlite");
        base.OnConfiguring(optionsBuilder);
    }
}
