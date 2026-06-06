using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CentroMaritimo
{
    /// <inheritdoc />
    public partial class AgregarImportePredeterminado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ImporteVoucherPredeterminado",
                table: "Configuraciones",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImporteVoucherPredeterminado",
                table: "Configuraciones");
        }
    }
}
