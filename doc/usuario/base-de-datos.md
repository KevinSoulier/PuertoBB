# Base de datos — Backup, Restauración y Mantenimiento

La pestaña **"Base de datos"** en Configuración agrupa todas las operaciones sobre el archivo SQLite
de la aplicación. Está disponible en ambas apps (CentroMaritimo y CamaraPortuaria).

---

## Backup

### Generar backup

Crea una copia exacta del estado actual de la base de datos en un archivo `.db` que elegís vos.

**Cuándo usarlo:** antes de migraciones, actualizaciones de la app, o periódicamente como respaldo.

**Cómo:**
1. Click en **"Generar backup…"**
2. Elegí dónde guardar el archivo (se sugiere un nombre con la fecha del día)
3. La app genera la copia y confirma con un mensaje de éxito

El mecanismo usado es `VACUUM INTO` de SQLite, que garantiza una copia consistente incluso si hay
operaciones en curso — no bloquea el uso normal de la app.

---

### Restaurar backup

Reemplaza la base de datos actual con una copia de seguridad guardada anteriormente.

> **Advertencia:** esta operación es destructiva. Todos los datos ingresados después de generar el
> backup **se perderán**. La app se cerrará al finalizar y tenés que reabrirla para continuar.

**Cuándo usarlo:** si la base quedó en un estado inconsistente, si necesitás volver a un punto
anterior, o para migrar datos a otra máquina.

**Cómo:**
1. Click en **"Restaurar backup…"**
2. Seleccioná el archivo `.db` de backup
3. La app **valida** que el archivo sea una base sana y de esta misma aplicación (si está dañado o
   es de la otra app, lo rechaza sin tocar tus datos)
4. Antes de reemplazar, la app **guarda automáticamente una copia de la base actual** (red de
   seguridad por si el backup elegido no era el correcto)
5. Leé la advertencia y confirmá con **"Restaurar"**
6. La app copia el archivo, muestra confirmación, y se cierra automáticamente
7. Al reabrir la app, los datos son los del backup

---

### Backups automáticos

Además de los backups manuales, la app **genera copias por su cuenta** para que nunca dependas de
acordarte:

- **Al abrir la app** (una vez por día): guarda una copia en la carpeta `Backups`.
- **Antes de actualizar la base** (cuando una versión nueva de la app cambia el esquema): guarda una
  copia previa, así una actualización fallida nunca te deja sin respaldo.
- **Antes de restaurar**: como se explicó arriba.

Cada copia se **verifica** apenas se crea (se abre y se le corre el chequeo de integridad), y se
**conservan las últimas 10** — las más viejas se borran solas para no llenar el disco.

El botón **"Abrir carpeta de backups"** (en la sección Backup) abre el Explorador en esa carpeta. Debajo
de los botones, un texto te recuerda **cuándo fue el último backup** (y te avisa si pasó más de una semana).

> **Recomendación importante:** los backups automáticos viven en **la misma máquina** que la base. Si
> el disco falla, se pierden junto con todo lo demás. Copiá la carpeta `Backups` cada tanto a un lugar
> externo: un pendrive, un disco aparte, o una carpeta de OneDrive/Google Drive.

---

## Mantenimiento

Estas herramientas sirven para mantener la base en buenas condiciones o diagnosticar problemas.
No modifican los datos de la aplicación.

### Verificar integridad

Comprueba que el archivo de la base no esté dañado.

**Cuándo usarlo:**
- Después de un corte de luz o cierre forzado de la app
- Si la app muestra errores raros o datos inconsistentes
- Antes de hacer una migración importante

**Qué hace:** ejecuta `PRAGMA integrity_check` de SQLite, que revisa la estructura interna del
archivo (páginas, índices, referencias entre tablas). Si todo está bien muestra "La base de datos
está en buen estado." Si hay problemas, lista los errores encontrados.

**Si aparecen errores:** la acción recomendada es restaurar el backup más reciente. Si no hay backup,
contactar al equipo de desarrollo.

---

### Compactar (VACUUM)

Reconstruye el archivo de la base para recuperar espacio en disco y mejorar la performance.

**Cuándo usarlo:**
- Después de borrar muchos registros (clientes, recibos históricos, etc.)
- Si el archivo `.db` creció mucho y querés recuperar espacio
- Como mantenimiento periódico (una vez cada pocos meses)

**Qué hace:** SQLite no libera espacio en disco al borrar registros — los marca como disponibles
para reutilizar. `VACUUM` reescribe el archivo completo, compactándolo. También rehace todos los
índices (equivalente a `REINDEX`).

> **Nota:** en bases grandes (varios GB) puede tardar algunos segundos. La app no se puede usar
> durante ese tiempo.

---

### Optimizar consultas

Actualiza las estadísticas internas que SQLite usa para decidir cómo ejecutar las consultas.

**Cuándo usarlo:**
- Si la app se siente más lenta de lo normal al cargar listas o buscar registros
- Después de importar o cargar muchos datos de una vez
- Como mantenimiento preventivo ocasional

**Qué hace:** ejecuta `PRAGMA optimize`, que analiza las tablas y actualiza las estadísticas del
*query planner*. Con estadísticas actualizadas, SQLite elige mejores estrategias de búsqueda (ej.
usar un índice en lugar de un scan completo). La operación es rápida y no modifica datos.

---

## Dónde se guarda la base de datos

El archivo `.db` vive en la carpeta de datos de la app dentro del perfil del usuario:

| App | Ruta del archivo |
|-----|-----------------|
| CentroMaritimo | `%LocalAppData%\PuertoBB\CentroMaritimo\centro-maritimo.db` |
| CamaraPortuaria | `%LocalAppData%\PuertoBB\CamaraPortuaria\camara-portuaria.db` |

En Windows, `%LocalAppData%` es típicamente `C:\Users\<usuario>\AppData\Local`.

Los backups automáticos se guardan en la subcarpeta `Backups` junto a la base (por ejemplo,
`%LocalAppData%\PuertoBB\CentroMaritimo\Backups\`).
