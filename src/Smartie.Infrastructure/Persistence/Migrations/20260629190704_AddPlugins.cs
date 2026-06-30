using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlugins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PluginInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FolderName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntryAssembly = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IconRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsLoaded = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LoadError = table.Column<string>(type: "TEXT", nullable: true),
                    LastLoadDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    InstalledAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginInstallations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginInstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginLogEntries_PluginInstallations_PluginInstallationId",
                        column: x => x.PluginInstallationId,
                        principalTable: "PluginInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PluginInstallations_UserId_PluginKey",
                table: "PluginInstallations",
                columns: new[] { "UserId", "PluginKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginLogEntries_PluginInstallationId_CreatedAt",
                table: "PluginLogEntries",
                columns: new[] { "PluginInstallationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PluginLogEntries");

            migrationBuilder.DropTable(
                name: "PluginInstallations");
        }
    }
}
