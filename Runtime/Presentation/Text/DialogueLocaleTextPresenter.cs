using System;
using UnityEngine;
using UnityEngine.Localization;

namespace SAS.DialogueSystem
{
    [DisallowMultipleComponent]
    public class DialogueLocaleTextPresenter : DialogueTextPresenter
    {
        [SerializeField] private string m_LocalizedTableName = "DialogueTextTable";
        private DialogueLineContext _pendingLocalizedLine;
        private LocalizedString _activeLocalizedString;
        private LocalizedString.ChangeHandler _localizedStringHandler;
        private int _localizationVersion;

        protected override void ExitDialogueMode()
        {
            CancelLocalization();
            base.ExitDialogueMode();
        }

        protected override void OnDestroy()
        {
            CancelLocalization();
            base.OnDestroy();
        }

        protected override void OnLineReady(DialogueLineContext lineContext)
        {
            if (lineContext == null)
                return;

            if (!string.IsNullOrEmpty(lineContext.Locale))
            {
                ApplyLineAudio(lineContext);
                BeginLocalization(lineContext);
            }
            else
            {
                CancelLocalization();
                base.OnLineReady(lineContext);
            }
        }

        private void BeginLocalization(DialogueLineContext lineContext)
        {
            CancelLocalization();
            _pendingLocalizedLine = lineContext;
            var version = _localizationVersion;
            _activeLocalizedString = new LocalizedString(m_LocalizedTableName, lineContext.Locale);
            _localizedStringHandler = localizedText => HandleLocalizedString(version, lineContext, localizedText);
            _activeLocalizedString.StringChanged += _localizedStringHandler;
        }

        private void HandleLocalizedString(int version, DialogueLineContext lineContext, string localizedText)
        {
            if (version != _localizationVersion || lineContext == null || !ReferenceEquals(_pendingLocalizedLine, lineContext) ||
                _dialogueHandler == null || _dialogueHandler.State != DialogueSessionState.PresentingLine || !ReferenceEquals(_dialogueHandler.CurrentLineContext, lineContext))
            {
                return;
            }

            CancelLocalization();
            StartLinePresentation(lineContext, localizedText);
        }

        private void CancelLocalization()
        {
            _localizationVersion++;
            _pendingLocalizedLine = null;
            if (_activeLocalizedString != null && _localizedStringHandler != null)
                _activeLocalizedString.StringChanged -= _localizedStringHandler;

            (_activeLocalizedString as IDisposable)?.Dispose();
            _activeLocalizedString = null;
            _localizedStringHandler = null;
        }
    }
}