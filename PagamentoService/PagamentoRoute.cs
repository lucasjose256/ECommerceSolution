using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Classes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PagamentoService
{
    public static class PagamentoRoute
    {
        public static async Task ProcessarPedidoAsync(Pedido pedido)
        {
            Random RandomGenerator = new();

            Console.WriteLine($"Iniciando o processamento do Pedido ID {pedido.PedidoId}...");

            // Simula um atraso no processamento (tempo aleatório entre 1 e 3 segundos)
            int delay = RandomGenerator.Next(1000, 3000);
            await Task.Delay(delay);

            // Processamento aleatório: Aprovação ou Recusa
            bool pagamentoAprovado = RandomGenerator.Next(0, 2) == 1;

            pedido.Status = pagamentoAprovado ? "aprovado" : "recusado";

            var pedidoJson = JsonSerializer.Serialize(pedido);

            // Simula a publicação do evento em uma fila (ou apenas loga no console)
            Console.WriteLine($"Pedido ID {pedido.PedidoId} processado. Status: {pedido.Status}");

            if (pagamentoAprovado)
            {
                RabbitMqHelper.Publish("Pagamentos_Aprovados", pedidoJson);
            }
            else
            {
                RabbitMqHelper.Publish("Pagamentos_Recusados", pedidoJson);
            }
        }

        // Método de extensão para configuração de rotas
        public static async Task Consume()
        {
            string queueName = "Pedidos-Criados";
            var factory = new ConnectionFactory { HostName = "localhost" };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false,
                autoDelete: false, arguments: null);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new AsyncEventingBasicConsumer(channel);
            string message = "";
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");
                  Pedido pedido = JsonSerializer.Deserialize<Pedido>(message);

                ProcessarPedidoAsync(pedido);
            };
            // Iniciando o consumo da fila
            await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

            // Aguardando o usuário pressionar Enter para sair
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();



        }
    }
}