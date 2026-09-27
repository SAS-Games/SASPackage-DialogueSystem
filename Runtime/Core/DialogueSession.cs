using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;

namespace SAS.DialogueSystem
{
    public enum DialogueSessionState
    {
        Idle,
        Starting,
        PresentingLine,
        WaitingForAdvance,
        PresentingChoices,
        Exiting,
        Faulted
    }

    public enum DialogueStepKind
    {
        Line,
        Choices,
        Completed
    }

    public enum DialogueAdvanceAction
    {
        None,
        RevealCurrentLine,
        ContinueStory
    }

    public readonly struct DialogueStep
    {
        private DialogueStep(DialogueStepKind kind, DialogueLineContext line)
        {
            Kind = kind;
            Line = line;
        }

        public DialogueStepKind Kind { get; }
        public DialogueLineContext Line { get; }
        public static DialogueStep PresentLine(DialogueLineContext line) => new(DialogueStepKind.Line, line);
        public static DialogueStep PresentChoices() => new(DialogueStepKind.Choices, null);
        public static DialogueStep Complete() => new(DialogueStepKind.Completed, null);
    }

    /// <summary>
    /// Pure dialogue-domain state. Unity input and presentation are adapters around this session.
    /// </summary>
    public sealed class DialogueSession
    {
        public DialogueSession(Story story, DialogueMetadataSchema metadataSchema)
        {
            Story = story ?? throw new ArgumentNullException(nameof(story));
            MetadataSchema = metadataSchema ?? throw new ArgumentNullException(nameof(metadataSchema));
            PresentationState = new DialoguePresentationState(MetadataSchema);
            State = DialogueSessionState.Starting;
        }

        public Story Story { get; }
        public DialogueMetadataSchema MetadataSchema { get; }
        public DialoguePresentationState PresentationState { get; }
        public DialogueSessionState State { get; private set; }
        public DialogueLineContext CurrentLine { get; private set; }
        public bool CanSkipStory { get; private set; }
        public event Action<DialogueSessionState> StateChanged;

        public DialogueStep Continue()
        {
            if (State != DialogueSessionState.Starting && State != DialogueSessionState.WaitingForAdvance)
                throw new InvalidOperationException($"Cannot continue a dialogue session while it is {State}.");

            CurrentLine = null;
            while (Story.canContinue)
            {
                var text = Story.Continue();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                CurrentLine = ParseMetadata(text, Story.currentTags);
                PresentationState.Resolve(CurrentLine);
                TransitionTo(DialogueSessionState.PresentingLine);
                return DialogueStep.PresentLine(CurrentLine);
            }

            if (Story.currentChoices.Count > 0)
            {
                TransitionTo(DialogueSessionState.PresentingChoices);
                return DialogueStep.PresentChoices();
            }

            TransitionTo(DialogueSessionState.Exiting);
            return DialogueStep.Complete();
        }

        public bool CompleteLinePresentation(DialogueLineContext line)
        {
            if (State != DialogueSessionState.PresentingLine || line == null || !ReferenceEquals(CurrentLine, line))
                return false;

            ApplyStorySkipDirective(line.StorySkipDirective);
            TransitionTo(Story.currentChoices.Count > 0 ? DialogueSessionState.PresentingChoices : DialogueSessionState.WaitingForAdvance);
            return true;
        }

        public bool TrySkipStory()
        {
            if (!CanSkipStory || State == DialogueSessionState.Exiting || State == DialogueSessionState.Faulted)
                return false;

            CurrentLine = null;
            TransitionTo(DialogueSessionState.Exiting);
            return true;
        }

        public DialogueAdvanceAction GetAdvanceAction()
        {
            return State switch
            {
                DialogueSessionState.PresentingLine => DialogueAdvanceAction.RevealCurrentLine,
                DialogueSessionState.WaitingForAdvance => DialogueAdvanceAction.ContinueStory,
                _ => DialogueAdvanceAction.None
            };
        }

        public bool TryChoose(int choiceIndex)
        {
            if (State != DialogueSessionState.PresentingChoices || choiceIndex < 0 || choiceIndex >= Story.currentChoices.Count)
                return false;

            Story.ChooseChoiceIndex(choiceIndex);
            CurrentLine = null;
            TransitionTo(DialogueSessionState.Starting);
            return true;
        }

        public DialogueLineContext ParseMetadata(string text, IEnumerable<string> tags)
        {
            return DialogueMetadataParser.ParseLine(text, tags, MetadataSchema);
        }

        public void BeginExit()
        {
            if (State != DialogueSessionState.Exiting)
                TransitionTo(DialogueSessionState.Exiting);
        }

        public void Fault()
        {
            TransitionTo(DialogueSessionState.Faulted);
        }

        private void ApplyStorySkipDirective(DialogueStorySkipDirective directive)
        {
            switch (directive)
            {
                case DialogueStorySkipDirective.Enable:
                    CanSkipStory = true;
                    break;
                case DialogueStorySkipDirective.Disable:
                    CanSkipStory = false;
                    break;
            }
        }

        private void TransitionTo(DialogueSessionState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }
    }

    public sealed class DialoguePresentationState
    {
        private sealed class CharacterAppearance
        {
            public string Role = string.Empty;
            public string DisplayName = string.Empty;
            public string PortraitKey = string.Empty;
            public string AnimationKey = string.Empty;
        }

        private readonly DialogueMetadataSchema _schema;
        private readonly Dictionary<string, string> _characterBySlot = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _slotByCharacter = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CharacterAppearance> _appearanceByCharacter = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _slotOrder = new();

        public DialoguePresentationState(DialogueMetadataSchema schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            foreach (var slot in schema.AutoPlacementSlots)
                AddSlotToOrder(slot);
        }

        public DialoguePlacementMode Mode { get; private set; } = DialoguePlacementMode.FollowSpeaker;
        public IReadOnlyDictionary<string, string> SlotAssignments => _characterBySlot;

        public void Resolve(DialogueLineContext line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            ApplyPlacementDirective(line.PlacementDirective);
            foreach (var assignment in line.SlotAssignments)
                Assign(assignment.Key, assignment.Value);

            foreach (var participant in line.Participants)
                Remember(participant);

            if (Mode == DialoguePlacementMode.FollowSpeaker)
            {
                line.ResolveRoleBasedPresentation();
                return;
            }

            foreach (var participant in line.Participants)
                AutoAssign(participant.CharacterId, line);

            var presentation = new List<DialoguePresentationParticipant>();
            foreach (var slot in _slotOrder)
            {
                if (!_characterBySlot.TryGetValue(slot, out var characterId))
                    continue;

                _appearanceByCharacter.TryGetValue(characterId, out var appearance);
                presentation.Add(new DialoguePresentationParticipant(slot, characterId,
                    characterId.Equals(line.CurrentSpeakerId, StringComparison.OrdinalIgnoreCase),
                    appearance?.DisplayName, appearance?.PortraitKey, appearance?.AnimationKey, appearance?.Role));
            }

            if (!string.IsNullOrEmpty(line.CurrentSpeakerId) && !_slotByCharacter.ContainsKey(line.CurrentSpeakerId))
                line.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Warning,
                    "speaker-not-visible", $"Current speaker '{line.CurrentSpeakerId}' could not be assigned to a presentation slot."));

            line.SetPresentation(DialoguePlacementMode.FixedCharacter, presentation);
        }

        private void ApplyPlacementDirective(DialoguePlacementDirective directive)
        {
            switch (directive)
            {
                case DialoguePlacementDirective.FollowSpeaker:
                    Mode = DialoguePlacementMode.FollowSpeaker;
                    break;
                case DialoguePlacementDirective.FixedCharacter:
                    Mode = DialoguePlacementMode.FixedCharacter;
                    break;
            }
        }

        private void Remember(DialogueParticipant participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.CharacterId))
                return;

            if (!_appearanceByCharacter.TryGetValue(participant.CharacterId, out var appearance))
            {
                appearance = new CharacterAppearance();
                _appearanceByCharacter[participant.CharacterId] = appearance;
            }

            appearance.Role = participant.Role;
            if (!string.IsNullOrEmpty(participant.DisplayName))
                appearance.DisplayName = participant.DisplayName;
            if (!string.IsNullOrEmpty(participant.PortraitKey))
                appearance.PortraitKey = participant.PortraitKey;
            if (!string.IsNullOrEmpty(participant.AnimationKey))
                appearance.AnimationKey = participant.AnimationKey;
        }

        private void AutoAssign(string characterId, DialogueLineContext line)
        {
            if (string.IsNullOrWhiteSpace(characterId) || _slotByCharacter.ContainsKey(characterId))
                return;

            var availableSlot = _schema.AutoPlacementSlots.FirstOrDefault(slot => !_characterBySlot.ContainsKey(slot));
            if (!string.IsNullOrEmpty(availableSlot))
            {
                Assign(availableSlot, characterId);
                return;
            }

            line.AddDiagnostic(new DialogueMetadataDiagnostic(DialogueMetadataSeverity.Warning,
                "no-available-slot", $"Character '{characterId}' has no available auto-placement slot."));
        }

        private void Assign(string slotId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return;

            slotId = slotId.Trim();
            AddSlotToOrder(slotId);

            if (string.IsNullOrWhiteSpace(characterId))
            {
                if (_characterBySlot.TryGetValue(slotId, out var removedCharacter))
                {
                    _characterBySlot.Remove(slotId);
                    _slotByCharacter.Remove(removedCharacter);
                }
                return;
            }

            characterId = characterId.Trim();
            if (_slotByCharacter.TryGetValue(characterId, out var previousSlot) &&
                !previousSlot.Equals(slotId, StringComparison.OrdinalIgnoreCase))
                _characterBySlot.Remove(previousSlot);

            if (_characterBySlot.TryGetValue(slotId, out var previousCharacter) &&
                !previousCharacter.Equals(characterId, StringComparison.OrdinalIgnoreCase))
                _slotByCharacter.Remove(previousCharacter);

            _characterBySlot[slotId] = characterId;
            _slotByCharacter[characterId] = slotId;
        }

        private void AddSlotToOrder(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || _slotOrder.Any(existing =>
                    existing.Equals(slotId, StringComparison.OrdinalIgnoreCase)))
                return;

            _slotOrder.Add(slotId.Trim());
        }
    }
}