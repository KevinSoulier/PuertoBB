# Reglas de dependencias

## Dirección permitida

```
UI → Core ← Services
UI → Core ← Infrastructure
```

## Reglas absolutas

- **Core no depende de ningún otro proyecto.** Nunca agregar referencias desde Core hacia Services, Infrastructure o UI.
- **Services solo depende de Core.** No puede referenciar Infrastructure.
- **Infrastructure solo depende de Core.** No puede referenciar Services.
- **UI puede referenciar Core, Services e Infrastructure.** Es el punto de composición.

## Por qué esta arquitectura

- Core define los contratos (interfaces) y los modelos. Es el lenguaje del dominio.
- Services e Infrastructure implementan esos contratos sin conocerse entre sí.
- La UI inyecta las implementaciones concretas vía DI al arrancar.

## Inyección de dependencias

El contenedor DI se configura en `App.xaml.cs` de cada proyecto UI usando `Microsoft.Extensions.Hosting`. Todos los servicios y repositorios se registran ahí como implementaciones de las interfaces definidas en Core.
