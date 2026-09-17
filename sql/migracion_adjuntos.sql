-- =====================================================================
-- Migracion incremental: adjuntos almacenados DENTRO de SQL Server
-- Proyecto: Catalogo de Soluciones Citizen Development
-- Fecha: 2026-09-10
--
-- Contexto: dbo.Adjunto ya existia en el diseno original
-- (06-diseno-bd-sql-server.md) pensada para guardar el archivo en disco
-- y solo la RUTA en la base. Por decision del Arquitecto de Aplicacion
-- (2026-09-10) el contenido pasa a guardarse dentro de SQL Server.
--
-- El binario NO va en dbo.Adjunto sino en una tabla aparte 1 a 1:
-- asi cualquier consulta que solo necesite el listado de adjuntos
-- (nombre, tipo, tamano, quien lo subio) nunca arrastra megabytes de
-- contenido. Es la diferencia entre abrir el detalle de una solucion en
-- milisegundos o en segundos.
--
-- Idempotente: se puede correr varias veces sin efecto adicional.
-- =====================================================================

SET NOCOUNT ON;
GO

-- 1. RutaAlmacenamiento deja de usarse (queda para trazabilidad del
--    diseno original). Pasa a admitir NULL porque ya no hay ruta.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Adjunto')
             AND name = N'RutaAlmacenamiento'
             AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.Adjunto ALTER COLUMN RutaAlmacenamiento NVARCHAR(500) NULL;
    PRINT 'dbo.Adjunto.RutaAlmacenamiento ahora admite NULL.';
END
GO

-- 2. Metadatos nuevos del archivo.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Adjunto') AND name = N'TipoMime')
BEGIN
    ALTER TABLE dbo.Adjunto
        ADD TipoMime NVARCHAR(150) NOT NULL
            CONSTRAINT DF_Adjunto_TipoMime DEFAULT (N'application/octet-stream');
    PRINT 'dbo.Adjunto.TipoMime agregada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Adjunto') AND name = N'TamanoBytes')
BEGIN
    ALTER TABLE dbo.Adjunto
        ADD TamanoBytes INT NOT NULL
            CONSTRAINT DF_Adjunto_TamanoBytes DEFAULT (0);
    PRINT 'dbo.Adjunto.TamanoBytes agregada.';
END
GO

-- 3. Contenido binario, en tabla separada 1 a 1 con dbo.Adjunto.
IF OBJECT_ID(N'dbo.AdjuntoContenido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdjuntoContenido (
        AdjuntoId  INT             NOT NULL,
        Contenido  VARBINARY(MAX)  NOT NULL,
        CONSTRAINT PK_AdjuntoContenido PRIMARY KEY CLUSTERED (AdjuntoId),
        CONSTRAINT FK_AdjuntoContenido_Adjunto FOREIGN KEY (AdjuntoId)
            REFERENCES dbo.Adjunto (AdjuntoId)
    );
    PRINT 'dbo.AdjuntoContenido creada.';
END
GO

-- 4. Indice para listar los adjuntos de una solucion.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.Adjunto') AND name = N'IX_Adjunto_Solucion')
BEGIN
    CREATE INDEX IX_Adjunto_Solucion ON dbo.Adjunto (SolucionId);
    PRINT 'IX_Adjunto_Solucion creado.';
END
GO

PRINT 'Migracion de adjuntos completada.';
GO
