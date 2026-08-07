namespace Jdice.Domain.Users;

public enum UserRole
{
    /// <summary>Usa o sistema: cria modelos, cadastra destinatários, dispara e agenda.</summary>
    User = 0,

    /// <summary>Tudo que o User faz, mais a gestão de contas.</summary>
    Admin = 1
}
