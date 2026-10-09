using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Escorial.Etiquetas.Core.Datos;

// Entidades mapeadas a las tablas existentes de la base "Etiquetas" (SQL Server).
// Equivalen a las clases generadas desde Etiquetas.edmx, solo las que usa la app.

[Table("Productos")]
public class Producto
{
    [Key, Column("id"), MaxLength(50)]
    public string Id { get; set; } = "";

    [Column("descripcion"), MaxLength(50)]
    public string? Descripcion { get; set; }

    [Column("visualizacion"), MaxLength(50)]
    public string? Visualizacion { get; set; }

    [Column("rutaetiqueta")]
    public string? RutaEtiqueta { get; set; }

    [Column("rutaetiquetacon"), Unicode(false)]
    public string? RutaEtiquetaCon { get; set; }
}

[Table("EtiquetasMaestroCocinas")]
public class EtiquetaCocina
{
    [Key, Column("numero"), DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Numero { get; set; }

    [Column("modelo"), MaxLength(50)]
    public string? Modelo { get; set; }

    [Column("tipogas"), MaxLength(50)]
    public string? TipoGas { get; set; }

    [Column("equipo"), MaxLength(50)]
    public string? Equipo { get; set; }

    /// <summary>Formato "dd/MM/yyyy-HH:mm:ss" (texto, como en la tabla original).</summary>
    [Column("fecha"), MaxLength(50)]
    public string? Fecha { get; set; }

    [Column("idproducto"), MaxLength(50)]
    public string? IdProducto { get; set; }

    [Column("usuario"), MaxLength(50)]
    public string? Usuario { get; set; }
}

[Table("EtiquetasMaestroTermotanques")]
public class EtiquetaTermotanque
{
    [Key, Column("numero"), DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Numero { get; set; }

    [Column("modelo"), MaxLength(50)]
    public string? Modelo { get; set; }

    [Column("fecha"), MaxLength(50)]
    public string? Fecha { get; set; }

    [Column("idproducto"), MaxLength(50)]
    public string? IdProducto { get; set; }

    [Column("usuario"), MaxLength(50)]
    public string? Usuario { get; set; }
}

[Table("HabilitacionProductos")]
public class HabilitacionProducto
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("productos_id"), MaxLength(50), Unicode(false)]
    public string ProductoId { get; set; } = "";

    [Column("habilitado")]
    public bool Habilitado { get; set; }

    [Column("actualizacion", TypeName = "datetime")]
    public DateTime Actualizacion { get; set; }
}

[Table("TBL_GAS")]
public class Gas
{
    [Key, Column("ID"), DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Column("DESCRIPCION"), MaxLength(50)]
    public string? Descripcion { get; set; }

    [Column("IDENTIFICACION"), MaxLength(50)]
    public string? Identificacion { get; set; }
}

[Table("TBL_EQUIP")]
public class Equipamiento
{
    [Key, Column("ID"), DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Column("DESCRIPCION"), MaxLength(50)]
    public string? Descripcion { get; set; }

    [Column("IDENTIFICACION"), MaxLength(50)]
    public string? Identificacion { get; set; }
}

/// <summary>Modelos de producto (con su foto) que se ofrecen en la pantalla principal.</summary>
[Table("TBL_IMAGEN")]
public class Modelo
{
    [Key, Column("ID")]
    public int Id { get; set; }

    [Column("IMAGEN", TypeName = "image")]
    public byte[]? Imagen { get; set; }

    [Column("DESCRIPCION"), MaxLength(50)]
    public string? Descripcion { get; set; }

    /// <summary>Filtro sobre TBL_GAS.IDENTIFICACION.</summary>
    [Column("TABLAG"), MaxLength(50)]
    public string? TablaG { get; set; }

    /// <summary>Filtro sobre TBL_EQUIP.IDENTIFICACION.</summary>
    [Column("TABLAE"), MaxLength(50)]
    public string? TablaE { get; set; }

    /// <summary>"COCINAS" o "TERMOS" (nchar(10) en la base).</summary>
    [Column("CATEGORIA", TypeName = "nchar(10)")]
    public string? Categoria { get; set; }
}

[Table("Users")]
public class Usuario
{
    [Key, Column("id"), DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Column("usuario"), MaxLength(50)]
    public string Nombre { get; set; } = "";

    [Column("nombre"), MaxLength(50)]
    public string NombreCompleto { get; set; } = "";

    [Column("contrasena"), MaxLength(50)]
    public string Contrasena { get; set; } = "";

    [Column("permisos")]
    public int Permisos { get; set; }
}
