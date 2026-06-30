using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecentCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsed = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecentCommands_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecentCommands_UserId_CommandName",
                table: "RecentCommands",
                columns: new[] { "UserId", "CommandName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecentCommands_UserId_LastUsed",
                table: "RecentCommands",
                columns: new[] { "UserId", "LastUsed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecentCommands");
        }
    }
}
