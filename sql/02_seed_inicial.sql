-- =========================================================
-- YanbalCitizenDevIA — Datos semilla mínimos
-- Ejecutar después de 01_schema_YanbalCitizenDevIA.sql
-- =========================================================
USE YanbalCitizenDevIA;
GO

-- Usuario administrador inicial, para poder operar el módulo
-- de administración de plataformas desde el primer día.
-- IMPORTANTE: reemplazar el AzureAdObjectId de ejemplo (NEWID())
-- por el Object ID real del usuario en Azure AD / Entra ID antes
-- de ejecutar en UAT o Producción.
INSERT INTO dbo.Usuario (AzureAdObjectId, CorreoCorporativo, NombreCompleto, EsAdministrador)
VALUES (NEWID(), N'luis.castro@yanbal.com', N'Luis Castro', 1);
GO

-- La tabla Plataforma NO se precarga aquí: el catálogo real de
-- plataformas de IA aprobadas está pendiente de definición por
-- el Comité de Citizen Development (ver 05-documento-cero-prerequisitos.md,
-- sección 4). Cuando el Comité entregue el listado oficial, agregar
-- aquí un INSERT por plataforma aprobada, por ejemplo:
--
-- INSERT INTO dbo.Plataforma (Nombre, Categoria, Estado, CreadoPorId)
-- VALUES (N'Microsoft 365 Copilot', N'Transversal', N'Aprobada', 1);
GO
