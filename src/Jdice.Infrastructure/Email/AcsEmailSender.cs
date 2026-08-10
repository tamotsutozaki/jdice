using Azure;
using Jdice.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// EmailMessage e EmailSendResult existem nos dois lados; o alias deixa claro
// quando se fala do SDK da Azure e quando se fala do contrato da aplicação.
using Acs = Azure.Communication.Email;

namespace Jdice.Infrastructure.Email;

/// <summary>
/// Envio pelo Azure Communication Services — Email, via SDK oficial. Mesma
/// interface do envio por SMTP: quem decide qual usar é a configuração, não o
/// resto do sistema.
/// <para>
/// Usa <see cref="WaitUntil.Started"/>: espera o serviço aceitar a mensagem, não
/// a entrega final. É o análogo do 250 do SMTP — segurar o worker até o ACS
/// concluir a entrega gastaria minutos num disparo grande.
/// </para>
/// </summary>
public sealed class AcsEmailSender : IEmailSender
{
    private readonly Acs.EmailClient _client;
    private readonly AcsOptions _options;
    private readonly ILogger<AcsEmailSender> _logger;

    public AcsEmailSender(IOptions<AcsOptions> options, ILogger<AcsEmailSender> logger)
    {
        _options = options.Value;
        _client = new Acs.EmailClient(_options.ConnectionString);
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage mensagem,
        CancellationToken cancellationToken = default)
    {
        var conteudo = new Acs.EmailContent(mensagem.Assunto)
        {
            Html = mensagem.CorpoHtml,

            // Alguns clientes não mostram HTML; sem alternativa em texto, a
            // mensagem chega em branco.
            PlainText = ParaTextoSimples(mensagem.CorpoHtml)
        };

        // O ACS não aceita nome de remetente por mensagem — o "De:" vem do nome
        // configurado no domínio do recurso. Por isso mensagem.RemetenteNome (o
        // remetente por disparo) não é aplicado aqui; ele só vale no SMTP.
        var email = new Acs.EmailMessage(
            senderAddress: _options.SenderAddress,
            content: conteudo,
            recipients: new Acs.EmailRecipients(
                [new Acs.EmailAddress(mensagem.ParaEmail, mensagem.ParaNome)]));

        try
        {
            await _client.SendAsync(WaitUntil.Started, email, cancellationToken);

            return EmailSendResult.Sucesso();
        }
        catch (RequestFailedException excecao) when (EhPermanente(excecao))
        {
            // 4xx que não seja throttling: remetente/domínio inválido, endereço
            // recusado. Repetir não muda o resultado.
            _logger.LogWarning(
                "Envio para {Email} recusado pelo ACS: {Status} {Motivo}",
                mensagem.ParaEmail,
                excecao.Status,
                excecao.Message);

            return EmailSendResult.FalhaPermanente(excecao.Message);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // Throttling (429), instabilidade (5xx) ou rede: merece nova tentativa.
            _logger.LogWarning(
                excecao,
                "Falha temporária ao enviar para {Email} pelo ACS",
                mensagem.ParaEmail);

            return EmailSendResult.FalhaTemporaria(excecao.Message);
        }
    }

    /// <summary>
    /// 4xx (fora 429) é erro de quem chamou: domínio não verificado, endereço
    /// inválido. 429 e 5xx são passageiros e merecem retry.
    /// </summary>
    private static bool EhPermanente(RequestFailedException excecao) =>
        excecao.Status is >= 400 and < 500 and not 429;

    private static string ParaTextoSimples(string html)
    {
        var semTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        var decodificado = System.Net.WebUtility.HtmlDecode(semTags);

        return System.Text.RegularExpressions.Regex
            .Replace(decodificado, @"\s+", " ")
            .Trim();
    }
}
