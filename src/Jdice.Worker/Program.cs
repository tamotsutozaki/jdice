using Hangfire;
using Jdice.Application;
using Jdice.Infrastructure;
using Jdice.Worker;

// Processo separado da API de propósito. A API só enfileira e responde; quem
// executa disparo é este worker, que pode ser escalado sozinho quando o volume
// de envio crescer, sem multiplicar o servidor HTTP junto.
var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();

await host.RunAsync();
