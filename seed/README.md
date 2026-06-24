# Bases semilla de handoff (producción)

Estas son las bases SQLite **iniciales** que se entregan al cliente para arrancar a trabajar.
Contienen únicamente los **datos maestros reales** y **no** datos transaccionales:

- Socios/agencias reales (CUIT + emails reales) y sus relaciones con los grupos de facturación.
- Grupos de facturación (cuotas) — **ojo:** importes hoy *placeholder*, actualizar antes del handoff final.
- Singletons de fábrica (de la migración): `Configuracion` con identidad fiscal **vacía**,
  `PuntoDeVenta` "Principal", `CuentaCorreo` vacía y (Centro) `ContadorVoucher` en 0.

**No** incluyen: recibos, notas de crédito, barcos ni vouchers. La identidad fiscal del emisor,
el certificado AFIP y la cuenta SMTP los completa el cliente desde **Configuración**.

| Base | App | Destino en la PC del cliente |
|------|-----|------------------------------|
| `camara-portuaria.db` | Cámara Portuaria | `%LOCALAPPDATA%\Puerto de Bahia Blanca\CamaraPortuaria\camara-portuaria.db` |
| `centro-maritimo.db`  | Centro Marítimo  | `%LOCALAPPDATA%\Puerto de Bahia Blanca\CentroMaritimo\centro-maritimo.db` |

## Cómo regenerarlas

Desde el build de cada app (`--seed-prod` migra el esquema + siembra solo los maestros reales,
sin abrir la ventana):

```
CamaraPortuaria.exe --seed-prod "<ruta>\seed\camara-portuaria.db"
CentroMaritimo.exe  --seed-prod "<ruta>\seed\centro-maritimo.db"
```

La app instalada va con `appsettings.json → PuertoBB:SeedMockData = false`, así que **no** genera
datos por su cuenta: arranca con lo que trae esta base.
