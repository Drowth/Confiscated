namespace Confiscated
{
    /// <summary>Reusable stationary teacher NPC with the shared camera-facing cutout and idle motion.</summary>
    public class MissTenison : Interactable
    {
        public DetentionController detention;
        public override string GetPrompt(PlayerInteractor player) => "F: speak to Miss D Tenison";
        public override void Interact(PlayerInteractor player)
        {
            string line = detention != null && detention.Active ? detention.blackboard.TeacherInstruction :
                "This is detention. I would rather not see you here again.";
            HudController.Instance?.SetStatus("Miss D Tenison: " + line, 5f);
        }
    }
}
