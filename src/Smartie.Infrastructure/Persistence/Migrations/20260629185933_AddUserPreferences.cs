using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Default"),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Purple"),
                    CustomAccentHex = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Density = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Default"),
                    FontSize = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Medium"),
                    SidebarMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Expanded"),
                    AnimationMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Enabled"),
                    TransparencyEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    WindowEffect = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Disabled"),
                    TypingSpeedMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 20),
                    TransitionSpeedMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 200),
                    BubbleRadius = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Medium"),
                    BubbleWidth = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Standard"),
                    MessageSpacing = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Normal"),
                    CodeBlockTheme = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Default"),
                    MarkdownTheme = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Default"),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
