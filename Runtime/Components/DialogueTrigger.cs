using System.Collections;
using System.Collections.Generic;
using SAS.Core.TagSystem;
using SAS.DialogueSystem;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DialogueTrigger : MonoBehaviour
{
    [Inject] private IDialogueHandler _dialogueHandler;

    [Tooltip("Optional direct reference. When empty, SAS Core injection is used first, then the scene is searched.")]
    [SerializeField]
    private DialogueHandler m_DialogueHandler;

    [Header("Ink JSON")] [FormerlySerializedAs("inkJSON")] [SerializeField]
    private TextAsset m_InkJSON;

    [Tooltip("Optional per-story tag mapping. The dialogue handler default is used when this is empty.")]
    [SerializeField]
    private DialogueMetadataProfile m_MetadataProfile;

    [Header("Ink Bindings")]
    [Tooltip("Optional binding components from this or another GameObject. InkStoryBinding components on this GameObject are discovered automatically.")]
    [SerializeField] private List<MonoBehaviour> m_BindingSources = new();

    [Header("Trigger")]
    [SerializeField] private bool m_AutoStart = false;
    [SerializeField] private bool m_TriggerOncePerSession = true;
    private readonly List<IInkStoryBinding> _bindings = new();
    private bool _triggered = false;

    public TextAsset StoryAsset => m_InkJSON;
    public bool DialogueIsPlaying => _dialogueHandler != null && _dialogueHandler.DialogueIsPlaying;

    private void Awake()
    {
        ResolveBindings();
    }

    private IEnumerator Start()
    {
        this.Initialize();
        ResolveDialogueHandler();

        if (!m_AutoStart)
            yield break;

        // Let DialogueHandler.Start initialize its presentation before auto-starting.
        yield return null;
        ShowDialogue();
    }

    public void ShowDialogue()
    {
        ResolveDialogueHandler();
        ResolveBindings();

        if (_dialogueHandler == null)
        {
            Debug.LogWarning("DialogueTrigger cannot show dialogue because no dialogue handler is bound.", this);
            return;
        }

        if (m_InkJSON == null)
        {
            Debug.LogWarning("DialogueTrigger cannot show dialogue because Ink JSON is not assigned.", this);
            return;
        }

        if (_dialogueHandler.DialogueIsPlaying || (m_TriggerOncePerSession && _triggered))
            return;

        // Register on every launch. If two story bindings use the same Ink external
        // name, the binding belonging to the story being launched must win.
        RegisterExternalMethods();

        _dialogueHandler.OnEnterDialogueMode += BindActiveStoryVariables;
        try
        {
            _triggered = true;
            _dialogueHandler.EnterDialogueMode(m_InkJSON, gameObject, m_MetadataProfile);
        }
        finally
        {
            _dialogueHandler.OnEnterDialogueMode -= BindActiveStoryVariables;
        }
    }

    public void ResetTrigger() => _triggered = false;

    private void ResolveDialogueHandler()
    {
        if (_dialogueHandler != null)
            return;

        if (m_DialogueHandler == null)
            m_DialogueHandler = FindFirstObjectByType<DialogueHandler>();

        _dialogueHandler = m_DialogueHandler;
    }

    private void ResolveBindings()
    {
        _bindings.Clear();

        foreach (var source in m_BindingSources)
            AddBinding(source);

        foreach (var source in GetComponents<MonoBehaviour>())
            AddBinding(source);
    }

    private void AddBinding(MonoBehaviour source)
    {
        if (source is IInkStoryBinding binding && !_bindings.Contains(binding))
            _bindings.Add(binding);
    }

    private void RegisterExternalMethods()
    {
        var registry = _dialogueHandler?.InkExternalMethodRegistry;
        if (registry == null)
            return;

        foreach (var binding in _bindings)
            binding.RegisterExternalMethods(registry);
    }

    private void BindActiveStoryVariables()
    {
        var story = _dialogueHandler.CurrentStory;
        foreach (var binding in _bindings)
            binding.BindVariables(story);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (var source in m_BindingSources)
        {
            if (source != null && source is not IInkStoryBinding)
                Debug.LogWarning($"'{source.name}' does not implement {nameof(IInkStoryBinding)}.", source);
        }
    }
#endif
}
