using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class EmployeeTabDeparture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PageSequence",
                table: "device_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "TabClosesAt",
                table: "device_sessions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageSequence",
                table: "device_sessions");

            migrationBuilder.DropColumn(
                name: "TabClosesAt",
                table: "device_sessions");
        }
    }
}
