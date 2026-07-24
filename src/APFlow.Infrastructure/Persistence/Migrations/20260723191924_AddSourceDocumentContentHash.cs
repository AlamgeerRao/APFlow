using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceDocumentContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceDocumentContentHash",
                table: "Invoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_SourceDocumentContentHash",
                table: "Invoices",
                columns: new[] { "TenantId", "SourceDocumentContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_SourceDocumentContentHash",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceDocumentContentHash",
                table: "Invoices");
        }
    }
}
