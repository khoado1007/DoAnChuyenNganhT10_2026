using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentEnrollmentHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_enrollment_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_enrollment_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_enrollment_tokens_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    Hostname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Os = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cpu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RamBytes = table.Column<long>(type: "bigint", nullable: false),
                    AgentVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_enrollment_tokens_AssetId",
                table: "agent_enrollment_tokens",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_enrollment_tokens_TokenHash",
                table: "agent_enrollment_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_AssetId",
                table: "agents",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_CredentialHash",
                table: "agents",
                column: "CredentialHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_InstanceId",
                table: "agents",
                column: "InstanceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_enrollment_tokens");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
