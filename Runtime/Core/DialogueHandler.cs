using System;
using System.Collections.Generic;
using Ink.Runtime;
using SAS.Core.TagSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.DialogueSystem
{
    public class DialogueHandler : MonoBehaviour, IDialogueHandler
    {
        [Header("Persistence")] [SerializeField]
        private TextAsset m_LoadGlobalsJSON;

        [SerializeField] private string m_GlobalsSaveKey = "INK_VARIABLES";
        [Header("Metadata")] [SerializeField] private DialogueMetadataProfile m_DefaultMetadataProfile;

        [Header("Presentation")] [SerializeField]
        private GameObject m_DialoguePanel;

        [SerializeField] private bool m_RejectInvalidMetadata = true;
        [field: SerializeField] public bool AutoContinueToNextLine { get; private set; }
        [SerializeField] private InputAction _nextInputAction;

        private DialogueSession _session;
        private DialogueGlobalVariables _dialogueGlobalVariables;
        private InkExternalMethodRegistry _inkExternalMethodRegistry;
        private GameObject _initiator;
        private IContextBinder _contextBinder;
        private bool _isExiting;

        public Story CurrentStory => _session?.Story;
        public InkExternalMethodRegistry InkExternalMethodRegistry => _inkExternalMethodRegistry;
        public DialogueSessionState State => _session?.State ?? DialogueSessionState.Idle;

        public bool DialogueIsPlaying =>
            State != DialogueSessionState.Idle &&
            State != DialogueSessionState.Exiting &&
            State != DialogueSessionState.Faulted;

        public bool CanSkipStory => _session?.CanSkipStory ?? false;
        public DialogueLineContext CurrentLineContext => _session?.CurrentLine;

        public event Action<DialogueLineContext> OnLinePresented;
        public event Action<DialogueLineContext> OnLineReady;
        public event Action<DialogueSessionState> OnStateChanged;
        public event Action OnEnterDialogueMode;
        public event Action OnExitDialogueMode;
        public event Action OnSkipRequested;
        public event Action<bool> OnStorySkipAvailabilityChanged;

        private void Awake()
        {
            this.Initialize();
            if (_nextInputAction != null)
                _nextInputAction.performed += OnNextInputPerformed;

            if (m_LoadGlobalsJSON != null)
                _dialogueGlobalVariables = new DialogueGlobalVariables(m_LoadGlobalsJSON, m_GlobalsSaveKey);
            else
                Debug.LogWarning("Dialogue globals JSON is not assigned.", this);

            _inkExternalMethodRegistry = new InkExternalMethodRegistry();
        }

        private void OnEnable() => _nextInputAction?.Enable();
        private void OnDisable() => _nextInputAction?.Disable();

        private void Start()
        {
            if (m_DialoguePanel != null)
                m_DialoguePanel.SetActive(false);
        }

        public void EnterDialogueMode(TextAsset inkJSON, GameObject initiator, DialogueMetadataProfile metadataProfile = null)
        {
            if (State != DialogueSessionState.Idle)
            {
                Debug.LogWarning($"Cannot start dialogue while the session is {State}.", this);
                return;
            }

            if (inkJSON == null)
            {
                Debug.LogError("Cannot enter dialogue mode because Ink JSON is not assigned.", this);
                return;
            }

            DialogueMetadataSchema metadataSchema;
            try
            {
                var selectedProfile = metadataProfile != null ? metadataProfile : m_DefaultMetadataProfile;
                if (selectedProfile == null)
                {
                    Debug.LogError("Cannot enter dialogue mode because no metadata profile is assigned to the story or handler.",this);
                    return;
                }

                metadataSchema = selectedProfile.GetSchema();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cannot enter dialogue mode because its metadata profile is invalid: {ex.Message}", this);
                return;
            }

            Story story;
            try
            {
                story = new Story(inkJSON.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create Ink story from '{inkJSON.name}': {ex}", this);
                return;
            }

            _session = new DialogueSession(story, metadataSchema);
            _session.StateChanged += HandleSessionStateChanged;
            _initiator = initiator;
            OnStateChanged?.Invoke(DialogueSessionState.Starting);
            OnStorySkipAvailabilityChanged?.Invoke(false);

            if (m_DialoguePanel != null)
                m_DialoguePanel.SetActive(true);

            try
            {
                _dialogueGlobalVariables?.StartListening(story);
                _inkExternalMethodRegistry.Bind(story);
            }
            catch (Exception ex)
            {
                FailAndExit($"Failed to initialize dialogue '{inkJSON.name}'.", ex);
                return;
            }

            EventBus<DialogueStartEvent>.Raise(new DialogueStartEvent
            {
                dialogueHandler = this,
                initiator = initiator
            });
            OnEnterDialogueMode?.Invoke();
            ContinueStory();
        }

        private void ExitDialogueMode(bool notify = true)
        {
            if (_session == null || _isExiting)
                return;

            _isExiting = true;
            var session = _session;
            var story = session.Story;
            var initiator = _initiator;

            try
            {
                session.BeginExit();
                _dialogueGlobalVariables?.StopListening(story);
                _inkExternalMethodRegistry?.Unbind(story);

                if (notify)
                {
                    OnExitDialogueMode?.Invoke();
                    EventBus<DialogueEndEvent>.Raise(new DialogueEndEvent
                    {
                        dialogueHandler = this,
                        initiator = initiator
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Dialogue cleanup failed: {ex}", this);
            }
            finally
            {
                session.StateChanged -= HandleSessionStateChanged;
                if (ReferenceEquals(_session, session))
                    _session = null;

                _initiator = null;
                if (m_DialoguePanel != null)
                    m_DialoguePanel.SetActive(false);

                _isExiting = false;
                OnStorySkipAvailabilityChanged?.Invoke(false);
                OnStateChanged?.Invoke(DialogueSessionState.Idle);
            }
        }

        public void ContinueStory()
        {
            var session = _session;
            if (session == null)
                return;

            DialogueStep step;
            try
            {
                step = session.Continue();
            }
            catch (Exception ex)
            {
                FailAndExit("Failed to continue the Ink story.", ex);
                return;
            }

            if (!ReferenceEquals(_session, session))
                return;

            switch (step.Kind)
            {
                case DialogueStepKind.Line:
                    if (ValidateMetadata(step.Line))
                        OnLineReady?.Invoke(step.Line);
                    break;

                case DialogueStepKind.Choices:
                    // DialogueChoiceController reacts to the PresentingChoices state transition.
                    break;

                case DialogueStepKind.Completed:
                    ExitDialogueMode();
                    break;
            }
        }

        public bool ValidateMetadata(DialogueLineContext lineContext)
        {
            if (lineContext == null)
                return false;

            foreach (var diagnostic in lineContext.Diagnostics)
            {
                var key = string.IsNullOrEmpty(diagnostic.Key) ? string.Empty : $" ({diagnostic.Key})";
                var message = $"Dialogue metadata [{diagnostic.Code}]{key}: {diagnostic.Message}";
                if (diagnostic.Severity == DialogueMetadataSeverity.Error)
                    Debug.LogError(message, this);
                else
                    Debug.LogWarning(message, this);
            }

            if (!lineContext.HasErrors || !m_RejectInvalidMetadata)
                return true;

            FailAndExit("Dialogue stopped because the current line contains invalid metadata.");
            return false;
        }

        internal DialogueLineContext ParseMetadata(string text, IEnumerable<string> tags)
        {
            return _session?.ParseMetadata(text, tags);
        }

        public void CompleteLinePresentation(DialogueLineContext lineContext)
        {
            var session = _session;
            var couldSkipStory = session?.CanSkipStory ?? false;
            if (session == null || !session.CompleteLinePresentation(lineContext))
                return;

            if (couldSkipStory != session.CanSkipStory)
                OnStorySkipAvailabilityChanged?.Invoke(session.CanSkipStory);

            OnLinePresented?.Invoke(lineContext);

            if (ReferenceEquals(_session, session) &&
                AutoContinueToNextLine &&
                session.State == DialogueSessionState.WaitingForAdvance)
            {
                ContinueStory();
            }
        }

        public void MakeChoice(int choiceIndex)
        {
            var session = _session;
            if (session == null)
                return;

            try
            {
                if (!session.TryChoose(choiceIndex))
                {
                    Debug.LogWarning(
                        $"Cannot choose index {choiceIndex} while the dialogue session is {session.State}.",
                        this);
                    return;
                }
            }
            catch (Exception ex)
            {
                FailAndExit($"Failed to choose Ink choice index {choiceIndex}.", ex);
                return;
            }

            if (ReferenceEquals(_session, session))
                ContinueStory();
        }

        public void RequestAdvance()
        {
            switch (_session?.GetAdvanceAction() ?? DialogueAdvanceAction.None)
            {
                case DialogueAdvanceAction.RevealCurrentLine:
                    OnSkipRequested?.Invoke();
                    break;

                case DialogueAdvanceAction.ContinueStory:
                    ContinueStory();
                    break;
            }
        }

        public bool SkipStory()
        {
            var session = _session;
            if (session == null || !session.TrySkipStory())
                return false;

            ExitDialogueMode();
            return true;
        }

        private void HandleSessionStateChanged(DialogueSessionState state)
        {
            OnStateChanged?.Invoke(state);
        }

        private void FailAndExit(string message, Exception exception = null)
        {
            if (exception == null)
                Debug.LogError(message, this);
            else
                Debug.LogError($"{message}\n{exception}", this);

            _session?.Fault();
            ExitDialogueMode();
        }

        private void OnNextInputPerformed(InputAction.CallbackContext _) => RequestAdvance();

        private void OnApplicationQuit()
        {
            _dialogueGlobalVariables?.SaveVariables();
        }

        public void OnCreated(IContextBinder contextBinder)
        {
            _contextBinder = contextBinder;
        }

        private void OnDestroy()
        {
            if (_nextInputAction != null)
                _nextInputAction.performed -= OnNextInputPerformed;

            if (_session != null)
                ExitDialogueMode(false);

            _contextBinder?.Remove(this);
        }
    }
}