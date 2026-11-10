using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetAssignmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetAssignments_app_users_UserId",
                table: "AssetAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetAssignments_assets_AssetId",
                table: "AssetAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AssetAssignments_AssetId",
                table: "AssetAssignments");

            migrationBuilder.AddColumn<string>(
                name: "CloseNotes",
                table: "AssetAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedBy",
                table: "AssetAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetAssignments_AssetId",
                table: "AssetAssignments",
                column: "AssetId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAssignments_app_users_UserId",
                table: "AssetAssignments",
                column: "UserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAssignments_assets_AssetId",
                table: "AssetAssignments",
                column: "AssetId",
                principalTable: "assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetAssignments_app_users_UserId",
                table: "AssetAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetAssignments_assets_AssetId",
                table: "AssetAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AssetAssignments_AssetId",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "CloseNotes",
                table: "AssetAssignments");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "AssetAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAssignments_AssetId",
                table: "AssetAssignments",
                column: "AssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAssignments_app_users_UserId",
                table: "AssetAssignments",
                column: "UserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAssignments_assets_AssetId",
                table: "AssetAssignments",
                column: "AssetId",
                principalTable: "assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
