using Classes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();



List<Produto> produtosLista=  new List<Produto> {
    new Produto { Id = 1, Nome = "Notebook", Preco = 3500.00m, Estoque = 10, ImagemUrl = "https://example.com/notebook.jpg" },
    new Produto { Id = 2, Nome = "Smartphone", Preco = 2500.00m, Estoque = 20, ImagemUrl = "https://example.com/smartphone.jpg" },
    new Produto { Id = 3, Nome = "Headset", Preco = 300.00m, Estoque = 50, ImagemUrl = "https://example.com/headset.jpg" },
    new Produto { Id = 4, Nome = "Teclado Mecânico", Preco = 450.00m, Estoque = 15, ImagemUrl = "https://example.com/teclado.jpg" }
};

app.MapGet("/estoque", () =>
{
return produtosLista;
    
});
  
app.Run();

