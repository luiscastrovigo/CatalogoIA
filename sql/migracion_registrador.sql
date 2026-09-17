-- =====================================================================
-- Migracion incremental: separar REGISTRADOR de OWNER
-- Proyecto: Catalogo de Soluciones Citizen Development
-- Fecha: 2026-09-14
--
-- Contexto: dbo.Solucion tenia una sola columna de persona,
-- UsuarioDuenioId, que respondia dos preguntas distintas: quien es el
-- dueno de la solucion y quien cargo el registro. Mientras ambas cosas
-- fueron la misma persona no molestaba; desde que un Administrador puede
-- registrar a nombre de otro, el modelo quedo ambiguo.
--
-- A partir de aqui:
--   UsuarioDuenioId  = OWNER de la solucion. Editable por Administrador.
--   RegistradoPorId  = quien cargo el registro. Hecho historico, NO editable.
--
-- ORDEN IMPORTANTE: el relleno de abajo (RegistradoPorId = UsuarioDuenioId)
-- es exacto SOLO mientras nadie haya reasignado el owner de un registro.
-- Por eso esta migracion debe correrse ANTES de publicar o usar la
-- reasignacion de owner. Si se corre despues, los registros reasignados
-- quedarian con el registrador equivocado y sin forma de recuperarlo.
--
-- Idempotente: se puede correr varias veces sin efecto adicional.
-- =====================================================================

SET NOCOUNT ON;
GO

-- 1. Columna nueva, primero anulable para poder rellenar.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Solucion') AND name = N'RegistradoPorId')
BEGIN
    ALTER TABLE dbo.Solucion ADD RegistradoPorId INT NULL;
    PRINT 'dbo.Solucion.RegistradoPorId agregada (anulable).';
END
GO

-- 2. Relleno historico: hasta hoy, quien registro y el dueno son la misma persona.
UPDATE dbo.Solucion
   SET RegistradoPorId = UsuarioDuenioId
 WHERE RegistradoPorId IS NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.Solucion WHERE RegistradoPorId IS NULL)
BEGIN
    RAISERROR('Quedan filas con RegistradoPorId NULL. Revisar antes de continuar.', 16, 1);
END
GO

-- 3. Ahora si, obligatoria.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Solucion')
             AND name = N'RegistradoPorId'
             AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.Solucion ALTER COLUMN RegistradoPorId INT NOT NULL;
    PRINT 'dbo.Solucion.RegistradoPorId ahora es NOT NULL.';
END
GO

-- 4. Integridad referencial contra dbo.Usuario.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Solucion_RegistradoPor')
BEGIN
    ALTER TABLE dbo.Solucion
        ADD CONSTRAINT FK_Solucion_RegistradoPor FOREIGN KEY (RegistradoPorId)
            REFERENCES dbo.Usuario (UsuarioId);
    PRINT 'FK_Solucion_RegistradoPor creada.';
END
GO

-- 5. Indice para consultar "que registro cada persona".
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.Solucion') AND name = N'IX_Solucion_RegistradoPor')
BEGIN
    CREATE INDEX IX_Solucion_RegistradoPor ON dbo.Solucion (RegistradoPorId);
    PRINT 'IX_Solucion_RegistradoPor creado.';
END
GO

PRINT 'Migracion de registrador/owner completada.';
GO
