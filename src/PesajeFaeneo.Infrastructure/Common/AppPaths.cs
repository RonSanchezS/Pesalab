namespace PesajeFaeneo.Infrastructure.Common;

public class AppPaths
{
    public string BaseDir { get; }
    public string LogsDir { get; }
    public string LogoDir { get; }
    public string BackupsDir { get; }
    public string RestoreDir { get; }
    public string DatabasePath { get; }
    public string ConnectionString { get; }
    public string ConfigDir { get; }

    public AppPaths(string? baseDir = null)
    {
        BaseDir = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PesajeFaeneo");

        LogsDir = Path.Combine(BaseDir, "logs");
        LogoDir = Path.Combine(BaseDir, "logo");
        ConfigDir = Path.Combine(BaseDir, "config");
        BackupsDir = Path.Combine(BaseDir, "backups");
        RestoreDir = Path.Combine(BaseDir, "restore");
        DatabasePath = Path.Combine(BaseDir, "pesaje.db");
        ConnectionString = $"Data Source={DatabasePath}";
    }

    public void AsegurarDirectorios()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(LogoDir);
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(RestoreDir);
    }
}
