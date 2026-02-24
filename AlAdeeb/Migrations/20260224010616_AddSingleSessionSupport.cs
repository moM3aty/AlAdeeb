using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlAdeeb.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleSessionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentSessionId",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSessionId",
                table: "Users");
        }
    }
}
