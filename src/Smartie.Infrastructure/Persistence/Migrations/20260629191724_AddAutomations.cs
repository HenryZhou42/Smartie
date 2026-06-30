using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Automations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    TriggerType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastRun = table.Column<long>(type: "INTEGER", nullable: true),
                    NextRun = table.Column<long>(type: "INTEGER", nullable: true),
                    RunCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Automations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRunLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRunLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRunLogs_Automations_AutomationRuleId",
                        column: x => x.AutomationRuleId,
                        principalTable: "Automations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_AutomationRuleId_StartedAt",
                table: "AutomationRunLogs",
                columns: new[] { "AutomationRuleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_UserId_Enabled",
                table: "Automations",
                columns: new[] { "UserId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_UserId_NextRun",
                table: "Automations",
                columns: new[] { "UserId", "NextRun" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRunLogs");

            migrationBuilder.DropTable(
                name: "Automations");
        }
    }
}
