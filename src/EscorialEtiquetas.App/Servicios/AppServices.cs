using EscorialEtiquetas.Core.Configuracion;
using EscorialEtiquetas.Core.Datos;
using EscorialEtiquetas.Core.Impresion;
using EscorialEtiquetas.Core.Servicios;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace EscorialEtiquetas.App.Servicios;

/// <summary>Raíz de composición: arma los servicios del Core con la configuración real.</summary>
public sealed class AppServices
{
    public required AppSettings Settings { get; init; }
    public required ISettingsStore SettingsStore { get; init; }
    public required LocalSecrets Secrets { get; init; }
    public required EtiquetaService Etiquetas { get; init; }
    public required CatalogoService Catalogo { get; init; }
    public required UsuarioService Usuarios { get; init; }
    public required HabilitacionService Habilitaciones { get; init; }
    public required ActualizacionService Actualizaciones { get; init; }

    public static AppServices Crear()
    {
        var store = new JsonSettingsStore();
        var settings = store.Load();
        var secrets = LocalSecrets.Load()
            ?? throw new InvalidOperationException(
                $"No se encontró '{LocalSecrets.FileName}'. Copialo junto al ejecutable, en " +
                $"{Path.GetDirectoryName(JsonSettingsStore.DefaultPath())} o indicá la ruta con ESCORIAL_SECRETS.");

        IDbContextFactory<EtiquetasDbContext> db = new ZonaDbContextFactory(settings, secrets);
        var reloj = TimeProvider.System;

        return new AppServices
        {
            Settings = settings,
            SettingsStore = store,
            Secrets = secrets,
            Etiquetas = new EtiquetaService(db, settings, new RawPrinter(), new ArchivoPlantillaReader(() => settings.MapeoRutas), reloj),
            Catalogo = new CatalogoService(db, settings),
            Usuarios = new UsuarioService(db),
            Habilitaciones = new HabilitacionService(
                db,
                new PostgresHabilitacionOrigen(() => secrets.PostgresConnectionString(settings.IpPublica)),
                reloj),
            Actualizaciones = new ActualizacionService(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, VersionActual()),
        };
    }

    /// <summary>Versión con la que compiló el CI (-p:Version), sin el sufijo "+commit".</summary>
    private static string VersionActual() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0-dev";
}
