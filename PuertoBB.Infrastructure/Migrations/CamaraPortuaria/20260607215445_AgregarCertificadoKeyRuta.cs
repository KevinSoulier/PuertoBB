using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CamaraPortuaria
{
    /// <inheritdoc />
    public partial class AgregarCertificadoKeyRuta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificadoKeyRuta",
                table: "PuntosDeVenta",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PuntosDeVenta",
                keyColumn: "Id",
                keyValue: 1,
                column: "CertificadoKeyRuta",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificadoKeyRuta",
                table: "PuntosDeVenta");
        }
    }
}
