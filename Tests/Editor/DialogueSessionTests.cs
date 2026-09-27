using System;
using System.Linq;
using Ink;
using NUnit.Framework;

namespace SAS.DialogueSystem.Tests
{
    public class DialogueSessionTests
    {
        [Test]
        public void SessionOwnsLineChoiceAndCompletionTransitions()
        {
            var session = CreateSession(@"
Hello. # speaker:alice # listener:bob
* [Ask about the gate. # id:choice.ask_gate]
    The gate is locked.
    -> END
");

            var line = session.Continue();
            Assert.AreEqual(DialogueStepKind.Line, line.Kind);
            Assert.Throws<InvalidOperationException>(() => session.Continue());
            Assert.IsTrue(session.CompleteLinePresentation(line.Line));
            Assert.AreEqual(DialogueSessionState.PresentingChoices, session.State);
            Assert.IsTrue(session.TryChoose(0));

            var answer = session.Continue();
            Assert.AreEqual(DialogueStepKind.Line, answer.Kind);
            Assert.IsTrue(session.CompleteLinePresentation(answer.Line));
            Assert.AreEqual(DialogueAdvanceAction.ContinueStory, session.GetAdvanceAction());
            Assert.AreEqual(DialogueStepKind.Completed, session.Continue().Kind);
            Assert.AreEqual(DialogueSessionState.Exiting, session.State);
        }

        [Test]
        public void StaleLineCannotCompleteTheCurrentLine()
        {
            var session = CreateSession("First.\nSecond.\n-> END");
            var first = session.Continue().Line;
            Assert.IsTrue(session.CompleteLinePresentation(first));
            var second = session.Continue().Line;
            Assert.IsFalse(session.CompleteLinePresentation(first));
            Assert.AreSame(second, session.CurrentLine);
        }

        [Test]
        public void StorySkipUnlocksAfterTaggedLineAndKeepsNormalAdvanceAvailable()
        {
            var session = CreateSession(@"
# skip:enable
You may skip after reading this line.
You may also keep reading.
-> END
");

            var unlockLine = session.Continue();
            Assert.IsFalse(session.CanSkipStory);
            Assert.IsFalse(session.TrySkipStory());

            Assert.IsTrue(session.CompleteLinePresentation(unlockLine.Line));
            Assert.IsTrue(session.CanSkipStory);
            Assert.AreEqual(DialogueAdvanceAction.ContinueStory, session.GetAdvanceAction());

            var nextLine = session.Continue();
            Assert.AreEqual(DialogueStepKind.Line, nextLine.Kind);
            Assert.IsTrue(session.CanSkipStory);
            Assert.IsTrue(session.TrySkipStory());
            Assert.AreEqual(DialogueSessionState.Exiting, session.State);
        }

        [Test]
        public void StorySkipCanBeRevokedByALaterCompletedLine()
        {
            var session = CreateSession(@"
# skip:enable
Unlock.
# skip:disable
Lock again.
-> END
");

            var unlockLine = session.Continue();
            session.CompleteLinePresentation(unlockLine.Line);
            Assert.IsTrue(session.CanSkipStory);

            var lockLine = session.Continue();
            Assert.IsTrue(session.CanSkipStory);
            session.CompleteLinePresentation(lockLine.Line);
            Assert.IsFalse(session.CanSkipStory);
            Assert.IsFalse(session.TrySkipStory());
        }


        [Test]
        public void FixedCharacterPlacementKeepsCharactersInTheirSlotsWhenSpeakerChanges()
        {
            var session = CreateSession(@"
# placement:fixed-character
# slot.left:alice
# slot.right:bob
# speaker:alice
# listener:bob
First.
# speaker:bob
# listener:alice
Second.
-> END
");

            var first = session.Continue().Line;
            Assert.AreEqual(DialoguePlacementMode.FixedCharacter, first.PlacementMode);
            Assert.AreEqual("alice", first.PresentationParticipants.Single(item => item.SlotId == "left").CharacterId);
            Assert.IsTrue(first.PresentationParticipants.Single(item => item.SlotId == "left").IsCurrentSpeaker);
            Assert.AreEqual("bob", first.PresentationParticipants.Single(item => item.SlotId == "right").CharacterId);

            session.CompleteLinePresentation(first);
            var second = session.Continue().Line;
            Assert.AreEqual("alice", second.PresentationParticipants.Single(item => item.SlotId == "left").CharacterId);
            Assert.IsFalse(second.PresentationParticipants.Single(item => item.SlotId == "left").IsCurrentSpeaker);
            Assert.AreEqual("bob", second.PresentationParticipants.Single(item => item.SlotId == "right").CharacterId);
            Assert.IsTrue(second.PresentationParticipants.Single(item => item.SlotId == "right").IsCurrentSpeaker);
        }

        [Test]
        public void LegacyStoriesContinueToRouteBySpeakerAndListenerRoles()
        {
            var session = CreateSession("Hello. # speaker:alice # listener:bob\n-> END");

            var line = session.Continue().Line;

            Assert.AreEqual(DialoguePlacementMode.FollowSpeaker, line.PlacementMode);
            Assert.AreEqual("alice", line.PresentationParticipants.Single(item => item.SlotId == "speaker").CharacterId);
            Assert.AreEqual("bob", line.PresentationParticipants.Single(item => item.SlotId == "listener").CharacterId);
        }
        private static DialogueSession CreateSession(string source)
        {
            var story = new Compiler(source).Compile();
            Assert.IsNotNull(story);
            return new DialogueSession(story, DialogueMetadataSchema.Canonical);
        }
    }
}
