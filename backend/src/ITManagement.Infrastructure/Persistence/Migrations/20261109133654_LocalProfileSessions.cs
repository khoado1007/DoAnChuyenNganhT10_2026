using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalProfileSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_access_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_access_grants_app_users_UserId",
                        column: x => x.UserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_access_grants_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "local_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxFiles = table.Column<int>(type: "integer", nullable: false),
                    UsedFiles = table.Column<int>(type: "integer", nullable: false),
                    Ready = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_profiles", x => x.Id);
                    table.CheckConstraint("CK_Profile_Counts", "\"MaxFiles\" >= 0 AND \"UsedFiles\" >= 0");
                    table.ForeignKey(
                        name: "FK_local_profiles_app_users_UserId",
                        column: x => x.UserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_local_profiles_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_sessions_local_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "local_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Folder = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Extension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_files_local_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "local_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CommandJson = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReservedFiles = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_operations_local_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "local_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_audit_events_CreatedAt",
                table: "app_audit_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_device_access_grants_AssetId",
                table: "device_access_grants",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_device_access_grants_UserId_AssetId",
                table: "device_access_grants",
                columns: new[] { "UserId", "AssetId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_AssetId",
                table: "device_sessions",
                column: "AssetId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_ProfileId",
                table: "device_sessions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_local_profiles_AssetId",
                table: "local_profiles",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_local_profiles_UserId_AssetId",
                table: "local_profiles",
                columns: new[] { "UserId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_files_ProfileId_Folder_Name",
                table: "profile_files",
                columns: new[] { "ProfileId", "Folder", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_operations_ProfileId",
                table: "profile_operations",
                column: "ProfileId",
                unique: true,
                filter: "\"CompletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_profile_operations_ProfileId_RequestId",
                table: "profile_operations",
                columns: new[] { "ProfileId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_audit_events");

            migrationBuilder.DropTable(
                name: "device_access_grants");

            migrationBuilder.DropTable(
                name: "device_sessions");

            migrationBuilder.DropTable(
                name: "profile_files");

            migrationBuilder.DropTable(
                name: "profile_operations");

            migrationBuilder.DropTable(
                name: "local_profiles");
        }
    }
}
