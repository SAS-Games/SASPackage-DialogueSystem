using System.Linq;
using NUnit.Framework;

namespace SAS.DialogueSystem.Tests
{
    public class DialogueMetadataParserTests
    {
        [Test]
        public void CanonicalFieldsProduceParticipantsAndPreserveCustomTags()
        {
            var line = DialogueMetadataParser.ParseLine(
                "We should leave.",
                new[]
                {
                    "id:line.leave", "locale:dialogue.leave", "speaker:alice",
                    "speaker_name:Alice", "portrait:happy", "animation:Talk",
                    "listener:bob", "listener_portrait:concerned", "mood:urgent"
                },
                DialogueMetadataSchema.Canonical);

            Assert.AreEqual("line.leave", line.LineId);
            Assert.AreEqual("dialogue.leave", line.Locale);
            Assert.AreEqual("alice", line.CurrentSpeakerId);
            Assert.AreEqual("bob", line.ListenerId);
            Assert.AreEqual("urgent", line.GetTagValues("mood")[0]);
            Assert.IsTrue(line.TryGetParticipant("speaker", out var speaker));
            Assert.AreEqual("Alice", speaker.DisplayName);
            Assert.AreEqual("happy", speaker.PortraitKey);
            Assert.AreEqual("Talk", speaker.AnimationKey);
        }

        [Test]
        public void GenericRolesAndDuplicateDiagnosticsAreSupported()
        {
            var line = DialogueMetadataParser.ParseLine(
                "What did you see?",
                new[]
                {
                    "id:first", "id:second", "participant.interviewer:maya",
                    "participant.interviewer.name:Detective Maya",
                    "participant.interviewer.portrait:focused"
                },
                DialogueMetadataSchema.Canonical);

            Assert.AreEqual("second", line.LineId);
            Assert.IsTrue(line.TryGetParticipant("interviewer", out var participant));
            Assert.AreEqual("Detective Maya", participant.DisplayName);
            Assert.IsTrue(line.Diagnostics.Any(item => item.Code == "duplicate-field"));
        }

        [Test]
        public void ParticipantDetailsWithoutAnIdAreRejected()
        {
            var line = DialogueMetadataParser.ParseLine(
                "Malformed.",
                new[] { "portrait:focused", "listener_animation:Listen" },
                DialogueMetadataSchema.Canonical);

            Assert.IsTrue(line.HasErrors);
            Assert.AreEqual(2, line.Diagnostics.Count(item => item.Code == "participant-id-missing"));
        }

        [Test]
        public void StorySkipDirectiveIsParsedWithoutHidingCustomMetadata()
        {
            var enabled = DialogueMetadataParser.ParseLine(
                "You may leave now.",
                new[] { "skip:enable", "quest:forest_gate" },
                DialogueMetadataSchema.Canonical);
            var invalid = DialogueMetadataParser.ParseLine(
                "Malformed directive.",
                new[] { "skip:later" },
                DialogueMetadataSchema.Canonical);

            Assert.AreEqual(DialogueStorySkipDirective.Enable, enabled.StorySkipDirective);
            Assert.AreEqual("forest_gate", enabled.GetTagValues("quest")[0]);
            Assert.AreEqual(DialogueStorySkipDirective.Unchanged, invalid.StorySkipDirective);
            Assert.IsTrue(invalid.Diagnostics.Any(item => item.Code == "invalid-skip-directive"));
            Assert.IsFalse(invalid.HasErrors);
        }
    }
}
