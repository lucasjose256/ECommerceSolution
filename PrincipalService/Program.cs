using Microsoft.EntityFrameworkCore;
using Classes;
using PrincipalService; // Namespace com ApplicationDbContext

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
builder.Services.AddHttpClient();

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

app.PrincipalRoutes(app.Services.GetRequiredService<HttpClient>());


app.UseHttpsRedirection();
app.Run();
