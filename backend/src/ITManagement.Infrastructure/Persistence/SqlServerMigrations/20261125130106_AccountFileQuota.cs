using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITManagement.Infrastructure.Persistence.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class AccountFileQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "profile_operations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileCountLimit",
                table: "app_users",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.Sql("""
                UPDATE u SET [FileCountLimit] = q.[Limit]
                FROM [app_users] u
                JOIN (SELECT [UserId], MIN([MaxFiles]) AS [Limit] FROM [local_profiles] GROUP BY [UserId]) q
                    ON u.[Id] = q.[UserId];
                UPDATE p SET [MaxFiles] = u.[FileCountLimit]
                FROM [local_profiles] p JOIN [app_users] u ON p.[UserId] = u.[Id]
                WHERE p.[MaxFiles] <> u.[FileCountLimit];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "profile_operations");

            migrationBuilder.DropColumn(
                name: "FileCountLimit",
                table: "app_users");
        }
    }
}
