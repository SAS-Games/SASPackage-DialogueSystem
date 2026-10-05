using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[Serializable]
public sealed class DialoguePortraitDefinition
{
    [SerializeField] private string m_Key;
    [SerializeField] private Sprite m_Sprite;

    public DialoguePortraitDefinition(string key, Sprite sprite)
    {
        m_Key = key;
        m_Sprite = sprite;
    }

    public string Key => m_Key?.Trim() ?? string.Empty;
    public Sprite Sprite => m_Sprite;
}

[Serializable]
public sealed class DialogueCharacterDefinition
{
    [SerializeField] private string m_Id;
    [SerializeField] private string m_DisplayName;
    [SerializeField] private LocalizedString m_LocalizedDisplayName;
    [SerializeField] private Sprite m_DefaultPortrait;
    [SerializeField] private string m_DefaultAnimationState = "Idle";
    [SerializeField] private List<DialoguePortraitDefinition> m_Portraits = new();

    private Dictionary<string, Sprite> _portraitsByKey;

    public DialogueCharacterDefinition(string id, string displayName = null, Sprite defaultPortrait = null, string defaultAnimationState = null, IEnumerable<DialoguePortraitDefinition> portraits = null)
    {
        m_Id = id;
        m_DisplayName = displayName;
        m_DefaultPortrait = defaultPortrait;
        m_DefaultAnimationState = defaultAnimationState;
        m_Portraits = portraits != null ? new List<DialoguePortraitDefinition>(portraits) : new List<DialoguePortraitDefinition>();
    }

    public string Id => m_Id?.Trim() ?? string.Empty;
    public string DisplayName => m_DisplayName?.Trim() ?? string.Empty;
    public LocalizedString LocalizedDisplayName => m_LocalizedDisplayName;
    public bool HasLocalizedDisplayName => m_LocalizedDisplayName != null && !m_LocalizedDisplayName.IsEmpty;
    public Sprite DefaultPortrait => m_DefaultPortrait;
    public string DefaultAnimationState => m_DefaultAnimationState?.Trim() ?? string.Empty;
    public IReadOnlyList<DialoguePortraitDefinition> Portraits => m_Portraits;

    public bool TryGetPortrait(string key, out Sprite sprite)
    {
        EnsurePortraitLookup();
        if (!string.IsNullOrWhiteSpace(key))
            return _portraitsByKey.TryGetValue(key.Trim(), out sprite);

        sprite = null;
        return false;
    }

    internal void RebuildLookup(Action<string> reportWarning = null)
    {
        _portraitsByKey = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var portrait in m_Portraits ?? new List<DialoguePortraitDefinition>())
        {
            if (portrait == null || string.IsNullOrWhiteSpace(portrait.Key))
                continue;

            if (_portraitsByKey.ContainsKey(portrait.Key))
                reportWarning?.Invoke($"Character '{Id}' contains duplicate portrait key '{portrait.Key}'. The final entry is used.");
            _portraitsByKey[portrait.Key] = portrait.Sprite;
        }
    }

    private void EnsurePortraitLookup()
    {
        if (_portraitsByKey == null)
            RebuildLookup();
    }
}

[CreateAssetMenu(fileName = "Dialogue Character Catalog", menuName = "Dialogue/Character Catalog")]
public class DialogueCharacterCatalog : ScriptableObject
{
    [SerializeField] private List<DialogueCharacterDefinition> m_Characters = new();

    private Dictionary<string, DialogueCharacterDefinition> _charactersById;

    public IReadOnlyList<DialogueCharacterDefinition> Characters => m_Characters;

    public bool TryGetCharacter(string characterId, out DialogueCharacterDefinition character)
    {
        EnsureLookups();
        if (!string.IsNullOrWhiteSpace(characterId))
            return _charactersById.TryGetValue(characterId.Trim(), out character);

        character = null;
        return false;
    }

    public string ResolveDisplayName(string characterId, string metadataOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(metadataOverride))
            return metadataOverride.Trim();

        return TryGetCharacter(characterId, out var character) && !string.IsNullOrEmpty(character.DisplayName)
            ? character.DisplayName
            : characterId?.Trim() ?? string.Empty;
    }

    public Sprite ResolvePortrait(string characterId, string portraitKey = null)
    {
        if (TryGetCharacter(characterId, out var character))
        {
            if (!string.IsNullOrWhiteSpace(portraitKey) && character.TryGetPortrait(portraitKey, out var portrait))
                return portrait;
            if (character.DefaultPortrait != null)
                return character.DefaultPortrait;
        }

        return null;
    }

    public string ResolveAnimation(string characterId, string metadataOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(metadataOverride))
            return metadataOverride.Trim();

        return TryGetCharacter(characterId, out var character) ? character.DefaultAnimationState : string.Empty;
    }

    private void OnEnable() => RebuildLookups();
    private void OnValidate() => RebuildLookups();

    private void EnsureLookups()
    {
        if (_charactersById == null)
            RebuildLookups();
    }

    private void RebuildLookups()
    {
        _charactersById = new Dictionary<string, DialogueCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        var characters = m_Characters ?? new List<DialogueCharacterDefinition>();
        for (var index = 0; index < characters.Count; index++)
        {
            var character = characters[index];
            if (character == null)
            {
                Debug.LogWarning($"Dialogue character entry {index} is empty.", this);
                continue;
            }
            if (string.IsNullOrWhiteSpace(character.Id))
            {
                Debug.LogWarning($"Dialogue character entry {index} requires an ID.", this);
                continue;
            }
            if (!IsValidCharacterId(character.Id))
            {
                Debug.LogWarning($"Dialogue character ID '{character.Id}' must start with a letter or number and contain only letters, numbers, dots, hyphens, or underscores.", this);
                continue;
            }

            character.RebuildLookup(message => Debug.LogWarning(message, this));
            if (_charactersById.ContainsKey(character.Id))
                Debug.LogWarning($"Dialogue character ID '{character.Id}' is configured more than once. The final entry is used.", this);
            _charactersById[character.Id] = character;
        }
    }

    private static bool IsValidCharacterId(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId) && char.IsLetterOrDigit(characterId[0]) &&
               characterId.Trim().Length == characterId.Length &&
               Array.TrueForAll(characterId.ToCharArray(), character =>
                   char.IsLetterOrDigit(character) || character == '.' || character == '-' || character == '_');
    }
}
