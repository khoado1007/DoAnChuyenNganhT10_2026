using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class ServerSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "profile_files",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageBackend",
                table: "local_profiles",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Agent");

            migrationBuilder.CreateTable(
                name: "machine_specifications",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Processor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MemoryGb = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    DiskTotalGb = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    DiskFreeGb = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    DiskEncryption = table.Column<bool>(type: "bit", nullable: true),
                    CollectorVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrivateIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    PublicIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    LastRestartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoftwareJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_specifications", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_machine_specifications_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_specifications");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "profile_files");

            migrationBuilder.DropColumn(
                name: "StorageBackend",
                table: "local_profiles");
        }
    }
}
