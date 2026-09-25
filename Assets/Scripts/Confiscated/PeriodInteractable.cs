using UnityEngine;
namespace Confiscated
{
    public sealed class PeriodInteractable : Interactable
    {
        public enum Role { Worksheet, Teacher, Delivery }
        public Role role;
        public SchoolPeriodController period;
        public override string GetPrompt(PlayerInteractor player)=>period==null?null:period.Prompt(role);
        public override bool CanInteract(PlayerInteractor player)=>period!=null&&period.CanInteract(role);
        public override void Interact(PlayerInteractor player){if(CanInteract(player))period.Interact(role);}
    }
}
