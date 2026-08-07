using Jdice.Domain.Recipients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jdice.Infrastructure.Persistence.Configurations;

public sealed class RecipientListConfiguration : IEntityTypeConfiguration<RecipientList>
{
    public void Configure(EntityTypeBuilder<RecipientList> builder)
    {
        builder.ToTable("recipient_lists");

        builder.HasKey(list => list.Id);
        builder.Property(list => list.Id).ValueGeneratedNever();

        builder.Property(list => list.Name)
            .HasMaxLength(RecipientList.MaximumNameLength)
            .IsRequired();

        builder.Property(list => list.Description)
            .HasMaxLength(RecipientList.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(list => list.CreatedBy).IsRequired();
        builder.Property(list => list.CreatedAt).IsRequired();
        builder.Property(list => list.UpdatedAt).IsRequired();

        builder.HasIndex(list => list.Name);

        builder.HasMany(list => list.Members)
            .WithOne()
            .HasForeignKey(member => member.RecipientListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(RecipientList.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RecipientListMemberConfiguration : IEntityTypeConfiguration<RecipientListMember>
{
    public void Configure(EntityTypeBuilder<RecipientListMember> builder)
    {
        builder.ToTable("recipient_list_members");

        // Chave composta: a mesma pessoa não entra duas vezes na mesma lista,
        // e quem garante isso é o banco.
        builder.HasKey(member => new { member.RecipientListId, member.RecipientId });

        builder.Property(member => member.AddedAt).IsRequired();

        builder.HasOne<Recipient>()
            .WithMany()
            .HasForeignKey(member => member.RecipientId)
            // Remover um destinatário tira ele das listas junto; o contrário
            // seria deixar referência apontando para o vazio.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(member => member.RecipientId);
    }
}
