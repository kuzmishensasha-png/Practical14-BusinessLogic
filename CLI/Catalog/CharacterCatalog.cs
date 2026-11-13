using Practical1.Core.Catalog;
using Practical1.Infra.Data;
using Practical1.Infra.Remote;

namespace Practical1.CLI.Catalog;

public class CharacterCatalog
{
    private readonly IGenshinApiClient api;
    private readonly IDataStorage storage;
    private readonly List<CatalogCharacter> characters = new();

    public IReadOnlyList<CatalogCharacter> Characters => characters.AsReadOnly();

    public CharacterCatalog(IGenshinApiClient api, IDataStorage storage)
    {
        this.api = api;
        this.storage = storage;
    }

    public async Task<int> UpdateAsync()
    {
        IReadOnlyList<string> remoteIds = await api.GetCharacterIdsAsync();
        HashSet<string> ids = remoteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ids.Count == 0)
        {
            throw new HttpRequestException("API returned an empty character list.");
        }

        characters.RemoveAll(character =>
            !character.IsCustom && !ids.Contains(character.Id));

        foreach (string id in ids.OrderBy(id => id))
        {
            if (characters.Any(character =>
                string.Equals(character.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            characters.Add(new CatalogCharacter
            {
                Id = id,
                Name = id,
                ImageUrl = CatalogBusinessRules.RemoteImageUrl(id)
            });
        }

        return ids.Count;
    }

    public async Task<CatalogCharacter?> FindAsync(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        string query = idOrName.Trim();
        CatalogCharacter? character = characters.FirstOrDefault(item =>
            string.Equals(item.Id, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, query, StringComparison.OrdinalIgnoreCase));

        if (character == null || character.IsCustom || character.DetailsLoaded)
        {
            return character;
        }

        RemoteCharacterData? data = await api.GetCharacterAsync(character.Id);
        if (data != null)
        {
            character.Name = string.IsNullOrWhiteSpace(data.Name)
                ? character.Id : data.Name.Trim();
            character.Description = data.Description ?? "";
            character.Vision = data.Vision ?? "";
            character.Weapon = data.Weapon ?? "";
            character.Rarity = data.Rarity;
            character.DetailsLoaded = true;
        }

        return character;
    }

    public IReadOnlyList<CatalogCard> GetCards(string? element = null)
    {
        IEnumerable<CatalogCharacter> items = characters;
        if (!string.IsNullOrWhiteSpace(element))
        {
            items = items.Where(item =>
                string.Equals(item.Vision, element.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        return items.OrderBy(item => item.Name)
            .Select(item => new CatalogCard(
                item.Id, item.Name, item.Vision, item.Weapon,
                item.Rarity, item.ImageUrl, item.IsCustom))
            .ToList();
    }

    public async Task<CatalogDetails?> GetDetailsAsync(string idOrName)
    {
        CatalogCharacter? item = await FindAsync(idOrName);
        if (item == null)
        {
            return null;
        }

        return new CatalogDetails(item.Id, item.Name, item.Description,
            item.Vision, item.Weapon, item.Rarity, item.ImageUrl, item.IsCustom);
    }

    public CatalogCharacter CreateCustom(
        string name,
        string description,
        string vision,
        string weapon,
        int rarity,
        string imageUrl)
    {
        name = CatalogBusinessRules.RequiredText(name, "Name");
        description = CatalogBusinessRules.RequiredText(description, "Description");
        vision = CatalogBusinessRules.RequiredText(vision, "Element");
        weapon = CatalogBusinessRules.RequiredText(weapon, "Weapon");
        rarity = CatalogBusinessRules.ValidRarity(rarity);
        imageUrl = CatalogBusinessRules.ValidImageUrl(imageUrl);

        if (characters.Any(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A character with this name already exists.");
        }

        CatalogCharacter character = new()
        {
            Id = "user-" + Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Description = description,
            Vision = vision,
            Weapon = weapon,
            Rarity = rarity,
            ImageUrl = imageUrl,
            IsCustom = true,
            DetailsLoaded = true
        };

        characters.Add(character);
        return character;
    }

    public void Save(string fileName)
    {
        storage.Save(fileName, new CatalogData
        {
            Characters = characters.ToList()
        });
    }

    public bool Load(string fileName)
    {
        CatalogData? data = storage.Load<CatalogData>(fileName);
        if (data == null)
        {
            return false;
        }

        List<CatalogCharacter> loaded = (data.Characters ?? new())
            .Where(item => item != null
                && !string.IsNullOrWhiteSpace(item.Id)
                && !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        characters.Clear();
        characters.AddRange(loaded);
        return true;
    }
}
