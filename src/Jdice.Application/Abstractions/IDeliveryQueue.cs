namespace Jdice.Application.Abstractions;

/// <param name="CampaignId">A campanha a que a entrega pertence.</param>
/// <param name="DeliveryId">A entrega a processar.</param>
public sealed record DeliveryWork(Guid CampaignId, Guid DeliveryId);

/// <summary>
/// Fila de entregas individuais.
/// <para>
/// Existe para separar o "quando" do "quanto": o Hangfire decide a hora do
/// disparo, e a fila permite que muitos workers processem as entregas em
/// paralelo. Sem ela, um disparo de milhares de destinatários seria um job só,
/// percorrendo tudo em série, e acrescentar workers não adiantaria nada.
/// </para>
/// </summary>
public interface IDeliveryQueue
{
    /// <summary>Está configurada e disponível? Quando não, o disparo é processado em série.</summary>
    bool IsEnabled { get; }

    Task PublishAsync(
        IReadOnlyCollection<DeliveryWork> trabalhos,
        CancellationToken cancellationToken = default);
}
