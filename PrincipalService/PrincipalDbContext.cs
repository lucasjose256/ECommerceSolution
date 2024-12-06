using Classes;
using Microsoft.EntityFrameworkCore;

namespace PrincipalService;

public class PrincipalDbContext:DbContext
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