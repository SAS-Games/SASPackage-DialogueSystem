using System.Collections;
using System.Collections.Generic;
using SAS.Core.TagSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SAS.DialogueSystem
{
    [RequireComponent(typeof(DialogueChoiceController))]
    public class ChoicePresenter : MonoBehaviour
    {
        [FieldRequiresSelf] private DialogueChoiceController _choiceController;
        [FieldRequiresChild (includeInactive = true)] private ChoiceView[] _choices;
        [SerializeField] private ChoiceView m_ChoicePrefab;
        [SerializeField] private Transform m_ChoiceContainer;

        private readonly List<ChoiceView> _choiceViews = new();

        private void Awake()
        {
            this.Initialize();
            InitializeChoicePool();
        }

        private void OnEnable()
        {
            if (_choiceController == null)
                return;

            _choiceController.OnChoicesPrepared += PresentChoices;
            _choiceController.OnChoicesHidden += HideChoices;
        }

        private void OnDisable()
        {
            if (_choiceController == null)
                return;

            _choiceController.OnChoicesPrepared -= PresentChoices;
            _choiceController.OnChoicesHidden -= HideChoices;
        }

        private void PresentChoices(ChoiceContext context)
        {
            var options = context.Options;
            var choiceCount = options?.Count ?? 0;

            EnsureChoiceCapacity(choiceCount);

            int index = 0;
            for (; index < choiceCount; index++)
            {
                if (index >= _choiceViews.Count)
                    break;

                var view = _choiceViews[index];
                SetChoice(view, options[index]);
            }

            for (int i = index; i < _choiceViews.Count; i++)
                _choiceViews[i].gameObject.SetActive(false);

            if (choiceCount > 0)
                StartCoroutine(SelectFirstChoice());
        }

        protected virtual void SetChoice(ChoiceView view, ChoiceOptionContext option)
        {
            var locale = option.LineContext?.Locale;
            if (!string.IsNullOrEmpty(locale))
                view.SetLocalText(locale, option.Choice.text);
            else
                view.SetText(option.Choice.text);

            view.BindSelectedEvent(_choiceController.MakeChoice, option.ChoiceIndex);
            view.gameObject.SetActive(true);
        }

        private IEnumerator SelectFirstChoice()
        {
            if (EventSystem.current == null)
                yield break;

            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            if (_choiceViews.Count > 0 && _choiceViews[0].gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_choiceViews[0].gameObject);
        }

        private void HideChoices()
        {
            foreach (var choice in _choiceViews)
            {
                choice.ClearSelectedEvents();
                choice.gameObject.SetActive(false);
            }
        }

        private void InitializeChoicePool()
        {
            _choiceViews.Clear();

            if (_choices != null)
            {
                foreach (var choice in _choices)
                {
                    if (choice != null && !_choiceViews.Contains(choice))
                        _choiceViews.Add(choice);
                }
            }

            if (m_ChoicePrefab == null && _choiceViews.Count > 0)
                m_ChoicePrefab = _choiceViews[0];

            if (m_ChoiceContainer == null && m_ChoicePrefab != null)
                m_ChoiceContainer = m_ChoicePrefab.transform.parent;

            HideChoices();
        }

        private void EnsureChoiceCapacity(int choiceCount)
        {
            if (choiceCount <= _choiceViews.Count)
                return;

            if (m_ChoicePrefab == null)
            {
                Debug.LogError(
                    $"UI supports {_choiceViews.Count} choices but received {choiceCount}, and no choice prefab/template is assigned.");
                return;
            }

            var parent = m_ChoiceContainer != null ? m_ChoiceContainer : m_ChoicePrefab.transform.parent;
            while (_choiceViews.Count < choiceCount)
            {
                var view = Instantiate(m_ChoicePrefab, parent);
                view.name = $"{m_ChoicePrefab.name} {_choiceViews.Count + 1}";
                view.gameObject.SetActive(false);
                _choiceViews.Add(view);
            }
        }
    }
}