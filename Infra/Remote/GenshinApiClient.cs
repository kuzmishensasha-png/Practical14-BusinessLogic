using System.Net.Http.Json;
using System.Text.Json;
using Practical1.Core.Catalog;

namespace Practical1.Infra.Remote;

public class GenshinApiClient : IGenshinApiClient
{
    private readonly HttpClient httpClient;
    private readonly CatalogJsonParser parser = new();

    public GenshinApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<string>> GetCharacterIdsAsync()
    {
        string json = await httpClient.GetStringAsync("characters");
        return parser.ParseIds(json);
    }

    public async Task<RemoteCharacterData?> GetCharacterAsync(string id)
    {
        string json = await httpClient.GetStringAsync(
            "characters/" + Uri.EscapeDataString(id));
        CatalogApiProfile data = parser.ParseProfile(json);
        RemoteCharacterData result = JsonSerializer.Deserialize<RemoteCharacterData>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("Character details are missing.");

        result.Name = data.Name;
        result.Description = data.Description;
        result.Vision = data.Vision;
        result.Weapon = data.Weapon;
        result.Rarity = data.Rarity;
        return result;
    }

    public async Task<IReadOnlyList<string>> GetWeaponIdsAsync()
    {
        return await httpClient.GetFromJsonAsync<List<string>>("weapons")
            ?? new List<string>();
    }

    public Task<RemoteWeaponData?> GetWeaponAsync(string id)
    {
        return httpClient.GetFromJsonAsync<RemoteWeaponData>(
            "weapons/" + Uri.EscapeDataString(id));
    }
}
