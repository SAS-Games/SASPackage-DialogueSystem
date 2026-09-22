using System;
using System.Collections.Generic;
using Ink.Runtime;
using SAS.Core.TagSystem;
using UnityEngine;

namespace SAS.DialogueSystem
{
    public struct ChoiceContext
    {
        public IReadOnlyList<ChoiceOptionContext> Options;
    }

    public struct ChoiceOptionContext
    {
        public Choice Choice;
        public int ChoiceIndex;
        public DialogueLineContext LineContext;
    }

    public class DialogueChoiceController : MonoBehaviour
    {
        [FieldRequiresParent] private DialogueHandler _dialogueHandler;

        public event Action<ChoiceContext> OnChoicesPrepared;
        public event Action OnChoicesHidden;

        private void Awake()
        {
            this.Initialize();
        }

        private void OnEnable()
        {
            if (_dialogueHandler == null)
                return;

            _dialogueHandler.OnLineReady += HandleLineReady;
            _dialogueHandler.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_dialogueHandler == null)
                return;

            _dialogueHandler.OnLineReady -= HandleLineReady;
            _dialogueHandler.OnStateChanged -= HandleStateChanged;
            OnChoicesHidden?.Invoke();
        }

        private void HandleLineReady(DialogueLineContext _)
        {
            OnChoicesHidden?.Invoke();
        }

        private void HandleStateChanged(DialogueSessionState state)
        {
            if (state == DialogueSessionState.PresentingChoices)
            {
                PrepareChoices(_dialogueHandler.CurrentStory);
                return;
            }

            if (state == DialogueSessionState.Starting ||
                state == DialogueSessionState.PresentingLine ||
                state == DialogueSessionState.Exiting ||
                state == DialogueSessionState.Faulted ||
                state == DialogueSessionState.Idle)
            {
                OnChoicesHidden?.Invoke();
            }
        }

        private void PrepareChoices(Story story)
        {
            if (story == null)
            {
                OnChoicesHidden?.Invoke();
                return;
            }

            var choices = story.currentChoices;
            var options = new List<ChoiceOptionContext>(choices.Count);

            for (int index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                var lineContext = BuildChoiceLineContext(choice);
                if (!_dialogueHandler.ValidateMetadata(lineContext))
                {
                    OnChoicesHidden?.Invoke();
                    return;
                }

                options.Add(new ChoiceOptionContext
                {
                    Choice = choice,
                    ChoiceIndex = index,
                    LineContext = lineContext
                });
            }

            OnChoicesPrepared?.Invoke(new ChoiceContext
            {
                Options = options
            });
        }

        private DialogueLineContext BuildChoiceLineContext(Choice choice)
        {
            return _dialogueHandler.ParseMetadata(choice.text, choice.tags);
        }

        public void MakeChoice(int choiceIndex) => _dialogueHandler.MakeChoice(choiceIndex);
    }
}