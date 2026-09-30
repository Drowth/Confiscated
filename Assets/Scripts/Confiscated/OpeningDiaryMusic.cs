using UnityEngine;

namespace Confiscated
{
    /// <summary>Quiet comic underscore; uses real time because the memory pauses gameplay.</summary>
    public sealed class OpeningDiaryMusic : MonoBehaviour
    {
        AudioSource source;bool ending;float startedAt,endedAt,endingVolume;
        public static OpeningDiaryMusic Play(Transform owner)
        {
            var clip=Resources.Load<AudioClip>("Audio/OpeningDiaryMusic");if(clip==null)return null;
            var go=new GameObject("Opening diary music");go.transform.SetParent(owner,false);
            var music=go.AddComponent<OpeningDiaryMusic>();
            music.source=SchoolAudio.Create(go,SchoolAudio.Channel.Music);music.source.clip=clip;
            music.source.spatialBlend=0;music.source.loop=false;music.source.volume=0;music.startedAt=Time.unscaledTime;music.source.Play();return music;
        }
        public void Release(bool skipped){transform.SetParent(null,true);if(skipped)FadeOut();}
        public void FadeOut()
        {
            if(ending)return;ending=true;endedAt=Time.unscaledTime;endingVolume=source.volume;
            transform.SetParent(null,true);
        }
        void Update()
        {
            if(!ending&&Time.unscaledTime-startedAt>=20)FadeOut();
            if(ending)
            {
                float t=(Time.unscaledTime-endedAt)/.65f;source.volume=endingVolume*(1-Mathf.Clamp01(t));
                if(t>=1)Destroy(gameObject);return;
            }
            float target=ComicDialogue.IsActive?.07f:.18f;
            source.volume=Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime*.225f);
        }
    }
}
