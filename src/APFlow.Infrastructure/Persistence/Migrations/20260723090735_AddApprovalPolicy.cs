using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresDualControl = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicies", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApprovalPolicies",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Domain", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "RequiredRole", "RequiresDualControl", "TenantId" },
                values: new object[] { new Guid("00000000-0000-0000-0004-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "InvoiceApproval", false, null, null, "FINANCE_MANAGER", false, new Guid("00000000-0000-0000-0000-0000000b5121") });

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FromStatusCode", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "TenantId", "ToStatusCode", "WorkflowTemplateId" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CHECKED_READY_TO_APPROVE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "APPROVED", new Guid("00000000-0000-0000-0001-000000000002") });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_Domain_TenantId",
                table: "ApprovalPolicies",
                columns: new[] { "Domain", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalPolicies");

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"));
        }
    }
}
