using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogoCitizenDevIA.Web.Data;

/// <summary>
/// DbContext real contra SQL Server (PERMS02 — YanbalCitizenDevIA). Mapea 1:1 el
/// esquema YA CREADO por el script DDL de mano (sql/01_schema_YanbalCitizenDevIA.sql,
/// documentado en 06-diseno-bd-sql-server.md) — a propósito NO se usan migraciones de
/// EF Core Code-First para crear el esquema, porque este incluye una función SQL
/// WITH SCHEMABINDING (dbo.fn_NivelRiesgo) que EF Core no puede generar. En Dev se
/// puede seguir usando `dotnet ef migrations script` solo como comparación de
/// esquema, nunca para aplicar contra PERMS02 (ver 06-diseno-bd-sql-server.md,
/// sección 5) — el DBA aplica siempre el script SQL revisado a mano.
///
/// Las columnas calculadas PERSISTED de dbo.Solucion (NivelRiesgo, ProximaRevision)
/// se mapean con ValueGeneratedOnAddOrUpdate(): de solo lectura para EF (nunca las
/// escribe en el INSERT/UPDATE), y las vuelve a leer después de SaveChanges para
/// traer el valor que realmente calculó SQL Server — la misma fuente autoritativa
/// de siempre (ver 08-reglas-negocio-calculo-riesgo.md y RiesgoCalculator, que sigue
/// usándose solo para el recálculo en vivo del formulario, nunca como lo persistido).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Plataforma> Plataformas => Set<Plataforma>();
    public DbSet<Solucion> Soluciones => Set<Solucion>();
    public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();
    public DbSet<NotificacionEnviada> NotificacionesEnviadas => Set<NotificacionEnviada>();
    public DbSet<Adjunto> Adjuntos => Set<Adjunto>();
    public DbSet<AdjuntoContenido> AdjuntoContenidos => Set<AdjuntoContenido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuario");
            e.HasKey(u => u.UsuarioId);
            e.Property(u => u.CorreoCorporativo).HasMaxLength(256).IsRequired();
            e.Property(u => u.NombreCompleto).HasMaxLength(200).IsRequired();
            e.Property(u => u.Area).HasMaxLength(150);
            e.Property(u => u.Pais).HasMaxLength(100);
            e.Property(u => u.FechaUltimoAcceso).HasColumnType("datetime2(0)");
            e.Property(u => u.FechaCreacion).HasColumnType("datetime2(0)");
            e.HasIndex(u => u.AzureAdObjectId).IsUnique();
            e.HasIndex(u => u.CorreoCorporativo).IsUnique();
        });

        modelBuilder.Entity<Plataforma>(e =>
        {
            e.ToTable("Plataforma");
            e.HasKey(p => p.PlataformaId);
            e.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
            e.Property(p => p.Categoria).HasMaxLength(100);
            e.Property(p => p.Estado).HasMaxLength(50).IsRequired();
            e.Property(p => p.Comentario).HasMaxLength(500);
            e.Property(p => p.FechaCreacion).HasColumnType("datetime2(0)");
            e.HasIndex(p => p.Nombre).IsUnique();
            e.HasOne<Usuario>().WithMany().HasForeignKey(p => p.CreadoPorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Solucion>(e =>
        {
            e.ToTable("Solucion");
            e.HasKey(s => s.SolucionId);
            e.Property(s => s.CodigoSolucion).HasMaxLength(20).IsRequired();
            e.Property(s => s.Nombre).HasMaxLength(250).IsRequired();
            e.Property(s => s.TipoSolucion).HasMaxLength(50).IsRequired();
            e.Property(s => s.DescripcionBreve).HasMaxLength(1000);
            e.Property(s => s.Area).HasMaxLength(150).IsRequired();
            e.Property(s => s.Pais).HasMaxLength(100).IsRequired();
            e.Property(s => s.AlcanceUso).HasMaxLength(20).IsRequired();
            e.Property(s => s.RequiereInfraDedicada).HasMaxLength(20).IsRequired();
            e.Property(s => s.RiesgoJustificacionOverride).HasMaxLength(1000);
            e.Property(s => s.Estado).HasMaxLength(30).IsRequired();
            e.Property(s => s.RevisadoPor).HasMaxLength(50);
            e.Property(s => s.Comentarios).HasMaxLength(2000);
            e.Property(s => s.EnlaceAcceso).HasMaxLength(500);
            e.Property(s => s.NivelMadurez).HasMaxLength(20);
            e.Property(s => s.ClasificacionInformacion).HasMaxLength(20);
            e.Property(s => s.Etiquetas).HasMaxLength(300);
            e.Property(s => s.FechaRegistro).HasColumnType("datetime2(0)");
            e.Property(s => s.FechaModificacion).HasColumnType("datetime2(0)");

            // Columnas calculadas PERSISTED en SQL Server (dbo.fn_NivelRiesgo) — EF
            // nunca las escribe, solo las relee despues de guardar. NO usar
            // HasComputedColumnSql aqui: esa opcion es para que EF GENERE la
            // columna calculada via migraciones, y el esquema ya existe de antes.
            e.Property(s => s.NivelRiesgo).HasMaxLength(10).ValueGeneratedOnAddOrUpdate();
            e.Property(s => s.ProximaRevision).ValueGeneratedOnAddOrUpdate();

            e.HasIndex(s => s.CodigoSolucion).IsUnique();
            e.HasIndex(s => s.Estado);
            e.HasIndex(s => s.NivelRiesgo);
            e.HasIndex(s => s.Pais);
            e.HasIndex(s => s.TipoSolucion);
            e.HasIndex(s => s.PlataformaId);

            e.HasOne<Usuario>().WithMany().HasForeignKey(s => s.UsuarioDuenioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(s => s.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Plataforma>().WithMany().HasForeignKey(s => s.PlataformaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(s => s.SponsorTiId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(s => s.RiesgoOverridePorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HistorialEstado>(e =>
        {
            e.ToTable("HistorialEstado");
            e.HasKey(h => h.HistorialEstadoId);
            e.Property(h => h.EstadoAnterior).HasMaxLength(30);
            e.Property(h => h.EstadoNuevo).HasMaxLength(30).IsRequired();
            e.Property(h => h.Comentario).HasMaxLength(1000);
            e.Property(h => h.FechaCambio).HasColumnType("datetime2(0)");
            e.HasIndex(h => new { h.SolucionId, h.FechaCambio });
            e.HasOne<Solucion>().WithMany().HasForeignKey(h => h.SolucionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificacionEnviada>(e =>
        {
            e.ToTable("NotificacionEnviada");
            e.HasKey(n => n.NotificacionId);
            e.Property(n => n.TipoNotificacion).HasMaxLength(50).IsRequired();
            e.Property(n => n.CorreoDestino).HasMaxLength(256).IsRequired();
            e.Property(n => n.Estado).HasMaxLength(20).IsRequired();
            e.Property(n => n.MensajeError).HasMaxLength(1000);
            e.Property(n => n.FechaEnvio).HasColumnType("datetime2(0)");
            e.HasOne<Solucion>().WithMany().HasForeignKey(n => n.SolucionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Adjunto>(e =>
        {
            e.ToTable("Adjunto");
            e.HasKey(a => a.AdjuntoId);
            e.Property(a => a.NombreArchivo).HasMaxLength(300).IsRequired();
            e.Property(a => a.TipoAdjunto).HasMaxLength(50).IsRequired();
            e.Property(a => a.RutaAlmacenamiento).HasMaxLength(500);
            e.Property(a => a.TipoMime).HasMaxLength(150).IsRequired();
            e.Ignore(a => a.TamanoLegible);
            e.Property(a => a.FechaCarga).HasColumnType("datetime2(0)");
            e.HasOne<Solucion>().WithMany().HasForeignKey(a => a.SolucionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(a => a.SubidoPorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdjuntoContenido>(e =>
        {
            e.ToTable("AdjuntoContenido");
            e.HasKey(c => c.AdjuntoId);
            e.Property(c => c.AdjuntoId).ValueGeneratedNever();
            e.Property(c => c.Contenido).IsRequired();
            e.HasOne<Adjunto>().WithOne().HasForeignKey<AdjuntoContenido>(c => c.AdjuntoId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
