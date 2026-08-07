using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(320) // limite do RFC 5321 para endereço de e-mail
            .IsRequired();

        // Índice único: a garantia de e-mail não duplicado é do banco, não da
        // checagem em memória — que perde numa corrida entre dois cadastros.
        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        // Guardado como texto: 'Admin'/'User' é legível numa consulta manual e
        // não quebra se alguém reordenar o enum.
        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.DeactivatedAt);

        // IsActive é derivado de DeactivatedAt e não tem coluna própria: duas
        // fontes para o mesmo fato acabariam divergindo.
        builder.Ignore(user => user.IsActive);
    }
}
