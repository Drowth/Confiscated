using UnityEngine;
namespace Confiscated
{
    public sealed class DoorSounds : MonoBehaviour
    {
        public enum Kind { Generic,Office,Heavy,Main,Locker }
        public Kind kind;
        public AudioSource Source {get;private set;}
        AudioSource unlockSource;
        int openVariant,closeVariant;bool opening;
        void Awake()
        {
            Source=gameObject.AddComponent<AudioSource>();Source.playOnAwake=false;Source.spatialBlend=1;
            Source.rolloffMode=AudioRolloffMode.Linear;Source.minDistance=1.5f;Source.maxDistance=20;Source.dopplerLevel=0;Source.volume=.65f;
        }
        public static DoorSounds For(GameObject target,Kind type)
        {
            var sound=target.GetComponent<DoorSounds>();if(sound==null)sound=target.AddComponent<DoorSounds>();sound.kind=type;return sound;
        }
        public void Movement(float before,float after)
        {
            if(after>before+.00001f){if(!opening)Play(true);opening=true;}
            else if(after<before-.00001f){opening=false;if(after<=.00001f)Play(false);}
            if(after>=1)opening=false;
        }
        public void PlayUnlock()
        {
            if(unlockSource==null)
            {
                unlockSource=gameObject.AddComponent<AudioSource>();unlockSource.playOnAwake=false;
                unlockSource.spatialBlend=1;unlockSource.rolloffMode=AudioRolloffMode.Linear;
                unlockSource.minDistance=1.5f;unlockSource.maxDistance=20;unlockSource.dopplerLevel=0;unlockSource.volume=.65f;
                unlockSource.clip=Resources.Load<AudioClip>("Audio/Doors/UnlockLock");
            }
            if(unlockSource.clip!=null)unlockSource.Play();
        }
        public void Play(bool open)
        {
            string name;
            if(!open)name=closeVariant++%2==0?"DoorClose":"DoorClose1";
            else switch(kind)
            {
                case Kind.Office:name="OfficeDoor";break;
                case Kind.Heavy:name="FireDoorHeavy";break;
                case Kind.Main:name="SchoolMainDoor";break;
                case Kind.Locker:name="LockerDoor";break;
                default:name=openVariant++%2==0?"GenericDoor":"GenericDoorOpen2";break;
            }
            var clip=Resources.Load<AudioClip>("Audio/Doors/"+name);
            if(clip!=null){Source.Stop();Source.clip=clip;Source.Play();}
        }
        void OnDestroy(){if(Source!=null)Destroy(Source);if(unlockSource!=null)Destroy(unlockSource);}
    }
}
