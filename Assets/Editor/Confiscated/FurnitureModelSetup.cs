using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Swaps box-built furniture for the Tripo models in Assets/Art/Models/SchoolProps/Furniture (Codex batch 4). The old
    /// box parts keep their colliders and NavMesh carving but stop drawing; the model is fitted to exactly the space they
    /// took, turned so its long side and front match, and lit like the office props. A model that isn't in the folder
    /// leaves its boxes as they were. Repeatable; chained into SchoolRunSetup.Build.
    /// </summary>
    public static class FurnitureModelSetup
    {
        public const string Folder="Assets/Art/Models/SchoolProps/Furniture/",RootName="Furniture model";

        enum Facing { AwayFromWall, TowardWall, AlongForward }
        /// <summary>models (several = variants picked per piece), how to find each target, which child parts stay visible.</summary>
        sealed class Job{public string[] models;public Func<Transform,bool> match;public Func<Transform,bool> keep=_=>false;public Facing facing=Facing.AwayFromWall;}
        static readonly Job[] Jobs=
        {
            new Job{models=new[]{"FilingCabinet"},match=t=>t.name=="FilingCabinet"},
            new Job{models=new[]{"EntrancePlanter"},match=t=>t.name=="Entrance planter"},
            new Job{models=new[]{"CaretakerTrolley"},match=t=>t.name=="MaintenanceTrolley"},
            new Job{models=new[]{"DinnerTrolley"},match=t=>t.name=="Sketched trolley visual",facing=Facing.AlongForward},
            new Job{models=new[]{"VisitorSignInDesk"},match=t=>t.name=="Visitor sign-in desk",keep=p=>{var n=p.name.ToLowerInvariant();return n.Contains("book")||n.Contains("pencil")||n.Contains("ruled")||n.Contains("fold");}},
            new Job{models=new[]{"TallStorageCupboard"},match=t=>t.name.StartsWith("P_TallStorage")},
            new Job{models=new[]{"MaintenanceShelving"},match=t=>t.name=="MaintenanceShelving",keep=p=>!new[]{"SideL","SideR","Back","Shelf","Box","Paper","Lettering"}.Contains(p.name)},
            // The teacher sits at the kneehole and drawers (the model's front), backed onto the board wall; the class sees the panel.
            new Job{models=new[]{"TeacherDesk"},match=t=>t.name.StartsWith("P_Desk")||t.name=="Decoy teacher desk",keep=p=>p.name.ToLowerInvariant().Contains("book"),facing=Facing.TowardWall},
            // Corridor lockers (not the player's own): six worn variants, the same one every build for a given locker.
            new Job{models=Enumerable.Range(1,7).Select(i=>"SchoolLocker"+i).ToArray(),match=t=>t.name=="Locker"&&t.GetComponent<Renderer>()!=null&&t.GetComponentInParent<PlayerLocker>()==null},
        };

        [MenuItem("Confiscated/School Run/Apply Furniture Models (Codex batch 4)")]
        public static void ApplyMenu()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play first.");
            int n=ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Furniture] "+n+" pieces now use a model.");
        }

        /// <summary>Does not save. Returns how many pieces now use a model.</summary>
        public static int ApplyToScene()
        {
            AssetDatabase.Refresh();int count=0;
            var all=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var job in Jobs)
            {
                var available=job.models.Where(m=>PickupModelSetup.Has(m,Folder)).ToArray();
                if(available.Length==0)continue;
                // Earlier jobs replace old model roots, so skip anything destroyed since `all` was gathered, and never
                // look inside a model tree.
                var targets=all.Where(t=>t!=null&&job.match(t)&&!Ancestors(t).Any(a=>a.name==RootName||job.match(a))).ToList();
                foreach(var t in targets)
                {
                    // Stable pick from the position, so rebuilds don't reshuffle the lockers.
                    var p=t.position;int pick=Mathf.Abs(Mathf.RoundToInt(p.x*7.3f)+Mathf.RoundToInt(p.z*3.1f))%available.Length;
                    if(Swap(t,job,available[pick]))count++;
                }
            }
            return count;
        }
        static bool Character(Renderer r){var n=r.name.ToLowerInvariant();return n.Contains("cutout")||n.Contains("shadow")||r.GetComponentInParent<CaretakerAI>()!=null;}
        static IEnumerable<Transform> Ancestors(Transform t){for(var p=t.parent;p!=null;p=p.parent)yield return p;}

        static bool Swap(Transform target,Job job,string model)
        {
            foreach(var old in target.Cast<Transform>().Where(c=>c.name==RootName).ToArray())Object.DestroyImmediate(old.gameObject);
            // Characters riding along (the dinner lady behind her trolley) and their ground shadows are never furniture:
            // bring back any an earlier pass hid, and leave them out of the measuring.
            foreach(var r in target.GetComponentsInChildren<Renderer>(true).Where(Character))r.enabled=true;
            var parts=target.GetComponentsInChildren<Renderer>(true).Where(r=>!job.keep(r.transform)&&!Character(r)&&r.GetComponentInParent<Interactable>()==null&&!(r is ParticleSystemRenderer)).ToList();
            if(parts.Count==0)return false;
            foreach(var r in parts)r.enabled=true; // measure the original, even on a re-run
            var space=parts[0].bounds;foreach(var r in parts)space.Encapsulate(r.bounds);
            // A child of the target, so it follows anything that moves (the dinner trolley), standing on the old footprint.
            var root=new GameObject(RootName).transform;root.SetParent(target,false);
            root.position=new Vector3(space.center.x,space.min.y,space.center.z);root.rotation=Quaternion.identity;
            float yaw=Yaw(job,space,target);
            if(!PickupModelSetup.Place(root,model,Mathf.Max(space.size.x,space.size.z),0,yaw,Folder)){Object.DestroyImmediate(root.gameObject);return false;}
            var holder=root.GetChild(0);
            // Fit the exact box it replaces (Tripo proportions drift a little). Each world axis stretches whichever of the
            // holder's own axes lies along it, which also holds under a rotated or scaled target (a locker box).
            var have=Bounds(holder);var scale=holder.localScale;
            Vector3 fit=new(space.size.x/Mathf.Max(have.size.x,.001f),space.size.y/Mathf.Max(have.size.y,.001f),space.size.z/Mathf.Max(have.size.z,.001f));
            var axes=new[]{holder.right,holder.up,holder.forward};
            for(int w=0;w<3;w++)
            {
                var world=w==0?Vector3.right:w==1?Vector3.up:Vector3.forward;
                int best=0;for(int i=1;i<3;i++)if(Mathf.Abs(Vector3.Dot(axes[i],world))>Mathf.Abs(Vector3.Dot(axes[best],world)))best=i;
                scale[best]*=fit[w];
            }
            holder.localScale=scale;
            var now=Bounds(holder);holder.position+=new Vector3(space.center.x-now.center.x,space.min.y-now.min.y,space.center.z-now.center.z);
            // Furniture follows the room's light, like the office props.
            foreach(var r in holder.GetComponentsInChildren<Renderer>())
            {r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);}
            foreach(var r in parts){r.enabled=false;EditorUtility.SetDirty(r);}
            return true;
        }

        /// <summary>
        /// Every batch-4 model has its front (drawers, doors, vents, the VISITORS panel) on its own -X, width along Z
        /// (checked from four-side renders, Docs/Furniture_Views_*.png). The front faces away from the nearest wall, towards
        /// it (teacher desks), or along the piece's own forward (the moving dinner trolley).
        /// </summary>
        static float Yaw(Job job,Bounds space,Transform target)
        {
            bool longX=space.size.x>=space.size.z;
            Vector3 across=longX?Vector3.forward:Vector3.right,front=across;
            if(job.facing!=Facing.AlongForward)
            {
                float Hit(Vector3 d)=>Physics.Raycast(new Vector3(space.center.x,space.min.y+.5f,space.center.z),d,out var h,4f,~0,QueryTriggerInteraction.Ignore)&&!h.transform.IsChildOf(target)?h.distance:99;
                var wall=Hit(across)<Hit(-across)?across:-across;
                front=job.facing==Facing.TowardWall?wall:-wall;
            }
            else
            {
                var f=target.forward;f.y=0;
                // A trolley's front is its long axis; everything else faces across it.
                Vector3 axis=Mathf.Abs(Vector3.Dot(f.normalized,across))>.7f?across:(longX?Vector3.right:Vector3.forward);
                front=Mathf.Sign(Vector3.Dot(f,axis))*axis;
            }
            // Turn the model's -X onto `front`: +90 about Y takes -X to +Z, then LookRotation takes +Z to front.
            return (Quaternion.LookRotation(front)*Quaternion.Euler(0,90,0)).eulerAngles.y;
        }

        static Bounds Bounds(Transform root){var rs=root.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    }
}
