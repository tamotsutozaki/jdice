using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jdice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDesativacaoDeConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "users");
        }
    }
}
