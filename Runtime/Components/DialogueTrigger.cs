using SAS.Core.TagSystem;
using SAS.DialogueSystem;
using UnityEngine;
using UnityEngine.Serialization;

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

    [SerializeField] private bool m_AutoStart = false;
    [SerializeField] private bool m_TriggerOncePerSession = true;
    private bool _triggered = false;

    private void Start()
    {
        this.Initialize();
        ResolveDialogueHandler();
        if (m_AutoStart)
            ShowDialogue();
    }

    public void ShowDialogue()
    {
        ResolveDialogueHandler();

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

        if (_dialogueHandler.DialogueIsPlaying)
            return;

        if (m_TriggerOncePerSession)
        {
            if (!_triggered)
            {
                _triggered = true;
                _dialogueHandler.EnterDialogueMode(m_InkJSON, gameObject, m_MetadataProfile);
            }
        }
        else
            _dialogueHandler.EnterDialogueMode(m_InkJSON, gameObject, m_MetadataProfile);
    }

    private void ResolveDialogueHandler()
    {
        if (_dialogueHandler != null)
            return;

        if (m_DialogueHandler == null)
            m_DialogueHandler = FindFirstObjectByType<DialogueHandler>();

        _dialogueHandler = m_DialogueHandler;
    }
}