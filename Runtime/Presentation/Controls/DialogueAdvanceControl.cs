using SAS.Core.TagSystem;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.DialogueSystem
{
    public class DialogueAdvanceControl : MonoBehaviour
    {
        [Tooltip("Visual displayed when the current line is ready to advance.")] [SerializeField]
        private GameObject m_ReadyIndicator;

        [Tooltip("Optional button. When assigned, clicking reveals the current line or advances dialogue.")]
        [SerializeField] private Button m_Button;

        [Tooltip("Allow the optional button to reveal a line that is still being presented.")] [SerializeField]
        private bool m_CanRevealCurrentLine = true;

        [FieldRequiresParent] private DialogueHandler _dialogueHandler;

        private void Awake()
        {
            this.Initialize();
            if (m_Button == null)
                TryGetComponent(out m_Button);
        }

        private void OnEnable()
        {
            m_Button?.onClick.AddListener(HandleClicked);
            if (_dialogueHandler != null)
                _dialogueHandler.OnStateChanged += HandleStateChanged;

            ApplyState(_dialogueHandler != null ? _dialogueHandler.State : DialogueSessionState.Idle);
        }

        private void OnDisable()
        {
            m_Button?.onClick.RemoveListener(HandleClicked);
            if (_dialogueHandler != null)
                _dialogueHandler.OnStateChanged -= HandleStateChanged;

            ApplyState(DialogueSessionState.Idle);
        }

        private void HandleClicked()
        {
            _dialogueHandler?.RequestAdvance();
        }

        private void HandleStateChanged(DialogueSessionState state) => ApplyState(state);

        private void ApplyState(DialogueSessionState state)
        {
            var isWaitingForAdvance = state == DialogueSessionState.WaitingForAdvance;
            var canRevealCurrentLine = m_CanRevealCurrentLine && state == DialogueSessionState.PresentingLine;

            if (m_ReadyIndicator != null)
                m_ReadyIndicator.SetActive(isWaitingForAdvance);

            if (m_Button != null)
                m_Button.interactable = isWaitingForAdvance || canRevealCurrentLine;
        }
    }
}