using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jdice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAttemptLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Array JSON vazio: "" não é jsonb válido e o Postgres recusaria a
            // coluna nas linhas já existentes.
            migrationBuilder.AddColumn<string>(
                name: "AttemptLog",
                table: "deliveries",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptLog",
                table: "deliveries");
        }
    }
}
