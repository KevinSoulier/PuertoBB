using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace CamaraPortuaria.UI.Data;

/// <summary>
/// Socios reales de la Cámara Portuaria de Bahía Blanca.
/// Se ejecuta solo si la base está vacía de empresas.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Si tiene valor, TODOS los socios se siembran con este único email — útil para
    /// probar envíos masivos sin escribir a las empresas reales. Poner en null para
    /// usar los emails reales de cada socio (los que figuran en cada Crear(...)).
    /// </summary>
    private static readonly string? EmailPruebas = "kevsoulier@gmail.com";

    public static async Task EnsureSeededAsync(CamaraPortuariaDbContext db)
    {
        if (await db.Empresas.AnyAsync()) return;

        var grupoSocial = new GrupoFacturacion
        {
            Nombre = "Cuota Social 2026", Importe = 25000m, CreatedAt = DateTime.Now,
            Lineas =
            [
                Linea("Cuota societaria mensual", 1, 20000m, 0),
                Linea("Fondo de obras", 1, 5000m, 1),
            ]
        };
        var grupoExtra = new GrupoFacturacion
        {
            Nombre = "Cuota Extraordinaria Fija", Importe = 0m, CreatedAt = DateTime.Now,
            Lineas = [Linea("Cuota extraordinaria fija", 1, 0m, 0)]
        };
        var grupoPapeleria = new GrupoFacturacion
        {
            Nombre = "Papelería", Importe = 0m, CreatedAt = DateTime.Now,
            Lineas = [Linea("Aporte papelería", 1, 0m, 0)]
        };
        db.Grupos.AddRange(grupoSocial, grupoExtra, grupoPapeleria);

        // Nota: Walsh (índice 2) incluye un ítem extra "Aporte Trans Ona" en su cuota social — gestionar por UI.
        var empresas = new[]
        {
            /* 0 */ Crear("ADM Agro",                 "ADM Agro S.R.L.",                                         "30621973173", "Lucas.Majnach2@adm.com"),
            /* 1 */ Crear("Agencia Marítima Martin",  "Agencia Marítima Martin S.R.L.",                          "30613198918", "adm@martin-shipping.com.ar"),
            /* 2 */ Crear("Agencia Marítima Walsh",   "Agencia Marítima Walsh E Burton S.R.L.",                  "30506738128", "adminis@walsh.com.ar"),
            /* 3 */ Crear("Amarradores del Puerto",   "Amarradores del Puerto de Bahía Blanca S.C.",             "30506775627", "flaviaparedes@lanchasdelsur.com", "cintiapoggio@lanchasdelsur.com"),
            /* 4 */ Crear("Antares Naviera",          "Antares Naviera S.A.U.",                                  "30635572287", "adm@martin-shipping.com.ar"),
            /* 5 */ Crear("Asoc. Cooperativas Arg.",  "Asociación de Cooperativas Argentinas Coop. Ltda.",       "30500120882", "bbaprovedores@acacoop.com.ar"),
            /* 6 */ Crear("Bahía Petróleo",           "Bahía Petróleo S.A.",                                     "30688061039", "administracion@bahiapetroleo.com"),
            /* 7 */ Crear("Bunge Argentina",          "Bunge Argentina S.A.",                                    "30700869918", "marcelo.verdi@bunge.com", "facturaelectronicapagosvarios.bar@bunge.com"),
            /* 8 */ Crear("Cargill",                  "Cargill Soc. Anón. Com. e Industrial",                   "30506792165", "Melanie_Pagnanelli@cargill.com"),
            /* 9 */ Crear("Celsur Logística",         "Celsur Logística S.A.",                                   "30681505381", "mguzman@celsur.com.ar"),
            /*10 */ Crear("Cofco International",      "Cofco International Argentina S.A.",                      "33506737449", "mesaneco@cofcointernational.com"),
            /*11 */ Crear("Donmar",                   "Donmar S.A.",                                             "30680766610", "fmezzano@serviciosmaritimos.com"),
            /*12 */ Crear("Estibajes Bahía",          "Estibajes Bahía S.R.L.",                                  "30707788379", "elalabi@estibajesbahia.com.ar"),
            /*13 */ Crear("Ferroexpreso Pampeano",    "Ferroexpreso Pampeano S.A. Concesionaria",                "30644285584", "fepmav@fepsa.com.ar"),
            /*14 */ Crear("Fugran",                   "Fugran Comercial e Industrial S.A.",                      "30612683537", "mpresutti@fugran.com"),
            /*15 */ Crear("Graneles",                 "Graneles S.R.L.",                                         "30688075579", "MGORLA@GRANELES.COM.AR"),
            /*16 */ Crear("LDC Argentina",            "LDC Argentina S.A.",                                      "30526712729", "DIAMELA.RANIOLO@ldc.com"),
            /*17 */ Crear("Murchison",                "Murchison S.A. Estibajes y Cargas",                       "30506726669", "fcproveedoresbahiablanca@murchison.com.ar"),
            /*18 */ Crear("Patagonia Estibajes",      "Patagonia Estibajes S.A.",                                "30670561271", "proveedores@patagoniaestibajes.com"),
            /*19 */ Crear("Puerto Frío",              "Puerto Frío S.A.",                                        "30708595388", "info@puertofrio.com.ar"),
            /*20 */ Crear("Sea White",                "Sea White S.A.",                                          "30707232338", "accounting@seawhite.com.ar"),
            /*21 */ Crear("Sycap",                    "Sycap S.A. Servicios y Controles Agro Portuarios",       "30708153873", "administracion@sycap.com.ar"),
            /*22 */ Crear("Tecnophos",                "Tecnophos Services S.A.",                                 "30711194084", "administracion@tecnophos.com.ar"),
            /*23 */ Crear("Terminal Bahía Blanca",    "Terminal Bahía Blanca S.A.",                              "30660168105", "natalia.sola@bunge.com"),
            /*24 */ Crear("Terminal Patagonia Norte", "Terminal de Servicios Portuarios Patagonia Norte S.A.",   "30689579864", "gtarodo@patagonia-norte.com.ar"),
            /*25 */ Crear("Transportes Crexell",      "Transportes Crexell S.A.",                                "30684503797", "proveedores@crexellsa.com.ar"),
            /*26 */ Crear("United Seas",              "United Seas S.R.L.",                                      "30647178908", "leticia.alza@moggia.com.ar", "sandra.mishevitch@unitedseas.com.ar"),
        };
        db.Empresas.AddRange(empresas);
        await db.SaveChangesAsync();

        foreach (var e in empresas)
            db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = e.Id, GrupoFacturacionId = grupoSocial.Id, CreatedAt = DateTime.Now });

        // Cuota Extraordinaria Fija: ADM Agro, Bunge, Cargill, LDC, Terminal Bahía Blanca
        foreach (var idx in new[] { 0, 7, 8, 16, 23 })
            db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = empresas[idx].Id, GrupoFacturacionId = grupoExtra.Id, CreatedAt = DateTime.Now });

        // Papelería: Martin, Walsh, Graneles, Murchison, Sea White, United Seas
        foreach (var idx in new[] { 1, 2, 15, 17, 20, 26 })
            db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = empresas[idx].Id, GrupoFacturacionId = grupoPapeleria.Id, CreatedAt = DateTime.Now });

        await db.SaveChangesAsync();
    }

    private static Empresa Crear(string nombre, string razon, string cuit, params string[] emails)
    {
        var email = EmailPruebas;
        var destino = email is null ? emails : new[] { email };
        return new()
        {
            Nombre = nombre,
            RazonSocial = razon,
            Cuit = cuit,
            CondicionIvaId = 1, // IVA Responsable Inscripto (dato demo; verificar con "Validar CUIT en ARCA")
            CreatedAt = DateTime.Now,
            Emails = destino.Select(e => new EmailEmpresa { Email = e, CreatedAt = DateTime.Now }).ToList()
        };
    }

    private static GrupoFacturacionLinea Linea(string descripcion, decimal cantidad, decimal precioUnitario, int orden) => new()
    {
        Descripcion = descripcion,
        Cantidad = cantidad,
        PrecioUnitario = precioUnitario,
        Importe = cantidad * precioUnitario,
        Orden = orden,
        CreatedAt = DateTime.Now
    };
}
