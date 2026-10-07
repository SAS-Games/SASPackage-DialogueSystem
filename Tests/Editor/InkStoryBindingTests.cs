using Ink;
using NUnit.Framework;
using UnityEngine;

namespace SAS.DialogueSystem.Tests
{
    public class InkStoryBindingTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void AttributesRegisterMethodsAndInjectCurrentVariableValues()
        {
            var story = new Compiler(@"
EXTERNAL add_to_base(value)
EXTERNAL mark_called()
VAR item_name = ""Default""
~ mark_called()
~ temp total = add_to_base(2)
{item_name}:{total}
-> END
").Compile();
            Assert.IsNotNull(story);

            _gameObject = new GameObject("Ink story binding test");
            var binding = _gameObject.AddComponent<TestInkStoryBinding>();
            var registry = new InkExternalMethodRegistry();

            binding.RegisterExternalMethods(registry);
            binding.BindVariables(story);
            registry.Bind(story);

            Assert.AreEqual("Potion:7", story.Continue().Trim());
            Assert.IsTrue(binding.WasCalled);

            registry.Unbind(story);
        }
    }

    public sealed class TestInkStoryBinding : InkStoryBinding
    {
        [InkVariable("item_name")]
        private string ItemName => "Potion";

        public bool WasCalled { get; private set; }

        [InkExternal("add_to_base")]
        private int AddToBase(int value) => 5 + value;

        [InkExternal("mark_called")]
        private void MarkCalled() => WasCalled = true;
    }
}
