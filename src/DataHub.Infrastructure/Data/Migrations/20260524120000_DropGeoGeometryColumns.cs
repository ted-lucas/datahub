using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataHub.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Drops the SQL Server <c>geography</c> columns from Countries / States /
    /// Counties. Boundaries are now served as static GeoJSON assets under
    /// <c>/geo/*</c> and joined to these reference rows by FIPS / ISO-2 on the
    /// frontend, so the columns are no longer needed.
    /// </summary>
    public partial class DropGeoGeometryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Geometry",
                table: "Counties");

            migrationBuilder.DropColumn(
                name: "Geometry",
                table: "States");

            migrationBuilder.DropColumn(
                name: "Geometry",
                table: "Countries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NetTopologySuite.Geometries.Geometry>(
                name: "Geometry",
                table: "Counties",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<NetTopologySuite.Geometries.Geometry>(
                name: "Geometry",
                table: "States",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<NetTopologySuite.Geometries.Geometry>(
                name: "Geometry",
                table: "Countries",
                type: "geography",
                nullable: true);
        }
    }
}
