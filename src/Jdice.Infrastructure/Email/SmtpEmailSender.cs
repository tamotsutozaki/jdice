using Jdice.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Jdice.Infrastructure.Email;

/// <summary>
/// Envio por SMTP com MailKit. O <c>SmtpClient</c> do .NET é oficialmente
/// obsoleto e não trata bem autenticação moderna.
/// <para>
/// A distinção entre falha temporária e permanente importa: repetir um envio
/// para endereço inexistente não muda o resultado, gasta recurso e prejudica a
/// reputação do remetente. Já um timeout merece nova tentativa.
/// </para>
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task<EmailSendResult> SendAsync(
        EmailMessage mensagem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var mime = Montar(mensagem);
            using var cliente = new SmtpClient
            {
                Timeout = _options.TimeoutSeconds * 1000
            };

            await cliente.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await cliente.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await cliente.SendAsync(mime, cancellationToken);
            await cliente.DisconnectAsync(quit: true, cancellationToken);

            return EmailSendResult.Sucesso();
        }
        catch (SmtpCommandException excecao) when (EhPermanente(excecao))
        {
            logger.LogWarning(
                "Envio para {Email} recusado definitivamente: {Motivo}",
                mensagem.ParaEmail,
                excecao.Message);

            return EmailSendResult.FalhaPermanente(excecao.Message);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            logger.LogWarning(
                excecao,
                "Falha temporária ao enviar para {Email}",
                mensagem.ParaEmail);

            return EmailSendResult.FalhaTemporaria(excecao.Message);
        }
    }

    private MimeMessage Montar(EmailMessage mensagem)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(mensagem.ParaNome, mensagem.ParaEmail));
        mime.Subject = mensagem.Assunto;

        mime.Body = new BodyBuilder
        {
            HtmlBody = mensagem.CorpoHtml,

            // Alguns clientes não mostram HTML; sem alternativa em texto, a
            // mensagem chega em branco.
            TextBody = ParaTextoSimples(mensagem.CorpoHtml)
        }.ToMessageBody();

        return mime;
    }

    /// <summary>
    /// Caixa postal inexistente e endereço recusado não melhoram com nova
    /// tentativa; o código 5xx do SMTP significa falha definitiva.
    /// </summary>
    private static bool EhPermanente(SmtpCommandException excecao) =>
        excecao.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted
        || (int)excecao.StatusCode >= 500;

    /// <summary>
    /// Versão em texto do corpo. Simples de propósito: converter HTML em texto
    /// com fidelidade é problema à parte, e o que importa é a mensagem não
    /// chegar vazia.
    /// </summary>
    private static string ParaTextoSimples(string html)
    {
        var semTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        var decodificado = System.Net.WebUtility.HtmlDecode(semTags);

        return System.Text.RegularExpressions.Regex
            .Replace(decodificado, @"\s+", " ")
            .Trim();
    }
}
