using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuertoBB.Infrastructure.Migrations.CentroMaritimo
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RazonSocial = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    Domicilio = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CondicionIvaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RazonSocial = table.Column<string>(type: "TEXT", nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", nullable: false),
                    IngresosBrutos = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    InicioActividades = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CodigoAfipRecibo = table.Column<int>(type: "INTEGER", nullable: false),
                    CodigoAfipNotaDeCredito = table.Column<int>(type: "INTEGER", nullable: false),
                    ImporteVoucherPredeterminado = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiasVencimiento = table.Column<int>(type: "INTEGER", nullable: false),
                    MailAsunto = table.Column<string>(type: "TEXT", nullable: true),
                    MailCuerpo = table.Column<string>(type: "TEXT", nullable: true),
                    MailCuerpoEsHtml = table.Column<bool>(type: "INTEGER", nullable: false),
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
                name: "EmailsCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailsCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailsCliente_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recibos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceptorNombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceptorRazonSocial = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceptorCuit = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    ReceptorDomicilio = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ReceptorCondicionIva = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReceptorCondicionIvaId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    PuntoDeVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComprobante = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodigoAfip = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "INTEGER", nullable: false),
                    CAE = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaVencimientoCAE = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstadoFiscal = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UltimoErrorCae = table.Column<string>(type: "TEXT", nullable: true),
                    UltimoErrorMail = table.Column<string>(type: "TEXT", nullable: true),
                    FechaEnvioMail = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaVencimientoPago = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaIncobrable = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoIncobrable = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recibos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recibos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CuentasCorreo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfiguracionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpSeguridad = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailRemitente = table.Column<string>(type: "TEXT", nullable: true),
                    Autenticacion = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUsuario = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthProveedor = table.Column<int>(type: "INTEGER", nullable: false),
                    OAuthFlujo = table.Column<int>(type: "INTEGER", nullable: false),
                    OAuthClientId = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthClientSecret = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthTenantId = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthScope = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthAuthorizeEndpoint = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthTokenEndpoint = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthRefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    OAuthUsuario = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasCorreo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasCorreo_Configuraciones_ConfiguracionId",
                        column: x => x.ConfiguracionId,
                        principalTable: "Configuraciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    CertificadoContenido = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CertificadoPassword = table.Column<string>(type: "TEXT", nullable: true),
                    CertificadoKeyRuta = table.Column<string>(type: "TEXT", nullable: true),
                    CertificadoKeyContenido = table.Column<byte[]>(type: "BLOB", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "ClientesGrupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesGrupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientesGrupos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientesGrupos_Grupos_GrupoFacturacionId",
                        column: x => x.GrupoFacturacionId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GruposLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Cantidad = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposLineas_Grupos_GrupoFacturacionId",
                        column: x => x.GrupoFacturacionId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Consolidaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReciboId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    Pendiente = table.Column<bool>(type: "INTEGER", nullable: false),
                    Individual = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consolidaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consolidaciones_Recibos_ReciboId",
                        column: x => x.ReciboId,
                        principalTable: "Recibos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmisionesGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrupoFacturacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReciboId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmisionesGrupo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmisionesGrupo_Grupos_GrupoFacturacionId",
                        column: x => x.GrupoFacturacionId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmisionesGrupo_Recibos_ReciboId",
                        column: x => x.ReciboId,
                        principalTable: "Recibos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "RecibosLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReciboId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Cantidad = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecibosLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecibosLineas_Recibos_ReciboId",
                        column: x => x.ReciboId,
                        principalTable: "Recibos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    BarcoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodoAnio = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodoMes = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsolidacionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vouchers_Barcos_BarcoId",
                        column: x => x.BarcoId,
                        principalTable: "Barcos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Consolidaciones_ConsolidacionId",
                        column: x => x.ConsolidacionId,
                        principalTable: "Consolidaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Configuraciones",
                columns: new[] { "Id", "CodigoAfipNotaDeCredito", "CodigoAfipRecibo", "CreatedAt", "Cuit", "DiasVencimiento", "ImporteVoucherPredeterminado", "IngresosBrutos", "InicioActividades", "MailAsunto", "MailCuerpo", "MailCuerpoEsHtml", "RazonSocial", "UpdatedAt" },
                values: new object[] { 1, 13, 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", 15, 0m, null, null, "{comprobante} {periodo} — {razonSocial}", "Estimados,\n\nAdjuntamos el comprobante correspondiente al período {periodo}.\n\nSaludos.", false, "", null });

            migrationBuilder.InsertData(
                table: "Contadores",
                columns: new[] { "Id", "CreatedAt", "UltimoNumero", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, null });

            migrationBuilder.InsertData(
                table: "CuentasCorreo",
                columns: new[] { "Id", "Activo", "Autenticacion", "ConfiguracionId", "CreatedAt", "EmailRemitente", "Nombre", "OAuthAuthorizeEndpoint", "OAuthClientId", "OAuthClientSecret", "OAuthFlujo", "OAuthProveedor", "OAuthRefreshToken", "OAuthScope", "OAuthTenantId", "OAuthTokenEndpoint", "OAuthUsuario", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpSeguridad", "SmtpUsuario", "UpdatedAt" },
                values: new object[] { 1, true, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Principal", null, null, null, 0, 0, null, null, null, null, null, null, null, 587, 0, null, null });

            migrationBuilder.InsertData(
                table: "PuntosDeVenta",
                columns: new[] { "Id", "Activo", "CertificadoContenido", "CertificadoKeyContenido", "CertificadoKeyRuta", "CertificadoPassword", "CertificadoRuta", "ConfiguracionId", "CreatedAt", "Nombre", "Numero", "UpdatedAt", "UsarHomologacion" },
                values: new object[] { 1, true, null, null, null, null, null, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Principal", 1, null, false });

            migrationBuilder.CreateIndex(
                name: "IX_Barcos_Nombre",
                table: "Barcos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientesGrupos_ClienteId_GrupoFacturacionId",
                table: "ClientesGrupos",
                columns: new[] { "ClienteId", "GrupoFacturacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientesGrupos_GrupoFacturacionId",
                table: "ClientesGrupos",
                column: "GrupoFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosRecibo_Nombre",
                table: "ConceptosRecibo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consolidaciones_ClienteId_PeriodoAnio_PeriodoMes",
                table: "Consolidaciones",
                columns: new[] { "ClienteId", "PeriodoAnio", "PeriodoMes" },
                unique: true,
                filter: "\"Pendiente\" = 1 AND \"Individual\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Consolidaciones_ReciboId",
                table: "Consolidaciones",
                column: "ReciboId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasCorreo_ConfiguracionId",
                table: "CuentasCorreo",
                column: "ConfiguracionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailsCliente_ClienteId",
                table: "EmailsCliente",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EmisionesGrupo_GrupoFacturacionId_ClienteId_PeriodoAnio_PeriodoMes",
                table: "EmisionesGrupo",
                columns: new[] { "GrupoFacturacionId", "ClienteId", "PeriodoAnio", "PeriodoMes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmisionesGrupo_ReciboId",
                table: "EmisionesGrupo",
                column: "ReciboId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposLineas_GrupoFacturacionId",
                table: "GruposLineas",
                column: "GrupoFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDeCredito_ReciboOriginalId",
                table: "NotasDeCredito",
                column: "ReciboOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuntosDeVenta_ConfiguracionId_Numero",
                table: "PuntosDeVenta",
                columns: new[] { "ConfiguracionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_ClienteId",
                table: "Recibos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_PeriodoAnio_PeriodoMes",
                table: "Recibos",
                columns: new[] { "PeriodoAnio", "PeriodoMes" });

            migrationBuilder.CreateIndex(
                name: "IX_Recibos_PuntoDeVenta_NumeroComprobante_CodigoAfip",
                table: "Recibos",
                columns: new[] { "PuntoDeVenta", "NumeroComprobante", "CodigoAfip" },
                unique: true,
                filter: "\"NumeroComprobante\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RecibosLineas_ReciboId",
                table: "RecibosLineas",
                column: "ReciboId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_BarcoId",
                table: "Vouchers",
                column: "BarcoId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ClienteId",
                table: "Vouchers",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ConsolidacionId",
                table: "Vouchers",
                column: "ConsolidacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Numero",
                table: "Vouchers",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientesGrupos");

            migrationBuilder.DropTable(
                name: "ConceptosRecibo");

            migrationBuilder.DropTable(
                name: "Contadores");

            migrationBuilder.DropTable(
                name: "CuentasCorreo");

            migrationBuilder.DropTable(
                name: "EmailsCliente");

            migrationBuilder.DropTable(
                name: "EmisionesGrupo");

            migrationBuilder.DropTable(
                name: "GruposLineas");

            migrationBuilder.DropTable(
                name: "NotasDeCredito");

            migrationBuilder.DropTable(
                name: "PuntosDeVenta");

            migrationBuilder.DropTable(
                name: "RecibosLineas");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Grupos");

            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "Barcos");

            migrationBuilder.DropTable(
                name: "Consolidaciones");

            migrationBuilder.DropTable(
                name: "Recibos");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
