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

     

    }
}