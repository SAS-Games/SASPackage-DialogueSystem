using System;
using System.Collections.Generic;
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
            State = DialogueSessionState.Starting;
        }

        public Story Story { get; }
        public DialogueMetadataSchema MetadataSchema { get; }
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
}