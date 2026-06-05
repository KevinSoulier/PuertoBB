using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CamaraPortuaria
{
    /// <inheritdoc />
    public partial class InicialCamara : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RazonSocial = table.Column<string>(type: "TEXT", nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", nullable: false),
                    PuntoDeVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    CodigoAfipRecibo = table.Column<int>(type: "INTEGER", nullable: false),
                    CodigoAfipNotaDeCredito = table.Column<int>(type: "INTEGER", nullable: false),
                    AfipCertificadoRuta = table.Column<string>(type: "TEXT", nullable: true),
                    AfipCertificadoPassword = table.Column<string>(type: "TEXT", nullable: true),
                    AfipUsarHomologacion = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiasVencimiento = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUsuario = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    EmailRemitente = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuraciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RazonSocial = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    Domicilio = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailsEmpresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailsEmpresa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailsEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmpresasGrupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresasGrupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpresasGrupos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmpresasGrupos_Grupos_GrupoFacturacionId",
                        column: x => x.GrupoFacturacionId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recibos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PuntoDeVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComprobante = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodigoAfip = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "INTEGER", nullable: false),
                    CAE = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaVencimientoCAE = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaVencimientoPago = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recibos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recibos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recibos_Grupos_GrupoFacturacionId",
                        column: x => x.GrupoFacturacionId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotasDeCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReciboOriginalId = table.Column<int>(type: "INTEGER", nullable: false),
                    PuntoDeVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComprobante = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodigoAfip = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "INTEGER", nullable: false),
                    CAE = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaVencimientoCAE = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasDeCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasDeCredito_Recibos_ReciboOriginalId",
                        column: x => x.ReciboOriginalId,
                        principalTable: "Recibos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Configuraciones",
                columns: new[] { "Id", "AfipCertificadoPassword", "AfipCertificadoRuta", "AfipUsarHomologacion", "CodigoAfipNotaDeCredito", "CodigoAfipRecibo", "CreatedAt", "Cuit", "DiasVencimiento", "EmailRemitente", "PuntoDeVenta", "RazonSocial", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpUsuario", "UpdatedAt" },
                values: new object[] { 1, null, null, false, 13, 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", 30, null, 1, "", null, null, 587, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_EmailsEmpresa_EmpresaId",
                table: "EmailsEmpresa",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Cuit",
                table: "Empresas",
                column: "Cuit");

            migrationBuilder.CreateIndex(
                name: "IX_EmpresasGrupos_EmpresaId_GrupoFacturacionId",
                table: "EmpresasGrupos",
                columns: new[] { "EmpresaId", "GrupoFacturacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpresasGrupos_GrupoFacturacionId",
                table: "EmpresasGrupos",
                column: "GrupoFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDeCredito_ReciboOriginalId",
                table: "NotasDeCredito",
                column: "ReciboOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_EmpresaId_GrupoFacturacionId_PeriodoAnio_PeriodoMes",
                table: "Recibos",
                columns: new[] { "EmpresaId", "GrupoFacturacionId", "PeriodoAnio", "PeriodoMes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_GrupoFacturacionId",
                table: "Recibos",
                column: "GrupoFacturacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "EmailsEmpresa");

            migrationBuilder.DropTable(
                name: "EmpresasGrupos");

            migrationBuilder.DropTable(
                name: "NotasDeCredito");

            migrationBuilder.DropTable(
                name: "Recibos");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropTable(
                name: "Grupos");
        }
    }
}
