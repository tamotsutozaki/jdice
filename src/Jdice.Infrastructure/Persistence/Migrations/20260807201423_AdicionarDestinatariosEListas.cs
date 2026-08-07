using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jdice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDestinatariosEListas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recipient_lists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipient_lists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fields = table.Column<string>(type: "jsonb", nullable: false),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recipient_list_members",
                columns: table => new
                {
                    RecipientListId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipient_list_members", x => new { x.RecipientListId, x.RecipientId });
                    table.ForeignKey(
                        name: "FK_recipient_list_members_recipient_lists_RecipientListId",
                        column: x => x.RecipientListId,
                        principalTable: "recipient_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipient_list_members_recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipient_list_members_RecipientId",
                table: "recipient_list_members",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_recipient_lists_Name",
                table: "recipient_lists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_recipients_Email",
                table: "recipients",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipient_list_members");

            migrationBuilder.DropTable(
                name: "recipient_lists");

            migrationBuilder.DropTable(
                name: "recipients");
        }
    }
}
