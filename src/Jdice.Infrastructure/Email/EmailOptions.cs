namespace Jdice.Infrastructure.Email;

/// <summary>
/// Escolhe por onde os e-mails saem. Em desenvolvimento é o Mailpit (SMTP de
/// captura); em produção, o Azure Communication Services. A troca é de
/// configuração, não de código — os dois implementam <c>IEmailSender</c>.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = ProvedorMailpit;

    public const string ProvedorMailpit = "Mailpit";
    public const string ProvedorAzure = "Azure";

    public bool UsaAzure =>
        string.Equals(Provider, ProvedorAzure, StringComparison.OrdinalIgnoreCase);
}
