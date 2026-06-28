using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMessageAttachmentsForLocalUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageAttachments_Documents_DocumentId",
                table: "MessageAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "MessageAttachments",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "MessageAttachments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                table: "MessageAttachments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "MessageAttachments",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "MessageAttachments",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "MessageAttachments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "MessageAttachments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "KnowledgeBase");

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "MessageAttachments",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE MessageAttachments
                SET ConversationId = (
                    SELECT ConversationId FROM Messages WHERE Messages.Id = MessageAttachments.MessageId
                );

                UPDATE MessageAttachments
                SET
                    SourceType = 'KnowledgeBase',
                    OriginalFileName = COALESCE((
                        SELECT FileName FROM Documents WHERE Documents.Id = MessageAttachments.DocumentId
                    ), ''),
                    StoredFileName = COALESCE((
                        SELECT FileName FROM Documents WHERE Documents.Id = MessageAttachments.DocumentId
                    ), ''),
                    FilePath = COALESCE((
                        SELECT RelativePath FROM Documents WHERE Documents.Id = MessageAttachments.DocumentId
                    ), ''),
                    Extension = COALESCE((
                        SELECT Extension FROM Documents WHERE Documents.Id = MessageAttachments.DocumentId
                    ), ''),
                    SizeBytes = COALESCE((
                        SELECT SizeBytes FROM Documents WHERE Documents.Id = MessageAttachments.DocumentId
                    ), 0)
                WHERE DocumentId IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                table: "MessageAttachments",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_ConversationId",
                table: "MessageAttachments",
                column: "ConversationId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageAttachments_Conversations_ConversationId",
                table: "MessageAttachments",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageAttachments_Documents_DocumentId",
                table: "MessageAttachments",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageAttachments_Conversations_ConversationId",
                table: "MessageAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageAttachments_Documents_DocumentId",
                table: "MessageAttachments");

            migrationBuilder.DropIndex(
                name: "IX_MessageAttachments_ConversationId",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "Extension",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "MessageAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "MessageAttachments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageAttachments_Documents_DocumentId",
                table: "MessageAttachments",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
