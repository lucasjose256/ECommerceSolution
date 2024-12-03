using Microsoft.EntityFrameworkCore;
using Classes; // Namespace com ApplicationDbContext

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});


// Configuração do DbContext com SQLite
builder.Services.AddDbContext<ApplicationDbContext>();

// Adiciona a documentação Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("AllowAllOrigins");

// Configura o middleware Swagger para desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Definindo as rotas para produtos
var produtosRoute = app.MapGroup("/produtos");

produtosRoute.MapPost("", async (ApplicationDbContext db, Produto produto) =>
{
    db.Produtos.Add(produto);
    await db.SaveChangesAsync();
    return Results.Created($"/produtos/{produto.ProdutoId}", produto);
});

produtosRoute.MapGet("/{id}", async (ApplicationDbContext db, int id) =>
{
    var produto = await db.Produtos.FindAsync(id);
    if (produto is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(produto);
});

produtosRoute.MapGet("", async (ApplicationDbContext db) =>
{
    var produto =  db.Produtos.ToList();
   
    return Results.Ok(produto);
});
produtosRoute.MapPut("/{id}", async (ApplicationDbContext db, int id, Produto produtoAtualizado) =>
{
    var produto = await db.Produtos.FindAsync(id);
    if (produto is null)
    {
        return Results.NotFound();
    }

    produto.Nome = produtoAtualizado.Nome;
    produto.Preco = produtoAtualizado.Preco;
    produto.Estoque = produtoAtualizado.Estoque;

    await db.SaveChangesAsync();
    return Results.Ok(produto);
});

produtosRoute.MapDelete("/{id}", async (ApplicationDbContext db, int id) =>
{
    var produto = await db.Produtos.FindAsync(id);
    if (produto is null)
    {
        return Results.NotFound();
    }

    db.Produtos.Remove(produto);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.UseHttpsRedirection();

app.Run();