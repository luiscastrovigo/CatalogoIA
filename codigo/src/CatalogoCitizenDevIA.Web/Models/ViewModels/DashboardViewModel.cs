namespace CatalogoCitizenDevIA.Web.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalSoluciones { get; set; }
    public Dictionary<string, int> PorNivelRiesgo { get; set; } = new();
    public Dictionary<string, int> PorEstado { get; set; } = new();
    public Dictionary<string, int> PorPais { get; set; } = new();
    public Dictionary<string, int> PorArea { get; set; } = new();
    public Dictionary<string, int> PorTipo { get; set; } = new();
    public Dictionary<string, int> PorPlataforma { get; set; } = new();
    public int ProximasARevision30Dias { get; set; }
}
