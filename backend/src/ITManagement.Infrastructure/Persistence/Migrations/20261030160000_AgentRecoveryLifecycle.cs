using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentRecoveryLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_AssetId",
                table: "agents");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "agents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                table: "agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_AssetId",
                table: "agents",
                column: "AssetId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_AssetId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                table: "agents");

            migrationBuilder.CreateIndex(
                name: "IX_agents_AssetId",
                table: "agents",
                column: "AssetId",
                unique: true);
        }
    }
}
