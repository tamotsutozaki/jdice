using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jdice.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vazia de propósito. Declarar que os identificadores são gerados pelo
    /// domínio (UUIDv7 derivado da data de criação) e não pelo banco muda
    /// apenas como o EF interpreta o modelo — nenhuma coluna, índice ou
    /// restrição é afetada.
    /// <para>
    /// A declaração importa: sem ela, o EF assume a convenção de chave gerada
    /// por ele e, ao encontrar uma entidade nova já com Id preenchido, conclui
    /// que ela existe — emitindo UPDATE no lugar de INSERT.
    /// </para>
    /// </summary>
    public partial class DeclararIdentificadoresGeradosPeloDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
