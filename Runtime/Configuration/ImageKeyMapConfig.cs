using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ImageKeyMapConfig")]
public class ImageKeyMapConfig : ScriptableObject
{
    [Serializable]
    class ImageKeyMap
    {
        public string key;
        public Sprite value;
    }

    [SerializeField] private ImageKeyMap[] m_ImageKeyMap;
    private Dictionary<string, Sprite> _imagesByKey;

    public Sprite GetImage(string key)
    {
        if (TryGetImage(key, out var sprite))
            return sprite;

        Debug.LogWarning($"No image entry found for key '{key}'.", this);
        return null;
    }

    public bool TryGetImage(string key, out Sprite sprite)
    {
        sprite = null;
        EnsureLookup();
        return !string.IsNullOrWhiteSpace(key) && _imagesByKey.TryGetValue(key.Trim(), out sprite);
    }

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void EnsureLookup()
    {
        if (_imagesByKey == null)
            RebuildLookup();
    }

    private void RebuildLookup()
    {
        _imagesByKey = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in m_ImageKeyMap ?? Array.Empty<ImageKeyMap>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            _imagesByKey[entry.key.Trim()] = entry.value;
        }
    }
}