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
    {    public static string exchangeName = "pedidos-exchange";

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
        public static async Task ConsumePedidosCriados()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            string queue = "fila-pedidos-pagamentos";
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic);

// declare a server-named queue
  

            
                await channel.QueueBindAsync(queue: queue, exchange: exchangeName, routingKey: "Pedidos-Criados");
                

            Console.WriteLine(" [*] Consumidor 1 aguardando mensagens...");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [Consumidor 1] Recebida: {message}");

                // Confirmação manual
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer);

            Console.ReadLine();
        }

    }
}