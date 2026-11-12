using System;
using System.IO;
using UnityEngine;

public static class ServerConfig
{
    private const string ConfigFileName = "server_config.txt";
    private static bool isLoaded = false;

    public static string BackendUrl { get; private set; } = "https://cocangua-backend.onrender.com";
    public static string TcpHost { get; private set; } = "127.0.0.1";
    public static int TcpPort { get; private set; } = 7777;
    public static string ConfigFilePath { get; private set; } = "";

    public static void Load()
    {
        if (isLoaded) return;
        isLoaded = true;

        string[] searchPaths = new string[]
        {
            // 1. Right next to the .exe in Standalone Windows build
            Path.Combine(Application.dataPath, "..", ConfigFileName),
            // 2. In Project Root / Current Working Directory
            Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName),
            // 3. In PersistentDataPath
            Path.Combine(Application.persistentDataPath, ConfigFileName)
        };

        bool fileFound = false;
        foreach (string path in searchPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    ConfigFilePath = Path.GetFullPath(path);
                    ParseConfigFile(ConfigFilePath);
                    fileFound = true;
                    Debug.Log($"[ServerConfig] Loaded server configuration from: {ConfigFilePath}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerConfig] Error checking path {path}: {ex.Message}");
            }
        }

        // Check Environment Variables as override
        string envBackend = Environment.GetEnvironmentVariable("COCANGUA_BACKEND_URL");
        if (!string.IsNullOrWhiteSpace(envBackend)) BackendUrl = envBackend.Trim();

        string envTcp = Environment.GetEnvironmentVariable("COCANGUA_TCP_HOST");
        if (!string.IsNullOrWhiteSpace(envTcp)) TcpHost = envTcp.Trim();

        // Check PlayerPrefs override
        string prefIp = PlayerPrefs.GetString("CustomServerIP", "");
        if (!string.IsNullOrWhiteSpace(prefIp))
        {
            if (prefIp.Contains("127.0.0.1") || prefIp.Contains("localhost"))
            {
                PlayerPrefs.DeleteKey("CustomServerIP");
                PlayerPrefs.Save();
            }
            else
            {
                ApplyCustomIp(prefIp);
            }
        }

        // If no config file was found, create a template config file
        if (!fileFound)
        {
            CreateDefaultConfigFile(searchPaths[0]);
        }

        NormalizeSettings();
        Debug.Log($"[ServerConfig] Active Backend URL: {BackendUrl} | TCP Server: {TcpHost}:{TcpPort}");
    }

    private static void ParseConfigFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            int equalIdx = line.IndexOf('=');
            if (equalIdx <= 0) continue;

            string key = line.Substring(0, equalIdx).Trim().ToLowerInvariant();
            string val = line.Substring(equalIdx + 1).Trim();

            if (key == "backendurl" || key == "url" || key == "server_url" || key == "api")
            {
                if (!string.IsNullOrEmpty(val)) BackendUrl = val;
            }
            else if (key == "tcphost" || key == "tcp" || key == "host" || key == "server_ip")
            {
                if (!string.IsNullOrEmpty(val)) TcpHost = val;
            }
            else if (key == "tcpport" || key == "port")
            {
                if (int.TryParse(val, out int p)) TcpPort = p;
            }
        }
    }

    public static void ApplyCustomIp(string ipOrUrl)
    {
        if (string.IsNullOrWhiteSpace(ipOrUrl)) return;
        string cleaned = ipOrUrl.Trim();

        // If entered just an IP or hostname (e.g. 192.168.1.15)
        if (!cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            TcpHost = cleaned.Split(':')[0];
            BackendUrl = $"http://{cleaned}:5001";
        }
        else
        {
            BackendUrl = cleaned;
            try
            {
                var uri = new Uri(cleaned);
                TcpHost = uri.Host;
            }
            catch { }
        }

        NormalizeSettings();
    }

    private static void NormalizeSettings()
    {
        BackendUrl = BackendUrl.TrimEnd('/');
        if (BackendUrl.Contains(":5080")) BackendUrl = BackendUrl.Replace(":5080", ":5001");

        // Automatically infer TcpHost from BackendUrl if TcpHost is still 127.0.0.1
        if (TcpHost == "127.0.0.1" && !BackendUrl.Contains("127.0.0.1") && !BackendUrl.Contains("localhost"))
        {
            try
            {
                var uri = new Uri(BackendUrl);
                TcpHost = uri.Host;
            }
            catch { }
        }
    }

    private static void CreateDefaultConfigFile(string targetPath)
    {
        try
        {
            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string defaultContent = @"# ===================================================================
# FILE CẤU HÌNH SERVER CHO GAME 3D CỜ CÁ NGỰA
# ===================================================================
# HƯỚNG DẪN KẾT NỐI:
#
# 1. Chơi trên 1 máy tính (Localhost):
#    BackendUrl=http://127.0.0.1:5001
#    TcpHost=127.0.0.1
#
# 2. Chơi cùng mạng WiFi / LAN (Điền IP máy Host chạy Backend dotnet run):
#    Ví dụ: Máy Host có IP là 192.168.1.15:
#    BackendUrl=http://192.168.1.15:5001
#    TcpHost=192.168.1.15
#
# 3. Chơi qua ngrok / Cloudflare Tunnel / Radmin VPN:
#    BackendUrl=https://your-domain.ngrok-free.app
#    TcpHost=127.0.0.1
# ===================================================================
BackendUrl=https://cocangua-backend.onrender.com
TcpHost=127.0.0.1
TcpPort=7777
";
            File.WriteAllText(targetPath, defaultContent);
            ConfigFilePath = Path.GetFullPath(targetPath);
            Debug.Log($"[ServerConfig] Created default server_config.txt at: {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ServerConfig] Could not create template config file: {ex.Message}");
        }
    }
}
