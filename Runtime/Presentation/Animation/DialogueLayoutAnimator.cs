using SAS.Core.TagSystem;
using UnityEngine;

namespace SAS.DialogueSystem
{
    [RequireComponent(typeof(Animator))]
    public class DialogueLayoutAnimator : MonoBehaviour
    {
        [FieldRequiresSelf] private Animator m_LayoutAnimator;
        [FieldRequiresParent] private DialogueHandler _dialogueHandler;

        void Awake()
        {
            this.Initialize();
            _dialogueHandler.OnEnterDialogueMode += OnEnterDialogueMode;
            _dialogueHandler.OnLineReady += OnLineReady;
        }

        private void OnDestroy()
        {
            if (_dialogueHandler == null)
                return;

            _dialogueHandler.OnEnterDialogueMode -= OnEnterDialogueMode;
            _dialogueHandler.OnLineReady -= OnLineReady;
        }

        private void OnEnterDialogueMode()
        {
            m_LayoutAnimator?.Play("None");
        }

        private void OnLineReady(DialogueLineContext lineContext)
        {
            if (lineContext == null)
                return;

            if (m_LayoutAnimator != null && !string.IsNullOrEmpty(lineContext.LayoutAnim))
                m_LayoutAnimator.Play(lineContext.LayoutAnim);
        }
    }
}