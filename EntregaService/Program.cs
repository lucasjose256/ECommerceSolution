using Classes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{

}

app.UseHttpsRedirection();
RabbitMqHelper.ConsumeMessageEntrega();

app.Run();
