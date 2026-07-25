using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace APFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateSuspectedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"));

            migrationBuilder.DeleteData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000004"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StatusReferences",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "IsDeleted", "IsTerminal", "ModifiedAtUtc", "ModifiedBy", "Name", "SortOrder", "TenantId", "WorkflowTemplateId" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000004"), "DUPLICATE_SUSPECTED", new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, false, false, null, null, "Duplicate Suspected", 30, null, new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("00000000-0000-0000-0003-000000000004"), "DUPLICATE_SUSPECTED", new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, false, false, null, null, "Duplicate Suspected", 30, new Guid("00000000-0000-0000-0000-0000000b5121"), new Guid("00000000-0000-0000-0001-000000000002") }
                });
        }
    }
}
