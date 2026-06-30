using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EmbeddedAt",
                table: "Documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddedChunkCount",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "Documents",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmbedded",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "EmbeddingGeneratedAt",
                table: "DocumentChunks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "DocumentChunks",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingStatus",
                table: "DocumentChunks",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<byte[]>(
                name: "EmbeddingVector",
                table: "DocumentChunks",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddedChunkCount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsEmbedded",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingGeneratedAt",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "DocumentChunks");
        }
    }
}
