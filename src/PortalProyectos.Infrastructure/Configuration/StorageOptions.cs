namespace PortalProyectos.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootPath { get; set; } = "../../storage";
    public long MaxProjectPdfBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxNewsVideoBytes { get; set; } = 100 * 1024 * 1024;
}
