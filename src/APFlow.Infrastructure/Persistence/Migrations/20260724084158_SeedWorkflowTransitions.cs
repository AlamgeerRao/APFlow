using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace APFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedWorkflowTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"));

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FromStatusCode", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "TenantId", "ToStatusCode", "WorkflowTemplateId" },
                values: new object[,]
                {
                    { new Guid("042ac4e7-fcda-cddb-552a-99bc87a71902"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("08e0db6e-4319-39c5-28bb-0ea9b75c5c50"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CHECKED_READY_TO_APPROVE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "NEEDS_QUERY", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("0d1d271f-6d15-a0fa-1357-466070d826ca"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_QUERY", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "QUERY_RAISED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("1532ebbf-a773-fb1c-504e-817b000a9539"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "EXTRACTED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("199a4579-f20e-4fbd-98e5-4512088d0b9d"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PAID", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("1a3403f2-5bc1-996b-f048-4840d46f5000"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "NEEDS_QUERY", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("2ac61aee-4df1-1fdb-92cf-f2bc4ffe4733"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("3b2c910f-d7e5-c2aa-5bf7-147f17634ba0"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CHECKED_READY_TO_APPROVE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "NEEDS_REVIEW_FEBINA", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("3f4244f3-9cd6-920c-a649-cfe1b5c4a335"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "REJECTED", false, null, null, null, "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("3fb39133-3218-fa2c-31da-1ab9761650f5"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "NEEDS_REVIEW_FEBINA", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("3fb94c0a-9718-a9ed-4e26-59419469bb52"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "REJECTED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("44f453de-efee-a70a-92f1-c2a3b230e086"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CANCELLED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "RECEIVED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("46cf4fd5-1626-fa56-a496-311c135707db"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("5511c330-96de-3c94-6ef3-a908dc0b4c51"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "EXTRACTED", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("5ce4259d-0754-6688-c193-7b0f39355737"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "RECEIVED", false, null, null, null, "PROCESSING", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("5f68b404-babc-4199-172d-9c223e7f02f4"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PAID", false, null, null, null, "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("607e581d-99a1-047e-f71b-f0daed12c28e"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("655395ba-a8a4-803f-46d9-b39a332d4795"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "RECEIVED", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("6e5951d2-5abf-a622-7da5-0b9fd25abaa2"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "REJECTED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("79252cb8-2a0f-13d7-e5ac-81c472d95548"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CANCELLED", false, null, null, null, "RECEIVED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("7a82b7fa-4176-972c-7d28-c230f195eb39"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, null, "REJECTED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("803e1c56-8854-51ef-2bf1-f08206e22f9a"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, null, "APPROVED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("829fe0dc-78e6-a14b-98b2-7ac5e98e9815"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_QUERY", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("8da8abb5-0f89-ff8b-be65-de073832b7ae"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "RECEIVED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("95d1b36b-aa59-c961-901d-eaadaa72a238"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "REJECTED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("9de6f32b-80ac-4eac-03bd-a055c0ae6ba4"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_QUERY", false, null, null, null, "QUERY_RAISED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("9e7caae8-924a-4984-5586-3098236f53c3"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CHECKED_READY_TO_APPROVE", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("9f7faa8d-f632-9818-7579-04343ab17db1"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PROCESSING", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("a049a50b-a162-61b1-159a-53b893fe62c4"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "REJECTED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("a19dd505-74e0-c453-5c41-74077fcfb544"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "READY_FOR_PAYMENT", false, null, null, null, "PAID", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("a52db712-cb7c-cb8e-2996-eaf31c552a81"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, null, "REJECTED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("ab275607-0a90-c081-a018-5a7bee48f4da"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CHECKED_READY_TO_APPROVE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "APPROVED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("ad3f0932-9a7f-da45-76d4-b0e504b1ef9e"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, null, "NEEDS_QUERY", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("aec013cd-abf8-f59c-be87-f2a491057379"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CANCELLED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("bb685725-37d3-57f2-1386-98a008bfc684"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "REJECTED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("bc4941c8-e905-609b-e885-f108c0e3458e"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_QUERY", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("bd66a1c6-5e50-c1e8-5503-364419ad922b"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PROCESSING", false, null, null, null, "EXTRACTED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("c2599b00-4f30-3fa0-791a-164a3696c10d"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "AWAITING_SUPPLIER_RESPONSE", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("c5ad3ccb-c890-3f5b-cd6d-b5214ed6806b"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "EXTRACTED", false, null, null, null, "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("c60db0b2-c8be-822d-0a0e-f316986b67c8"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PROCESSING", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "EXTRACTED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("cc00cbf0-6a62-a877-4d21-d1f9e812c4f5"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_REVIEW_FEBINA", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CHECKED_READY_TO_APPROVE", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("d3d1747c-2195-7b52-dfdf-a8efe59fabac"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_SUPPLIER_RESPONSE", false, null, null, null, "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("d51c1405-9c22-32ba-0342-38212d2ba841"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("d642c5f1-0585-e62b-de0a-cc7ebeb434ad"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "APPROVED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "READY_FOR_PAYMENT", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("d86a825f-602e-aabc-6c7b-ce21737da5dd"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "PROCESSING", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("dcba36e4-7a55-049a-a432-36c3aa776174"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_REVIEW_FEBINA", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "NEEDS_QUERY", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("de760142-342c-cc95-eeb8-f70e1a6e34de"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "REJECTED", false, null, null, null, "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("e25855ae-a0fc-1fa3-e76c-04034c18926d"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "NEEDS_REVIEW_FEBINA", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "REJECTED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("e827c842-823f-fcaf-6b58-82e5be2ca330"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "EXTRACTED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "AWAITING_REVIEW", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("ebfa2b8a-3f2c-7d93-279b-c00baeb46aac"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "CANCELLED", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("ed2a8e1a-c07a-83bd-3fc9-08577e177bb6"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, null, "AWAITING_SUPPLIER_RESPONSE", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("ed304a4b-a5e7-4d51-0a2c-bb08bc5f66b7"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "RECEIVED", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "PROCESSING", new Guid("00000000-0000-0000-0001-000000000002") },
                    { new Guid("ed45b9a4-88f0-a507-5a14-d5991d8650c2"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "AWAITING_REVIEW", false, null, null, null, "CANCELLED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("f1451bb6-00ec-32f7-97a5-a8b78a41f75d"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CANCELLED", false, null, null, null, "ARCHIVED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("f3e1eac1-9a5d-8181-6ee3-e9416c36782a"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "APPROVED", false, null, null, null, "READY_FOR_PAYMENT", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("f54eb22e-3be5-1d58-aed5-d58425674d97"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "QUERY_RAISED", false, null, null, null, "REJECTED", new Guid("00000000-0000-0000-0001-000000000001") },
                    { new Guid("fbd5c2ce-2041-d03f-235c-55231486b1e9"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "READY_FOR_PAYMENT", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "PAID", new Guid("00000000-0000-0000-0001-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("042ac4e7-fcda-cddb-552a-99bc87a71902"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("08e0db6e-4319-39c5-28bb-0ea9b75c5c50"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("0d1d271f-6d15-a0fa-1357-466070d826ca"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1532ebbf-a773-fb1c-504e-817b000a9539"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("199a4579-f20e-4fbd-98e5-4512088d0b9d"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1a3403f2-5bc1-996b-f048-4840d46f5000"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("2ac61aee-4df1-1fdb-92cf-f2bc4ffe4733"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3b2c910f-d7e5-c2aa-5bf7-147f17634ba0"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3f4244f3-9cd6-920c-a649-cfe1b5c4a335"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb39133-3218-fa2c-31da-1ab9761650f5"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb94c0a-9718-a9ed-4e26-59419469bb52"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("44f453de-efee-a70a-92f1-c2a3b230e086"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("46cf4fd5-1626-fa56-a496-311c135707db"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("5511c330-96de-3c94-6ef3-a908dc0b4c51"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("5ce4259d-0754-6688-c193-7b0f39355737"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("5f68b404-babc-4199-172d-9c223e7f02f4"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("607e581d-99a1-047e-f71b-f0daed12c28e"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("655395ba-a8a4-803f-46d9-b39a332d4795"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("6e5951d2-5abf-a622-7da5-0b9fd25abaa2"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("79252cb8-2a0f-13d7-e5ac-81c472d95548"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("7a82b7fa-4176-972c-7d28-c230f195eb39"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("803e1c56-8854-51ef-2bf1-f08206e22f9a"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("829fe0dc-78e6-a14b-98b2-7ac5e98e9815"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("8da8abb5-0f89-ff8b-be65-de073832b7ae"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("95d1b36b-aa59-c961-901d-eaadaa72a238"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9de6f32b-80ac-4eac-03bd-a055c0ae6ba4"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9e7caae8-924a-4984-5586-3098236f53c3"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9f7faa8d-f632-9818-7579-04343ab17db1"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a049a50b-a162-61b1-159a-53b893fe62c4"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a19dd505-74e0-c453-5c41-74077fcfb544"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a52db712-cb7c-cb8e-2996-eaf31c552a81"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ab275607-0a90-c081-a018-5a7bee48f4da"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ad3f0932-9a7f-da45-76d4-b0e504b1ef9e"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("aec013cd-abf8-f59c-be87-f2a491057379"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bb685725-37d3-57f2-1386-98a008bfc684"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bc4941c8-e905-609b-e885-f108c0e3458e"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bd66a1c6-5e50-c1e8-5503-364419ad922b"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c2599b00-4f30-3fa0-791a-164a3696c10d"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c5ad3ccb-c890-3f5b-cd6d-b5214ed6806b"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c60db0b2-c8be-822d-0a0e-f316986b67c8"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("cc00cbf0-6a62-a877-4d21-d1f9e812c4f5"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d3d1747c-2195-7b52-dfdf-a8efe59fabac"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d51c1405-9c22-32ba-0342-38212d2ba841"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d642c5f1-0585-e62b-de0a-cc7ebeb434ad"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d86a825f-602e-aabc-6c7b-ce21737da5dd"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("dcba36e4-7a55-049a-a432-36c3aa776174"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("de760142-342c-cc95-eeb8-f70e1a6e34de"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e25855ae-a0fc-1fa3-e76c-04034c18926d"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e827c842-823f-fcaf-6b58-82e5be2ca330"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ebfa2b8a-3f2c-7d93-279b-c00baeb46aac"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ed2a8e1a-c07a-83bd-3fc9-08577e177bb6"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ed304a4b-a5e7-4d51-0a2c-bb08bc5f66b7"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ed45b9a4-88f0-a507-5a14-d5991d8650c2"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("f1451bb6-00ec-32f7-97a5-a8b78a41f75d"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("f3e1eac1-9a5d-8181-6ee3-e9416c36782a"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("f54eb22e-3be5-1d58-aed5-d58425674d97"));

            migrationBuilder.DeleteData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("fbd5c2ce-2041-d03f-235c-55231486b1e9"));

            migrationBuilder.InsertData(
                table: "WorkflowTransitions",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FromStatusCode", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "TenantId", "ToStatusCode", "WorkflowTemplateId" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "CHECKED_READY_TO_APPROVE", false, null, null, new Guid("00000000-0000-0000-0000-0000000b5121"), "APPROVED", new Guid("00000000-0000-0000-0001-000000000002") });
        }
    }
}
