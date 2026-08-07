namespace Jdice.Application.Campaigns;

public sealed class CampaignNotFoundException(Guid campaignId)
    : Exception($"Não existe disparo com o identificador '{campaignId}'.")
{
    public Guid CampaignId { get; } = campaignId;
}

/// <summary>Nenhum destinatário sobrou depois de excluir descadastrados e repetidos.</summary>
public sealed class NoRecipientsException()
    : Exception("Nenhum destinatário disponível para este disparo.");

public sealed class CampaignStateException(string message) : Exception(message);
