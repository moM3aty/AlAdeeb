using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlAdeeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerToCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainerBio",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainerName",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainerBio",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TrainerName",
                table: "Courses");
        }
    }
}
