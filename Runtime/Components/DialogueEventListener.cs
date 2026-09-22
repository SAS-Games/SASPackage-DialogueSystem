using Ink.Runtime;
using SAS.Core.TagSystem;
using SAS.DialogueSystem;
using UnityEngine;
using UnityEngine.Events;

public class DialogueEventListener : MonoBehaviour
{
    [Inject] protected IDialogueHandler _dialogueHandler;
    [SerializeField] private UnityEvent<DialogueHandler> m_OnDialogueStart;
    [SerializeField] private UnityEvent<DialogueHandler> m_OnDialogueEnd;
    [SerializeField] private UnityEvent<string> m_OnDialogueTextRevealed;

    private EventBinding<DialogueStartEvent> _dialogueStartEventBinding;
    private EventBinding<DialogueEndEvent> _dialogueEndEventBinding;
    private DialogueHandler _subscribedDialogueHandler;
    protected Story CurrentStory => (_dialogueHandler as DialogueHandler)?.CurrentStory;

    protected virtual void Awake()
    {
        _dialogueStartEventBinding = new EventBinding<DialogueStartEvent>(OnDialogueStartInternal);
        _dialogueEndEventBinding = new EventBinding<DialogueEndEvent>(OnDialogueEndInternal);
    }

    protected virtual void OnEnable()
    {
        EventBus<DialogueStartEvent>.Register(_dialogueStartEventBinding);
        EventBus<DialogueEndEvent>.Register(_dialogueEndEventBinding);
    }

    protected virtual void Start()
    {
        this.Initialize();
    }

    protected virtual void OnDisable()
    {
        EventBus<DialogueStartEvent>.Deregister(_dialogueStartEventBinding);
        EventBus<DialogueEndEvent>.Deregister(_dialogueEndEventBinding);
        UnsubscribeFromStoryMessages();
    }

    private void OnDialogueStartInternal(DialogueStartEvent evt)
    {
        if (evt.initiator != gameObject)
            return;

        var dialogueHandler = evt.dialogueHandler as DialogueHandler;
        if (dialogueHandler == null)
            return;

        m_OnDialogueStart?.Invoke(dialogueHandler);
        OnDialogueStart(evt);
        UnsubscribeFromStoryMessages();
        dialogueHandler.OnLinePresented += OnTextRevealed;
        _subscribedDialogueHandler = dialogueHandler;
    }

    private void OnTextRevealed(DialogueLineContext lineContext)
    {
        if (lineContext == null)
            return;

        m_OnDialogueTextRevealed?.Invoke(lineContext.CurrentSpeakerId);
    }

    private void OnDialogueEndInternal(DialogueEndEvent evt)
    {
        if (evt.initiator != gameObject)
            return;

        var dialogueHandler = evt.dialogueHandler as DialogueHandler;
        m_OnDialogueEnd?.Invoke(dialogueHandler);
        OnDialogueEnd(evt);
        UnsubscribeFromStoryMessages();
    }

    private void UnsubscribeFromStoryMessages()
    {
        if (_subscribedDialogueHandler == null)
            return;

        _subscribedDialogueHandler.OnLinePresented -= OnTextRevealed;
        _subscribedDialogueHandler = null;
    }

    /// <summary>
    /// Called when a dialogue starts.
    /// </summary>
    protected virtual void OnDialogueStart(DialogueStartEvent dialogueStartEvent)
    {
    }

    /// <summary>
    /// Called when a dialogue ends.
    /// </summary>
    protected virtual void OnDialogueEnd(DialogueEndEvent dialogueEndEvent)
    {
    }
}