# Escorial Etiquetas (multiplataforma)

Reescritura en .NET 10 + Avalonia de `Escorial-Etiquetas-Auto` (WinForms / .NET Framework 4.7.2).
Se desarrolla, compila y testea en Linux; corre en Linux y Windows.

## Estructura

```
src/EscorialEtiquetas.Core/    Lógica, EF Core (SQL Server), impresión ZPL, Arduino, configuración
src/EscorialEtiquetas.App/     Interfaz Avalonia (MVVM con CommunityToolkit.Mvvm)
tests/EscorialEtiquetas.Core.Tests/  xUnit + SQLite en memoria
tests/EscorialEtiquetas.App.Tests/   ViewModels + UI headless (Avalonia.Headless)
```

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/EscorialEtiquetas.App
dotnet publish src/EscorialEtiquetas.App -c Release -r win-x64 --self-contained -o publish/win-x64
dotnet publish src/EscorialEtiquetas.App -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

Los tests de UI dejan capturas en `tests/EscorialEtiquetas.App.Tests/bin/Debug/net10.0/capturas/`.

## Releases

Todo cambio entra por PR a `dev`; `main` recibe solo PRs desde `dev`.

- Cada PR corre los tests (`test`, más un check por test) y compila los binarios (`Compilar binarios`) con la versión definitiva. Los dos checks son obligatorios.
- Al mergear se publica en Releases el mismo zip (win-x64) y tar.gz (linux-x64) compilados en la PR, sin recompilar.
- Versión: etiqueta de la PR `bugfix` (default), `feature` o `breaking`. Los merges a `dev` publican pre-releases `X.Y.Z-rc.N`; la PR `dev` → `main` publica la estable `X.Y.Z`.
- La versión no está en el código: sale de los tags (`git tag`). Las PRs a `dev` tienen que estar al día con `dev`.

## Configuración

**Credenciales:** `secrets.local.xml`, con el mismo formato que la app anterior y nunca versionado. Se busca en este orden:

1. La variable de entorno `ESCORIAL_SECRETS` (ruta completa al archivo).
2. Junto al ejecutable. Si está en `src/EscorialEtiquetas.App/`, se copia al compilar.
3. `~/.config/EscorialEtiquetas/` (Linux) o `%APPDATA%\EscorialEtiquetas\` (Windows).
4. Las carpetas superiores al ejecutable.

```xml
<secrets>
  <postgres>
    <host>srv-postgres</host>       <!-- opcional -->
    <database>ESCORIAL</database>   <!-- opcional, default ESCORIAL -->
    <user>usuario</user>
    <password>clave</password>
  </postgres>
  <sql>
    <cocinas><providerConnectionString>Server=srv-sql;Database=COCINAS;User Id=usuario;Password=clave;TrustServerCertificate=True</providerConnectionString></cocinas>
    <termos><providerConnectionString>Server=srv-sql;Database=TERMOS;User Id=usuario;Password=clave;TrustServerCertificate=True</providerConnectionString></termos>
  </sql>
</secrets>
```

- Un único `sql/providerConnectionString` (formato viejo) vale para las dos zonas. Con autenticación de Windows: `Integrated Security=True` en lugar de `User Id`/`Password`.
- Es XML: en las claves, `&` se escribe `&amp;` y `<` se escribe `&lt;`.
- Los releases no incluyen credenciales: el archivo se copia una vez por equipo en `%APPDATA%\EscorialEtiquetas\` y las actualizaciones no lo tocan. Si el equipo lo usan varios usuarios de Windows, dejarlo en `C:\ProgramData\EscorialEtiquetas\` y definir `ESCORIAL_SECRETS` como variable de sistema.
- Las claves quedan en texto plano: restringir los permisos del archivo y usar un usuario de base con los permisos mínimos.

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
