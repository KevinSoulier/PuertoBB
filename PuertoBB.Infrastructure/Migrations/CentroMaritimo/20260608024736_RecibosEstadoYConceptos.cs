using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CentroMaritimo
{
    /// <inheritdoc />
    public partial class RecibosEstadoYConceptos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEnvioMail",
                table: "Recibos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoErrorCae",
                table: "Recibos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoErrorMail",
                table: "Recibos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConceptosRecibo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptosRecibo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosRecibo_Nombre",
                table: "ConceptosRecibo",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConceptosRecibo");

            migrationBuilder.DropColumn(
                name: "FechaEnvioMail",
                table: "Recibos");

            migrationBuilder.DropColumn(
                name: "UltimoErrorCae",
                table: "Recibos");

            migrationBuilder.DropColumn(
                name: "UltimoErrorMail",
                table: "Recibos");
        }
    }
}
