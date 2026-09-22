/// <summary>
/// Optional bridge for forwarding dialogue portrait animation states to another system.
/// Implement this on the same GameObject as <see cref="SpeakerView"/>.
/// </summary>
public interface IDialogueAnimationTarget
{
    void Process(string stateName);
}