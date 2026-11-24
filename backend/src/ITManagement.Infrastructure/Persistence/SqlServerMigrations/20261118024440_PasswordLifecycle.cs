using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class PasswordLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PasswordExpiredLock",
                table: "app_users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordFirstLoginAt",
                table: "app_users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordReminderDay",
                table: "app_users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPasswordChange",
                table: "app_users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordExpiredLock",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "PasswordFirstLoginAt",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "PasswordReminderDay",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "RequiresPasswordChange",
                table: "app_users");
        }
    }
}
