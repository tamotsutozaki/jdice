using System.ComponentModel.DataAnnotations;

namespace Jdice.Infrastructure.Email;

/// <summary>
/// Configuração do Azure Communication Services — Email. Só é exigida quando
/// <see cref="EmailOptions.Provider"/> é "Azure"; em desenvolvimento fica vazia.
/// </summary>
public sealed class AcsOptions
{
    public const string SectionName = "Acs";

    /// <summary>
    /// Connection string do recurso, copiada do portal (Keys). Carrega o
    /// endpoint e a chave de acesso — é o segredo, não pode ir para o Git.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Remetente. Precisa ser um endereço de um domínio verificado no recurso
    /// (ex.: nao-responda@tomore.co). Enviar de um domínio não verificado é
    /// recusado pelo serviço.
    /// </summary>
    [Required, EmailAddress]
    public string SenderAddress { get; set; } = string.Empty;
}
