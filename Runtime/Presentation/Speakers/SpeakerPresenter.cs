using System;
using System.Collections.Generic;
using SAS.Core.TagSystem;
using SAS.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class SpeakerPresenter : MonoBehaviour
{
    [Serializable]
    private class ParticipantSlot
    {
        public string role;
        public SpeakerView view;
    }

    [SerializeField] private List<ParticipantSlot> m_ParticipantSlots;

    [FieldRequiresParent] protected DialogueHandler _dialogueHandler;

    private readonly Dictionary<string, SpeakerView> _viewsByRole = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedMissingRoles = new(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        this.Initialize();

        if (_dialogueHandler != null)
            _dialogueHandler.OnLineReady += OnLineReady;

        RegisterConfiguredSlots();
        RegisterChildSlots();
    }

    private void RegisterConfiguredSlots()
    {
        if (m_ParticipantSlots == null)
            return;

        foreach (var slot in m_ParticipantSlots)
        {
            if (string.IsNullOrWhiteSpace(slot.role) || slot.view == null)
                continue;

            var role = slot.role.Trim();
            if (_viewsByRole.ContainsKey(role))
                Debug.LogWarning($"Duplicate dialogue participant slot for role '{role}'. The final slot is used.",
                    this);
            _viewsByRole[role] = slot.view;
        }
    }

    private void RegisterChildSlots()
    {
        var fallbackRoles = new[] { "speaker", "listener" };
        var fallbackIndex = 0;
        foreach (var view in GetComponentsInChildren<SpeakerView>(true))
        {
            if (_viewsByRole.ContainsValue(view))
                continue;

            while (fallbackIndex < fallbackRoles.Length && _viewsByRole.ContainsKey(fallbackRoles[fallbackIndex]))
                fallbackIndex++;
            if (fallbackIndex >= fallbackRoles.Length)
                break;

            _viewsByRole.Add(fallbackRoles[fallbackIndex], view);
            fallbackIndex++;
        }
    }

    void OnDestroy()
    {
        if (_dialogueHandler != null)
            _dialogueHandler.OnLineReady -= OnLineReady;
    }

    void OnLineReady(DialogueLineContext lineContext)
    {
        if (lineContext == null)
            return;

        foreach (var view in _viewsByRole.Values)
            view.gameObject.SetActive(false);

        foreach (var participant in lineContext.Participants)
        {
            if (!_viewsByRole.TryGetValue(participant.Role, out var view))
            {
                if (_reportedMissingRoles.Add(participant.Role))
                    Debug.LogWarning($"Dialogue participant slot for role '{participant.Role}' is not configured.",
                        this);
                continue;
            }

            view.gameObject.SetActive(true);
            view.SetParticipant(participant);
        }
    }
}