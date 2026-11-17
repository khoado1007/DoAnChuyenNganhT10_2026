using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class SeparateServerAndAgentProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_local_profiles_UserId_AssetId",
                table: "local_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_local_profiles_UserId_AssetId_StorageBackend",
                table: "local_profiles",
                columns: new[] { "UserId", "AssetId", "StorageBackend" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_local_profiles_UserId_AssetId_StorageBackend",
                table: "local_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_local_profiles_UserId_AssetId",
                table: "local_profiles",
                columns: new[] { "UserId", "AssetId" },
                unique: true);
        }
    }
}
