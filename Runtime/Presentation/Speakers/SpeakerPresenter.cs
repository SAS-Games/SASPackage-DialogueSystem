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
        [Tooltip("Presentation slot ID, such as left, right, center, speaker, or listener.")]
        public string role;
        public SpeakerView view;
    }

    [SerializeField] private List<ParticipantSlot> m_ParticipantSlots;

    [FieldRequiresParent] protected DialogueHandler _dialogueHandler;

    private static readonly string[][] FallbackSlotGroups =
    {
        new[] { "speaker", "left" },
        new[] { "listener", "right" },
        new[] { "center" }
    };

    private readonly Dictionary<string, SpeakerView> _viewsBySlot = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedMissingSlots = new(StringComparer.OrdinalIgnoreCase);

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

            var slotId = slot.role.Trim();
            if (_viewsBySlot.ContainsKey(slotId))
                Debug.LogWarning($"Duplicate dialogue presentation slot '{slotId}'. The final slot is used.", this);
            _viewsBySlot[slotId] = slot.view;
        }

        RegisterMissingAliases();
    }

    private void RegisterMissingAliases()
    {
        foreach (var group in FallbackSlotGroups)
        {
            SpeakerView mappedView = null;
            foreach (var slotId in group)
            {
                if (_viewsBySlot.TryGetValue(slotId, out mappedView))
                    break;
            }

            if (mappedView == null)
                continue;

            foreach (var slotId in group)
            {
                if (!_viewsBySlot.ContainsKey(slotId))
                    _viewsBySlot.Add(slotId, mappedView);
            }
        }
    }

    private void RegisterChildSlots()
    {
        var fallbackIndex = 0;
        foreach (var view in GetComponentsInChildren<SpeakerView>(true))
        {
            if (_viewsBySlot.ContainsValue(view))
                continue;

            while (fallbackIndex < FallbackSlotGroups.Length && HasMappedSlot(FallbackSlotGroups[fallbackIndex]))
                fallbackIndex++;
            if (fallbackIndex >= FallbackSlotGroups.Length)
                break;

            foreach (var slotId in FallbackSlotGroups[fallbackIndex])
                _viewsBySlot.Add(slotId, view);
            fallbackIndex++;
        }
    }

    private bool HasMappedSlot(IEnumerable<string> slotIds)
    {
        foreach (var slotId in slotIds)
        {
            if (_viewsBySlot.ContainsKey(slotId))
                return true;
        }

        return false;
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

        foreach (var view in new HashSet<SpeakerView>(_viewsBySlot.Values))
            view.gameObject.SetActive(false);

        foreach (var participant in lineContext.PresentationParticipants)
        {
            if (!_viewsBySlot.TryGetValue(participant.SlotId, out var view))
            {
                if (_reportedMissingSlots.Add(participant.SlotId))
                    Debug.LogWarning($"Dialogue presentation slot '{participant.SlotId}' is not configured.", this);
                continue;
            }

            view.gameObject.SetActive(true);
            view.SetParticipant(participant);
        }
    }
}
