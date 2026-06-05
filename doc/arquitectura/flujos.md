# Flujos de negocio → técnicos

> Pendiente de definición en sesión de arquitectura.

Este documento debe describir los flujos principales mapeados a capas técnicas:

## Flujos a documentar

1. **Emisión masiva de recibos** — `UI → ViewModel → IReciboService → IAfipService + IPdfService + IMailService`
2. **Emisión individual de recibo** — variante del flujo masivo para un solo destinatario
3. **Emisión de nota de crédito** — cancela un recibo emitido previamente
4. **Cierre de período (Centro Marítimo)** — consolida vouchers en un recibo por agencia
5. **ABM de empresas/agencias** — CRUD básico con validación de duplicados
6. **Backup manual de base de datos**

## Formato sugerido por flujo

```
Trigger: [acción del usuario]
UI: [evento/command]
ViewModel: [método llamado, validaciones previas]
Service: [interfaz usada, qué devuelve]
Infrastructure: [repositorios, DbContext]
Core: [entidades afectadas, estado resultante]
```
