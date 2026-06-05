using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CentroMaritimo
{
    /// <inheritdoc />
    public partial class InicialCentro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agencias",
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
                    table.PrimaryKey("PK_Agencias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Barcos",
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
                    table.PrimaryKey("PK_Barcos", x => x.Id);
                });

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
                    UsarApoderado = table.Column<bool>(type: "INTEGER", nullable: false),
                    NombreApoderado = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CuitApoderado = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
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
                name: "Contadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UltimoNumero = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contadores", x => x.Id);
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
                name: "EmailsAgencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgenciaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailsAgencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailsAgencia_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenciasGrupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgenciaId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenciasGrupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenciasGrupos_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgenciasGrupos_Grupos_GrupoFacturacionId",
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
                    AgenciaId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EsConsolidadoVouchers = table.Column<bool>(type: "INTEGER", nullable: false),
                    EsApoderado = table.Column<bool>(type: "INTEGER", nullable: false),
                    NombreApoderado = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CuitApoderado = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
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
                        name: "FK_Recibos_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
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

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgenciaId = table.Column<int>(type: "INTEGER", nullable: false),
                    BarcoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    ReciboId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vouchers_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Barcos_BarcoId",
                        column: x => x.BarcoId,
                        principalTable: "Barcos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Recibos_ReciboId",
                        column: x => x.ReciboId,
                        principalTable: "Recibos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Configuraciones",
                columns: new[] { "Id", "AfipCertificadoPassword", "AfipCertificadoRuta", "AfipUsarHomologacion", "CodigoAfipNotaDeCredito", "CodigoAfipRecibo", "CreatedAt", "Cuit", "CuitApoderado", "DiasVencimiento", "EmailRemitente", "NombreApoderado", "PuntoDeVenta", "RazonSocial", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpUsuario", "UpdatedAt", "UsarApoderado" },
                values: new object[] { 1, null, null, false, 13, 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", null, 30, null, null, 1, "", null, null, 587, null, null, false });

            migrationBuilder.InsertData(
                table: "Contadores",
                columns: new[] { "Id", "CreatedAt", "UltimoNumero", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, null });

            migrationBuilder.CreateIndex(
                name: "IX_Agencias_Cuit",
                table: "Agencias",
                column: "Cuit");

            migrationBuilder.CreateIndex(
                name: "IX_AgenciasGrupos_AgenciaId_GrupoFacturacionId",
                table: "AgenciasGrupos",
                columns: new[] { "AgenciaId", "GrupoFacturacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgenciasGrupos_GrupoFacturacionId",
                table: "AgenciasGrupos",
                column: "GrupoFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Barcos_Nombre",
                table: "Barcos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailsAgencia_AgenciaId",
                table: "EmailsAgencia",
                column: "AgenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDeCredito_ReciboOriginalId",
                table: "NotasDeCredito",
                column: "ReciboOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_AgenciaId_GrupoFacturacionId_PeriodoAnio_PeriodoMes",
                table: "Recibos",
                columns: new[] { "AgenciaId", "GrupoFacturacionId", "PeriodoAnio", "PeriodoMes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_AgenciaId_PeriodoAnio_PeriodoMes",
                table: "Recibos",
                columns: new[] { "AgenciaId", "PeriodoAnio", "PeriodoMes" },
                unique: true,
                filter: "\"EsConsolidadoVouchers\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_GrupoFacturacionId",
                table: "Recibos",
                column: "GrupoFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_AgenciaId",
                table: "Vouchers",
                column: "AgenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_BarcoId",
                table: "Vouchers",
                column: "BarcoId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Numero",
                table: "Vouchers",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReciboId",
                table: "Vouchers",
                column: "ReciboId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgenciasGrupos");

            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "Contadores");

            migrationBuilder.DropTable(
                name: "EmailsAgencia");

            migrationBuilder.DropTable(
                name: "NotasDeCredito");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Barcos");

            migrationBuilder.DropTable(
                name: "Recibos");

            migrationBuilder.DropTable(
                name: "Agencias");

            migrationBuilder.DropTable(
                name: "Grupos");
        }
    }
}
