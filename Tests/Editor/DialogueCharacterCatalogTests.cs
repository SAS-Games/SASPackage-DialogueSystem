using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SAS.DialogueSystem.Tests
{
    public class DialogueCharacterCatalogTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _objectsToDestroy)
                Object.DestroyImmediate(value);
            _objectsToDestroy.Clear();
        }

        [Test]
        public void ResolvesCatalogDefaultsAndMetadataOverrides()
        {
            var defaultPortrait = CreateSprite();
            var happyPortrait = CreateSprite();
            var catalog = CreateCatalog(new DialogueCharacterDefinition(
                "guide",
                "Village Guide",
                defaultPortrait,
                "Idle",
                new[] { new DialoguePortraitDefinition("happy", happyPortrait) }));

            Assert.AreEqual("Village Guide", catalog.ResolveDisplayName("guide"));
            Assert.AreEqual("Hidden Stranger", catalog.ResolveDisplayName("guide", "Hidden Stranger"));
            Assert.AreSame(defaultPortrait, catalog.ResolvePortrait("guide"));
            Assert.AreSame(happyPortrait, catalog.ResolvePortrait("guide", "happy"));
            Assert.AreSame(defaultPortrait, catalog.ResolvePortrait("guide", "unknown"));
            Assert.AreEqual("Idle", catalog.ResolveAnimation("guide"));
            Assert.AreEqual("Talk", catalog.ResolveAnimation("guide", "Talk"));
        }

        [Test]
        public void FallsBackToCharacterIdWhenCatalogEntryIsMissing()
        {
            var catalog = CreateCatalog();

            Assert.AreEqual("unknown_character", catalog.ResolveDisplayName("unknown_character"));
            Assert.IsNull(catalog.ResolvePortrait("unknown_character"));
            Assert.AreEqual(string.Empty, catalog.ResolveAnimation("unknown_character"));
        }

        private DialogueCharacterCatalog CreateCatalog(params DialogueCharacterDefinition[] characters)
        {
            var catalog = ScriptableObject.CreateInstance<DialogueCharacterCatalog>();
            _objectsToDestroy.Add(catalog);
            typeof(DialogueCharacterCatalog)
                .GetField("m_Characters", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(catalog, new List<DialogueCharacterDefinition>(characters));
            typeof(DialogueCharacterCatalog)
                .GetMethod("RebuildLookups", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(catalog, null);
            return catalog;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _objectsToDestroy.Add(sprite);
            _objectsToDestroy.Add(texture);
            return sprite;
        }
    }
}
