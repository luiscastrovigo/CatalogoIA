using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.Interfaces;

public interface IPlataformaRepository
{
    IReadOnlyList<Plataforma> ObtenerTodas();
    IReadOnlyList<Plataforma> ObtenerActivas();
    Plataforma? ObtenerPorId(int plataformaId);
    Plataforma Crear(Plataforma plataforma);
    void Actualizar(Plataforma plataforma);

    /// <summary>
    /// Elimina la Plataforma. El llamador (PlataformaController) es responsable de
    /// validar antes que no existan Soluciones que la usen (ver
    /// ISolucionRepository.ExistenSolucionesConPlataforma) -- retroalimentacion de
    /// usuarios, 2026-09-08.
    /// </summary>
    void Eliminar(int plataformaId);
}
