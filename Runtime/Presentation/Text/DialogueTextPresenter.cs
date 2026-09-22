using SAS.Core.TagSystem;
using TMPro;
using UnityEngine;

namespace SAS.DialogueSystem
{
    public class DialogueTextPresenter : MonoBehaviour
    {
        [FieldRequiresSelf] private TMP_Text m_DialogueText;
        [FieldRequiresSelf] protected ITypewriterEffect _typewriterEffect;
        [FieldRequiresParent] protected DialogueHandler _dialogueHandler;
        private DialogueLineContext _presentedLine;

        protected virtual void Awake()
        {
            this.Initialize();
            if (_dialogueHandler != null)
            {
                _dialogueHandler.OnEnterDialogueMode += OnEnterDialogueMode;
                _dialogueHandler.OnExitDialogueMode += ExitDialogueMode;
                _dialogueHandler.OnSkipRequested += Skip;
                _dialogueHandler.OnLineReady += OnLineReady;
            }

            if (_typewriterEffect != null)
                _typewriterEffect.CompleteTextRevealed += HandleCompleteTextRevealed;
        }

        protected virtual void OnDestroy()
        {
            if (_dialogueHandler != null)
            {
                _dialogueHandler.OnEnterDialogueMode -= OnEnterDialogueMode;
                _dialogueHandler.OnExitDialogueMode -= ExitDialogueMode;
                _dialogueHandler.OnSkipRequested -= Skip;
                _dialogueHandler.OnLineReady -= OnLineReady;
            }

            if (_typewriterEffect != null)
                _typewriterEffect.CompleteTextRevealed -= HandleCompleteTextRevealed;
        }

        private void HandleCompleteTextRevealed()
        {
            if (_dialogueHandler == null || _presentedLine == null)
                return;

            var completedLine = _presentedLine;
            _presentedLine = null;
            _dialogueHandler.CompleteLinePresentation(completedLine);
        }

        private void Skip()
        {
            _typewriterEffect?.Skip();
        }

        protected virtual void OnEnterDialogueMode()
        {
        }

        protected virtual void ExitDialogueMode()
        {
            _presentedLine = null;
            _typewriterEffect?.Cancel();

            if (m_DialogueText != null)
                m_DialogueText.text = "";

            (_typewriterEffect as ITypewriterAudioEffect)?.SetDefaultAudioInfo();
        }

        protected virtual void OnLineReady(DialogueLineContext lineContext)
        {
            if (lineContext == null)
                return;

            ApplyLineAudio(lineContext);
            StartLinePresentation(lineContext, lineContext.Text);
        }

        protected void ApplyLineAudio(DialogueLineContext lineContext)
        {
            var audioEffect = _typewriterEffect as ITypewriterAudioEffect;
            if (!string.IsNullOrEmpty(lineContext.AudioInfoId))
                audioEffect?.SetCurrentAudioInfo(lineContext.AudioInfoId);
            else
                audioEffect?.SetDefaultAudioInfo();
        }

        protected void StartLinePresentation(DialogueLineContext lineContext, string textToDisplay)
        {
            if (lineContext == null)
                return;

            _presentedLine = lineContext;
            if (_typewriterEffect != null)
            {
                _typewriterEffect.StartTyping(textToDisplay ?? string.Empty);
                return;
            }

            HandleCompleteTextRevealed();
        }
    }
}