using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamClosedYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClosedYear",
                table: "Teams",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedYear",
                table: "Teams");
        }
    }
}
