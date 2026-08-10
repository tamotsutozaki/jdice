using Jdice.Domain.Campaigns;

namespace Jdice.Application.Abstractions;

public sealed record CampaignFilter(
    CampaignStatus? Situacao = null,
    int Pagina = 1,
    int TamanhoDaPagina = 25);

/// <summary>Contagem por situação, para exibir o andamento sem carregar tudo.</summary>
public sealed record DeliverySummary(
    int Total,
    int Pendentes,
    int Enviando,
    int Enviados,
    int Falhados,
    int Pulados)
{
    public int Concluidos => Enviados + Falhados + Pulados;

    public bool Terminou => Concluidos >= Total;
}

public interface ICampaignRepository
{
    Task<PagedResult<Campaign>> ListAsync(
        CampaignFilter filter,
        CancellationToken cancellationToken = default);

    Task<Campaign?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sem carregar as entregas — usado quando só interessa o cabeçalho.</summary>
    Task<Campaign?> FindHeaderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DeliverySummary> SummaryAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Entregas ainda por processar, em lotes, para não carregar milhares de
    /// uma vez.
    /// </summary>
    /// <param name="depoisDe">
    /// Cursor para percorrer o lote inteiro. Sem ele, quem só publica as
    /// entregas numa fila — sem mudar a situação delas — receberia sempre as
    /// mesmas primeiras e nunca chegaria ao fim da lista.
    /// </param>
    Task<IReadOnlyList<Delivery>> NextPendingAsync(
        Guid campaignId,
        int quantidade,
        Guid? depoisDe = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Delivery>> ListDeliveriesAsync(
        Guid campaignId,
        int limite,
        CancellationToken cancellationToken = default);

    /// <summary>Todas as entregas do disparo, em streaming, para exportar sem carregar tudo na memória.</summary>
    IAsyncEnumerable<Delivery> StreamDeliveriesAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<Delivery?> FindDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
