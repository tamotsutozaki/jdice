using Jdice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Worker;

/// <summary>
/// Espera o banco estar com as migrations aplicadas antes de o worker começar
/// a trabalhar.
/// <para>
/// Quem aplica as migrations é a API. Se os dois fizessem isso, disputariam a
/// mesma operação na subida do compose. O worker apenas aguarda — sem isso,
/// ele tentaria ler tabelas que ainda não existem e entraria em ciclo de
/// reinício.
/// </para>
/// </summary>
public sealed class AguardarBancoPronto(
    IServiceScopeFactory scopeFactory,
    ILogger<AguardarBancoPronto> logger) : IHostedService
{
    private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TempoMaximoDeEspera = TimeSpan.FromMinutes(5);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var limite = DateTimeOffset.UtcNow.Add(TempoMaximoDeEspera);
        var avisou = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (await BancoEstaProntoAsync(cancellationToken))
            {
                logger.LogInformation("Banco pronto; o worker vai começar a processar.");
                return;
            }

            if (!avisou)
            {
                logger.LogInformation("Aguardando a API aplicar as migrations...");
                avisou = true;
            }

            if (DateTimeOffset.UtcNow > limite)
            {
                // Falhar é melhor que ficar esperando para sempre em silêncio:
                // o orquestrador reinicia e o problema aparece no log.
                throw new TimeoutException(
                    "O banco não ficou pronto no tempo esperado. A API aplicou as migrations?");
            }

            await Task.Delay(IntervaloEntreTentativas, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> BancoEstaProntoAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JdiceDbContext>();

            var pendentes = await context.Database.GetPendingMigrationsAsync(cancellationToken);

            return !pendentes.Any();
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // Banco ainda subindo, ou tabela de histórico inexistente.
            return false;
        }
    }
}
