using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace FastMCLauncher.Services
{
    public interface IVersionService
    {
        Task<List<string>> GetMinecraftVersionsAsync();
        Task<List<string>> GetModLoaderVersionsAsync(string minecraftVersion, string modLoaderType);
    }

    public class VersionService : IVersionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public VersionService(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<string>> GetMinecraftVersionsAsync()
        {
            try
            {
                _logger.Information("Fetching Minecraft versions from https://meta.multimc.org/v1/net.minecraft/");
                var response = await _httpClient.GetAsync("https://meta.multimc.org/v1/net.minecraft/");
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                _logger.Debug("Received JSON: {Json}", jsonString.Substring(0, Math.Min(jsonString.Length, 100)));

                var json = JsonConvert.DeserializeObject<JObject>(jsonString);
                var versions = json["versions"]
                    .Where(v => v["type"].ToString() == "release")
                    .Select(v => new
                    {
                        Version = v["version"].ToString(),
                        ReleaseTime = DateTime.Parse(v["releaseTime"].ToString())
                    })
                    .OrderByDescending(v => v.ReleaseTime)
                    .Select(v => v.Version)
                    .ToList();

                if (versions.Contains("1.9.4"))
                {
                    _logger.Warning("Version 1.9.4 found in fetched versions, which may be due to LiteLoader compatibility");
                }

                _logger.Information("Loaded {Count} Minecraft release versions: {Versions}", versions.Count, string.Join(", ", versions));
                return versions;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed while fetching Minecraft versions");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse JSON for Minecraft versions");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while fetching Minecraft versions");
                throw;
            }
        }

        public async Task<List<string>> GetModLoaderVersionsAsync(string minecraftVersion, string modLoaderType)
        {
            if (string.IsNullOrEmpty(minecraftVersion) || string.IsNullOrEmpty(modLoaderType))
            {
                _logger.Information("Skipping mod loader versions fetch due to empty inputs");
                return new List<string>();
            }

            try
            {
                string url = modLoaderType switch
                {
                    "Forge" => "https://meta.multimc.org/v1/net.minecraftforge/",
                    "Fabric" => "https://meta.multimc.org/v1/net.fabricmc.fabric-loader/",
                    "Quilt" => "https://meta.multimc.org/v1/org.quiltmc.quilt-loader/",
                    "LiteLoader" => "https://meta.multimc.org/v1/com.mumfrey.liteloader/",
                    _ => throw new InvalidOperationException("Invalid mod loader type")
                };

                _logger.Information("Fetching {ModLoader} versions from {Url}", modLoaderType, url);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                _logger.Debug("Received JSON: {Json}", jsonString.Substring(0, Math.Min(jsonString.Length, 100)));

                var json = JsonConvert.DeserializeObject<JObject>(jsonString);
                var versions = json["versions"]
                    .Where(v => modLoaderType == "Forge" || modLoaderType == "LiteLoader"
                        ? v["requires"]?.Any(r => r["uid"].ToString() == "net.minecraft" && r["equals"].ToString() == minecraftVersion) == true
                        : v["type"].ToString() == "release")
                    .Select(v => new
                    {
                        Version = v["version"].ToString(),
                        ReleaseTime = DateTime.Parse(v["releaseTime"].ToString())
                    })
                    .OrderByDescending(v => v.ReleaseTime)
                    .Select(v => v.Version)
                    .ToList();

                _logger.Information("Loaded {Count} {ModLoader} versions for Minecraft {Version}: {Versions}", versions.Count, modLoaderType, minecraftVersion, string.Join(", ", versions));
                return versions;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed while fetching {ModLoader} versions", modLoaderType);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse JSON for {ModLoader} versions");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while fetching {ModLoader} versions", modLoaderType);
                throw;
            }
        }
    }
}