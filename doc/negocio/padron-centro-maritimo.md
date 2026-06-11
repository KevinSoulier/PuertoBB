# Padrón — Centro Marítimo de Bahía Blanca

Fuente: Excel `CUIT CAMAR CENTRO.xlsx` (cargado 2026-06-10).
Refleja el estado del `SeedData.cs` de `CentroMaritimo.UI`.

---

## Grupos de facturación

| Grupo | Importe | Líneas |
|---|---|---|
| Cuota Social 2026 | $18 000 | Cuota societaria mensual $15 000 + Aporte sostenimiento $3 000 |
| Tablas de Marea | — (configurar por UI) | Tablas de marea |

> **Nota Donmar:** Solo paga Tablas de Marea, aproximadamente 2 veces al año. **No** integra Cuota Social.
> Los importes y la periodicidad se gestionan manualmente por la UI de grupos.

> **Nota Walsh / Trans Ona:** Agencia Marítima Walsh factura un ítem extra "Aporte Trans Ona" dentro de
> su cuota social. No es una entidad separada. Agregar manualmente como línea adicional en el grupo correspondiente.

---

## Agencias (13 socios)

Las columnas **C. Social** y **T. Marea** indican pertenencia al grupo.

| CUIT | Nombre | Razón Social | Email(s) | C. Social | T. Marea |
|---|---|---|---|:---:|:---:|
| 30621973173 | ADM Agro | ADM Agro S.R.L. | Lucas.Majnach2@adm.com | ✓ | |
| 30643949381 | Ag. Marítima Austral | Agencia Marítima Austral S.R.L. | operaciones@agencia-austral.com.ar | ✓ | ✓ |
| 30585343427 | Ag. Marítima Internacional | Agencia Marítima Internacional S.A. | ltorres@ocean.com.ar | ✓ | ✓ |
| 30506738128 | Agencia Marítima Walsh | Agencia Marítima Walsh E Burton S.R.L. | adminis@walsh.com.ar | ✓ | ✓ |
| 30500120882 | Asoc. Cooperativas Arg. | Asociación de Cooperativas Argentinas Coop. Ltda. | bbaprovedores@acacoop.com.ar | ✓ | |
| 30506792165 | Cargill | Cargill Soc. Anón. Com. e Industrial | Melanie_Pagnanelli@cargill.com | ✓ | |
| 30680766610 | Donmar | Donmar S.A. | fmezzano@serviciosmaritimos.com | | ✓ |
| 30707691847 | Fertimport | Fertimport S.A. | bar.fertimport.adm@bunge.com | ✓ | ✓ |
| 30709247235 | Maritime Shipping Agency | Maritime Shipping Agency S.R.L. | nlamonega@isa-agents.com.ar | ✓ | ✓ |
| 30711320896 | Puerto White Multimodal | Puerto White Multimodal S.A. | administracion@puertowhite.com.ar | ✓ | |
| 30707232338 | Sea White | Sea White S.A. | accounting@seawhite.com.ar | ✓ | ✓ |
| 30660168105 | Terminal Bahía Blanca | Terminal Bahía Blanca S.A. | natalia.sola@bunge.com | ✓ | |
| 30647178908 | United Seas | United Seas S.R.L. | leticia.alza@moggia.com.ar · sandra.mishevitch@unitedseas.com.ar | ✓ | |

**Totales:** 13 socios · Cuota Social: 12 · Tablas de Marea: 7 (incluye Donmar)

---

## Observaciones operativas

- **ADM Agro**: emite con orden de compra (LLEVA OC). También es socio de Cámara Portuaria.
- **Donmar**: registrado en Centro pero **únicamente** para Tablas de Marea (2 veces al año).
- **Agencia Marítima Walsh**, **Cargill**, **Sea White**, **Terminal Bahía Blanca**, **United Seas**: socios compartidos con Cámara Portuaria.
- **Ag. Marítima Austral**, **Ag. Marítima Internacional**, **Fertimport**, **Maritime Shipping Agency**, **Puerto White Multimodal**: exclusivos de Centro Marítimo.
