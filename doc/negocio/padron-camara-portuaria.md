# Padrón — Cámara Portuaria de Bahía Blanca

Fuente: Excel `CUIT CAMAR CENTRO.xlsx` (cargado 2026-06-10).
Refleja el estado del `SeedData.cs` de `CamaraPortuaria.UI`.

---

## Grupos de facturación

| Grupo | Importe | Líneas |
|---|---|---|
| Cuota Social 2026 | $25 000 | Cuota societaria mensual $20 000 + Fondo de obras $5 000 |
| Cuota Extraordinaria Fija | — (configurar por UI) | Cuota extraordinaria fija |
| Papelería | — (configurar por UI) | Aporte papelería |

> **Nota Walsh / Trans Ona:** Agencia Marítima Walsh factura un ítem extra "Aporte Trans Ona" dentro de
> su cuota social. No es una entidad separada (usa el CUIT de Walsh). Agregar manualmente como línea
> adicional en el grupo correspondiente.

---

## Empresas (27 socios)

Las columnas **C. Social**, **C. Extra** y **Papel.** indican pertenencia al grupo.

| CUIT | Nombre | Razón Social | Email(s) | C. Social | C. Extra | Papel. |
|---|---|---|---|:---:|:---:|:---:|
| 30621973173 | ADM Agro | ADM Agro S.R.L. | Lucas.Majnach2@adm.com | ✓ | ✓ | |
| 30613198918 | Agencia Marítima Martin | Agencia Marítima Martin S.R.L. | adm@martin-shipping.com.ar | ✓ | | ✓ |
| 30506738128 | Agencia Marítima Walsh | Agencia Marítima Walsh E Burton S.R.L. | adminis@walsh.com.ar | ✓ | | ✓ |
| 30506775627 | Amarradores del Puerto | Amarradores del Puerto de Bahía Blanca S.C. | flaviaparedes@lanchasdelsur.com · cintiapoggio@lanchasdelsur.com | ✓ | | |
| 30635572287 | Antares Naviera | Antares Naviera S.A.U. | adm@martin-shipping.com.ar | ✓ | | |
| 30500120882 | Asoc. Cooperativas Arg. | Asociación de Cooperativas Argentinas Coop. Ltda. | bbaprovedores@acacoop.com.ar | ✓ | | |
| 30688061039 | Bahía Petróleo | Bahía Petróleo S.A. | administracion@bahiapetroleo.com | ✓ | | |
| 30700869918 | Bunge Argentina | Bunge Argentina S.A. | marcelo.verdi@bunge.com · facturaelectronicapagosvarios.bar@bunge.com | ✓ | ✓ | |
| 30506792165 | Cargill | Cargill Soc. Anón. Com. e Industrial | Melanie_Pagnanelli@cargill.com | ✓ | ✓ | |
| 30681505381 | Celsur Logística | Celsur Logística S.A. | mguzman@celsur.com.ar | ✓ | | |
| 33506737449 | Cofco International | Cofco International Argentina S.A. | mesaneco@cofcointernational.com | ✓ | | |
| 30680766610 | Donmar | Donmar S.A. | fmezzano@serviciosmaritimos.com | ✓ | | |
| 30707788379 | Estibajes Bahía | Estibajes Bahía S.R.L. | elalabi@estibajesbahia.com.ar | ✓ | | |
| 30644285584 | Ferroexpreso Pampeano | Ferroexpreso Pampeano S.A. Concesionaria | fepmav@fepsa.com.ar | ✓ | | |
| 30612683537 | Fugran | Fugran Comercial e Industrial S.A. | mpresutti@fugran.com | ✓ | | |
| 30688075579 | Graneles | Graneles S.R.L. | MGORLA@GRANELES.COM.AR | ✓ | | ✓ |
| 30526712729 | LDC Argentina | LDC Argentina S.A. | DIAMELA.RANIOLO@ldc.com | ✓ | ✓ | |
| 30506726669 | Murchison | Murchison S.A. Estibajes y Cargas | fcproveedoresbahiablanca@murchison.com.ar | ✓ | | ✓ |
| 30670561271 | Patagonia Estibajes | Patagonia Estibajes S.A. | proveedores@patagoniaestibajes.com | ✓ | | |
| 30708595388 | Puerto Frío | Puerto Frío S.A. | info@puertofrio.com.ar | ✓ | | |
| 30707232338 | Sea White | Sea White S.A. | accounting@seawhite.com.ar | ✓ | | ✓ |
| 30708153873 | Sycap | Sycap S.A. Servicios y Controles Agro Portuarios | administracion@sycap.com.ar | ✓ | | |
| 30711194084 | Tecnophos | Tecnophos Services S.A. | administracion@tecnophos.com.ar | ✓ | | |
| 30660168105 | Terminal Bahía Blanca | Terminal Bahía Blanca S.A. | natalia.sola@bunge.com | ✓ | ✓ | |
| 30689579864 | Terminal Patagonia Norte | Terminal de Servicios Portuarios Patagonia Norte S.A. | gtarodo@patagonia-norte.com.ar | ✓ | | |
| 30684503797 | Transportes Crexell | Transportes Crexell S.A. | proveedores@crexellsa.com.ar | ✓ | | |
| 30647178908 | United Seas | United Seas S.R.L. | leticia.alza@moggia.com.ar · sandra.mishevitch@unitedseas.com.ar | ✓ | | ✓ |

**Totales:** 27 socios · Cuota Extraordinaria Fija: 5 · Papelería: 6

---

## Observaciones operativas

- **ADM Agro** y **LDC Argentina**: emiten con orden de compra (LLEVA OC).
- **ADM Agro** también es socio de Centro Marítimo (mismo CUIT 30621973173).
- **Agencia Marítima Walsh** y **Cargill**: socios compartidos con Centro Marítimo.
- **Sea White**, **Terminal Bahía Blanca**, **United Seas**: socios compartidos con Centro Marítimo.
