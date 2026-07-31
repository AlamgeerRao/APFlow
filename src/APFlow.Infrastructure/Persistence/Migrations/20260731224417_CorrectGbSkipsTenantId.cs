using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrectGbSkipsTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApprovalPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000002"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000003"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000005"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000006"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000007"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000008"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000009"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000a"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000b"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000c"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000d"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000e"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000f"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("042ac4e7-fcda-cddb-552a-99bc87a71902"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("08e0db6e-4319-39c5-28bb-0ea9b75c5c50"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("0d1d271f-6d15-a0fa-1357-466070d826ca"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1532ebbf-a773-fb1c-504e-817b000a9539"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("199a4579-f20e-4fbd-98e5-4512088d0b9d"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1a3403f2-5bc1-996b-f048-4840d46f5000"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("2ac61aee-4df1-1fdb-92cf-f2bc4ffe4733"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3b2c910f-d7e5-c2aa-5bf7-147f17634ba0"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb39133-3218-fa2c-31da-1ab9761650f5"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb94c0a-9718-a9ed-4e26-59419469bb52"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("44f453de-efee-a70a-92f1-c2a3b230e086"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("46cf4fd5-1626-fa56-a496-311c135707db"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("6e5951d2-5abf-a622-7da5-0b9fd25abaa2"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("8da8abb5-0f89-ff8b-be65-de073832b7ae"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("95d1b36b-aa59-c961-901d-eaadaa72a238"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9e7caae8-924a-4984-5586-3098236f53c3"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a049a50b-a162-61b1-159a-53b893fe62c4"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ab275607-0a90-c081-a018-5a7bee48f4da"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("aec013cd-abf8-f59c-be87-f2a491057379"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bb685725-37d3-57f2-1386-98a008bfc684"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bc4941c8-e905-609b-e885-f108c0e3458e"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c2599b00-4f30-3fa0-791a-164a3696c10d"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c60db0b2-c8be-822d-0a0e-f316986b67c8"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("cc00cbf0-6a62-a877-4d21-d1f9e812c4f5"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d642c5f1-0585-e62b-de0a-cc7ebeb434ad"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d86a825f-602e-aabc-6c7b-ce21737da5dd"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("dcba36e4-7a55-049a-a432-36c3aa776174"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e25855ae-a0fc-1fa3-e76c-04034c18926d"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e827c842-823f-fcaf-6b58-82e5be2ca330"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ebfa2b8a-3f2c-7d93-279b-c00baeb46aac"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ed304a4b-a5e7-4d51-0a2c-bb08bc5f66b7"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("fbd5c2ce-2041-d03f-235c-55231486b1e9"),
                column: "TenantId",
                value: new Guid("641fc267-7902-48d0-8e1c-1d3d0166c8ac"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApprovalPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000002"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000003"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000005"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000006"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000007"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000008"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000009"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000a"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000b"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000c"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000d"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000e"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-00000000000f"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "StatusReferences",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("042ac4e7-fcda-cddb-552a-99bc87a71902"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("08e0db6e-4319-39c5-28bb-0ea9b75c5c50"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("0d1d271f-6d15-a0fa-1357-466070d826ca"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1532ebbf-a773-fb1c-504e-817b000a9539"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("199a4579-f20e-4fbd-98e5-4512088d0b9d"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("1a3403f2-5bc1-996b-f048-4840d46f5000"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("2ac61aee-4df1-1fdb-92cf-f2bc4ffe4733"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3b2c910f-d7e5-c2aa-5bf7-147f17634ba0"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb39133-3218-fa2c-31da-1ab9761650f5"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("3fb94c0a-9718-a9ed-4e26-59419469bb52"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("44f453de-efee-a70a-92f1-c2a3b230e086"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("46cf4fd5-1626-fa56-a496-311c135707db"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("6e5951d2-5abf-a622-7da5-0b9fd25abaa2"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("8da8abb5-0f89-ff8b-be65-de073832b7ae"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("95d1b36b-aa59-c961-901d-eaadaa72a238"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("9e7caae8-924a-4984-5586-3098236f53c3"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("a049a50b-a162-61b1-159a-53b893fe62c4"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ab275607-0a90-c081-a018-5a7bee48f4da"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("aec013cd-abf8-f59c-be87-f2a491057379"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bb685725-37d3-57f2-1386-98a008bfc684"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("bc4941c8-e905-609b-e885-f108c0e3458e"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c2599b00-4f30-3fa0-791a-164a3696c10d"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("c60db0b2-c8be-822d-0a0e-f316986b67c8"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("cc00cbf0-6a62-a877-4d21-d1f9e812c4f5"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d642c5f1-0585-e62b-de0a-cc7ebeb434ad"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("d86a825f-602e-aabc-6c7b-ce21737da5dd"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("dcba36e4-7a55-049a-a432-36c3aa776174"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e25855ae-a0fc-1fa3-e76c-04034c18926d"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("e827c842-823f-fcaf-6b58-82e5be2ca330"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ebfa2b8a-3f2c-7d93-279b-c00baeb46aac"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("ed304a4b-a5e7-4d51-0a2c-bb08bc5f66b7"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));

            migrationBuilder.UpdateData(
                table: "WorkflowTransitions",
                keyColumn: "Id",
                keyValue: new Guid("fbd5c2ce-2041-d03f-235c-55231486b1e9"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-0000000b5121"));
        }
    }
}
