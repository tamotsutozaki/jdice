using System.ComponentModel.DataAnnotations;

namespace Jdice.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Em desenvolvimento o servidor de captura não usa TLS. Fora daí, deve
    /// estar ligado — senão as credenciais trafegam em texto claro.
    /// </summary>
    public bool UseStartTls { get; set; }

    [Required, EmailAddress]
    public string FromEmail { get; set; } = "nao-responda@jdice.local";

    public string FromName { get; set; } = "JDice";

    /// <summary>Segundos até desistir de uma conexão pendurada.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
