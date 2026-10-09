using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClaroFlowEngine.Api.Tests.Support;

/// <summary>
/// Servidor HTTP simulado com a forma de uma API compatível com OpenAI (FASE 4.4, B.4). Roda dentro do teste, em
/// porta livre, sem depender de Node ou Python. Guarda o corpo de cada requisição recebida, para conferir o que saiu
/// pela rede. O comportamento de cada cenário é definido pelo teste em <see cref="Handler"/>.
/// </summary>
public sealed class SimulatedAiServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private SimulatedAiServer(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/v1";

    /// <summary>Corpo (texto) de cada requisição recebida em /chat/completions, na ordem de chegada.</summary>
    public ConcurrentQueue<(string Body, string? Authorization)> Requests { get; } = new();

    /// <summary>Resposta do cenário atual. Padrão: 200 com conteúdo JSON válido.</summary>
    public Func<HttpContext, string, Task> Handler { get; set; } = (context, _) => Respond(context, 200, Envelope(ValidContent));

    public const string ValidContent =
        "{\"summary\":\"Cliente com 1 atendimento registrado, sobre troca de plano.\"," +
        "\"attention_points\":[\"Jornada aberta parada há 2 horas.\"]," +
        "\"suggested_approach\":\"Retomar a troca de plano de onde parou.\"}";

    public static async Task<SimulatedAiServer> StartAsync()
    {
        var port = FreePort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        var server = new SimulatedAiServer(app, port);
        app.MapPost("/v1/chat/completions", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            server.Requests.Enqueue((body, context.Request.Headers.Authorization.FirstOrDefault()));
            await server.Handler(context, body);
        });

        await app.StartAsync();
        return server;
    }

    public static string Envelope(string content) => JsonSerializer.Serialize(new
    {
        id = "sim-1",
        model = "modelo-sim",
        choices = new[] { new { index = 0, message = new { role = "assistant", content } } },
        usage = new { prompt_tokens = 120, completion_tokens = 60 },
    });

    public static Task Respond(HttpContext context, int status, string body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(body);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
