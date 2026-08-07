using Hangfire;
using Jdice.Application;
using Jdice.Infrastructure;
using Jdice.Worker;
using Serilog;

// Processo separado da API de propósito. A API só enfileira e responde; quem
// executa disparo é este worker, que pode ser escalado sozinho quando o volume
// de envio crescer, sem multiplicar o servidor HTTP junto.
var builder = Host.CreateApplicationBuilder(args);

// O mesmo log estruturado da API. Com vários workers, saber de qual container
// veio cada linha é o que torna o log utilizável.
//
// Os níveis ficam em `Serilog:MinimumLevel` do appsettings, e não na seção
// `Logging` do ASP.NET: o AddSerilog substitui a ILoggerFactory, então os
// filtros do Microsoft.Extensions.Logging deixam de ser consultados.
builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacao", "Jdice.Worker")
    .Enrich.WithProperty("Worker", Environment.MachineName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration);

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = Environment.MachineName;

    // Quantos disparos este worker processa ao mesmo tempo. Baixo de
    // propósito: cada um percorre suas entregas em série, e SMTP costuma
    // limitar conexões simultâneas por remetente.
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 4);

    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// O worker sobe antes da API na largada e precisa esperar as tabelas
// existirem — inclusive as do próprio Hangfire.
builder.Services.AddHostedService<AguardarBancoPronto>();

// Consome as entregas repartidas na fila. Fica ocioso quando o RabbitMQ está
// desligado, e nesse caso o disparo roda em série dentro do próprio job.
builder.Services.AddHostedService<ConsumidorDeEntregas>();

var host = builder.Build();

await host.RunAsync();
