namespace Jdice.Application.Abstractions;

public sealed record EmailMessage(
    string ParaEmail,
    string ParaNome,
    string Assunto,
    string CorpoHtml);

/// <param name="Motivo">Vazio quando deu certo.</param>
/// <param name="Permanente">
/// Falha que não adianta repetir — endereço inexistente, domínio inválido. O
/// worker desiste na hora em vez de gastar as tentativas restantes.
/// </param>
public sealed record EmailSendResult(bool Enviado, string Motivo, bool Permanente)
{
    public static EmailSendResult Sucesso() => new(true, string.Empty, false);

    public static EmailSendResult FalhaTemporaria(string motivo) => new(false, motivo, false);

    public static EmailSendResult FalhaPermanente(string motivo) => new(false, motivo, true);
}

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage mensagem, CancellationToken cancellationToken = default);
}

/// <summary>Agendador de disparos. Isola a aplicação do Hangfire.</summary>
public interface ICampaignScheduler
{
    /// <summary>Agenda o processamento e devolve o identificador do job.</summary>
    string Schedule(Guid campaignId, DateTimeOffset quando);

    /// <returns><c>false</c> se o job já não existia — não é erro.</returns>
    bool Cancel(string jobId);
}
