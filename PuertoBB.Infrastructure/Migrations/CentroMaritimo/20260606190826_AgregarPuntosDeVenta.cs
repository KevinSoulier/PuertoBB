using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CentroMaritimo
{
    /// <inheritdoc />
    public partial class AgregarPuntosDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfipCertificadoPassword",
                table: "Configuraciones");

            migrationBuilder.DropColumn(
                name: "AfipCertificadoRuta",
                table: "Configuraciones");

            migrationBuilder.DropColumn(
                name: "AfipUsarHomologacion",
                table: "Configuraciones");

            migrationBuilder.DropColumn(
                name: "PuntoDeVenta",
                table: "Configuraciones");

            migrationBuilder.CreateTable(
                name: "PuntosDeVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfiguracionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    UsarHomologacion = table.Column<bool>(type: "INTEGER", nullable: false),
                    CertificadoRuta = table.Column<string>(type: "TEXT", nullable: true),
                    CertificadoPassword = table.Column<string>(type: "TEXT", nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuntosDeVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuntosDeVenta_Configuraciones_ConfiguracionId",
                        column: x => x.ConfiguracionId,
                        principalTable: "Configuraciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PuntosDeVenta",
                columns: new[] { "Id", "Activo", "CertificadoPassword", "CertificadoRuta", "ConfiguracionId", "CreatedAt", "Nombre", "Numero", "UpdatedAt", "UsarHomologacion" },
                values: new object[] { 1, true, null, null, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Principal", 1, null, false });

            migrationBuilder.CreateIndex(
                name: "IX_PuntosDeVenta_ConfiguracionId",
                table: "PuntosDeVenta",
                column: "ConfiguracionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuntosDeVenta");

            migrationBuilder.AddColumn<string>(
                name: "AfipCertificadoPassword",
                table: "Configuraciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfipCertificadoRuta",
                table: "Configuraciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AfipUsarHomologacion",
                table: "Configuraciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PuntoDeVenta",
                table: "Configuraciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Configuraciones",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AfipCertificadoPassword", "AfipCertificadoRuta", "AfipUsarHomologacion", "PuntoDeVenta" },
                values: new object[] { null, null, false, 1 });
        }
    }
}
