# Escorial Etiquetas (multiplataforma)

Reescritura en .NET 10 + Avalonia de `Escorial-Etiquetas-Auto` (WinForms / .NET Framework 4.7.2).
Se desarrolla, compila y testea en Linux; corre en Linux y Windows.

## Estructura

```
src/Escorial.Etiquetas.Core/    Lógica, EF Core (SQL Server), impresión ZPL, Arduino, configuración
src/Escorial.Etiquetas.App/     Interfaz Avalonia (MVVM con CommunityToolkit.Mvvm)
tests/Escorial.Etiquetas.Core.Tests/  xUnit + SQLite en memoria
tests/Escorial.Etiquetas.App.Tests/   ViewModels + UI headless (Avalonia.Headless)
```

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/Escorial.Etiquetas.App
dotnet publish src/Escorial.Etiquetas.App -c Release -r win-x64 --self-contained -o publish/win-x64
dotnet publish src/Escorial.Etiquetas.App -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

Los tests de UI dejan capturas en `tests/Escorial.Etiquetas.App.Tests/bin/Debug/net10.0/capturas/`.

## Configuración

**Credenciales:** `secrets.local.xml`, con el mismo formato que la app anterior y nunca versionado. Se busca en este orden:

1. La variable de entorno `ESCORIAL_SECRETS` (ruta completa al archivo).
2. Junto al ejecutable. Si está en `src/Escorial.Etiquetas.App/`, se copia al compilar.
3. `~/.config/EscorialEtiquetas/` (Linux) o `%APPDATA%\EscorialEtiquetas\` (Windows).
4. Las carpetas superiores al ejecutable.

**Preferencias:** `settings.json`, en la misma carpeta de usuario. Reemplaza a `Properties.Settings` y se edita desde la pantalla Configuración.

## Diferencias con la versión WinForms

| Tema | Antes | Ahora |
|---|---|---|
| Acceso a datos | EF6 + EDMX | EF Core 10, entidades en `Core/Datos/Entidades.cs` |
| Impresión | `EscoBLL.RawPrinterHelper` (winspool) | `tcp://IP:9100` directo a la Zebra (cualquier SO), cola de Windows (winspool) o CUPS (`lp -o raw`) en Linux |
| Plantillas `rutaetiqueta` | Rutas UNC | En Linux se traducen con "Mapeo de rutas" (Configuración → Avanzadas), p.ej. `\\srv\etiquetas=/mnt/etiquetas` |
| Postgres (habilitaciones) | SQL concatenado | Consulta parametrizada |
| Producto sin fila en `HabilitacionProductos` | NullReferenceException | Se considera no habilitado |
| Configuración → Avanzadas | Botón deshabilitado | Se desbloquea con la clave de `administrador` |
| Clave de impresión inválida | No hacía nada | Muestra un error |
| Arduino / impresión automática | Comentado | Clase portada (`Core/Hardware/Arduino.cs`), botón sigue deshabilitado |

`ImprimirAdvertenciaCambioModelo` (etiqueta de aviso con modelo, gas, equipamiento y logo) estaba
deshabilitada en WinForms con un `todo: volver a habilitar`. Acá se porta detrás de la opción
"Imprimir aviso al cambiar de modelo" en Configuración, **apagada por defecto**. Con la opción
encendida se imprime un solo aviso por cada cambio del operario. No se imprime al abrir la app ni
al volver de Configuración (en WinForms cada combo disparaba su propio aviso).

## Linux: notas

- **Puerto serie:** el usuario tiene que estar en el grupo `dialout` (`sudo usermod -aG dialout $USER`).
- **Impresión por cola:** hace falta CUPS con la Zebra configurada como cola raw. Lo más simple es usar `tcp://IP:9100`.
- **SQL Server viejo:** si la conexión falla por TLS, agregar `TrustServerCertificate=True` al connection string.
