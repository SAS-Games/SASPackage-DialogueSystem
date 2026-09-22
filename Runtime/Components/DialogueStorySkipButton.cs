using SAS.Core.TagSystem;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.DialogueSystem
{
    [RequireComponent(typeof(Button))]
    public sealed class DialogueStorySkipButton : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [Tooltip("Optional visual root to hide while story skipping is unavailable.")] [SerializeField]
        private GameObject m_VisualRoot;

        [SerializeField] private bool m_HideWhenUnavailable;
        [FieldRequiresParent] private DialogueHandler _dialogueHandler;

        private void Awake()
        {
            this.Initialize();
            if (m_Button == null)
                m_Button = GetComponent<Button>();

            m_Button?.onClick.AddListener(HandleClicked);
            if (_dialogueHandler != null)
                _dialogueHandler.OnStorySkipAvailabilityChanged += ApplyAvailability;

            ApplyAvailability(_dialogueHandler != null && _dialogueHandler.CanSkipStory);
        }

        private void OnEnable()
        {
            ApplyAvailability(_dialogueHandler != null && _dialogueHandler.CanSkipStory);
        }

        private void OnDestroy()
        {
            m_Button?.onClick.RemoveListener(HandleClicked);
            if (_dialogueHandler != null)
                _dialogueHandler.OnStorySkipAvailabilityChanged -= ApplyAvailability;
        }

        private void HandleClicked()
        {
            _dialogueHandler?.SkipStory();
        }

        private void ApplyAvailability(bool available)
        {
            if (m_Button != null)
                m_Button.interactable = available;

            if (m_VisualRoot != null && m_HideWhenUnavailable)
                m_VisualRoot.SetActive(available);
        }
    }
}