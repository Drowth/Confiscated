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

        enum Facing { AwayFromWall, TowardWall, AlongForward, AwayFromNearestWall }
        /// <summary>models (several = variants picked per piece), how to find each target, which child parts stay visible,
        /// loose parts beside it that belong to the piece, and whether to keep the model's proportions inside the space.</summary>
        sealed class Job{public string[] models;public Func<Transform,bool> match;public Func<Transform,bool> keep=_=>false;public Facing facing=Facing.AwayFromWall;public Func<Transform,IEnumerable<Renderer>> extras;public bool uniform,flush,doubleSided;public Vector3? front;}
        public static readonly string[] SchoolOfficeModels={"OfficeServingHatch2","OfficePrinterScanner","OfficePigeonholes","OfficeReceptionCounter","FilingCabinets"};
        static readonly Job[] Jobs=
        {
            new Job{models=new[]{"FilingCabinet","FilingCabinet2"},match=t=>t.name=="FilingCabinet"},
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
            // School office (SchoolOfficeSetup rebuilds it after this pass, then calls back for just these). The hatch model
            // has the SCHOOL OFFICE sign and the "Hatch closed" note painted on; the photocopier's box is deeper than wide.
            // Stands between the corridor and the office: needs to read from both sides. OfficeServingHatch2 is modelled
            // two-sided (unlike the first one, which needed the doubleSided hack to stop its back side being invisible).
            // Explicit front: SchoolOfficeSetup now cuts a real opening in the wall right behind this hatch, so the
            // AwayFromWall raycast no longer finds a wall here to orient off -- it would fall back to an arbitrary
            // yaw and show the baked SCHOOL OFFICE / Hatch closed face into the office instead of the corridor.
            // flush: pushes the model's back to the fitting box's rear (now extended to the real wall by the hidden
            // "Hatch back reveal" spacer), so the Tripo mesh's own depth can't leave an air gap in front of the wall.
            new Job{models=new[]{"OfficeServingHatch2"},match=t=>t.name=="Corridor hatch",uniform=true,doubleSided=true,front=Vector3.back,flush=true},
            new Job{models=new[]{"OfficePrinterScanner"},match=t=>t.name=="Photocopier",facing=Facing.AwayFromNearestWall,uniform=true,
                extras=t=>t.parent.Cast<Transform>().Where(s=>s.name=="Photocopier lid").Select(s=>s.GetComponent<Renderer>()).Where(r=>r!=null)},
            // Reception counter over the long west run: the model's RECEPTION sign and open screen frame stand in for the box
            // screen (its glass keeps colliding, unseen); the east run past the staff gap, the desk things and the jar stay.
            new Job{models=new[]{"OfficeReceptionCounter"},match=t=>t.name=="Reception counter",front=Vector3.forward,
                keep=p=>new[]{"East counter","East counter top","Bell base","Desk bell","Sign-in book","Counter monitor","STAFF plaque","STAFF"}.Contains(p.name)},
            // Staff pigeonholes: the box and its loose slots and post; wide and tall as the old unit, deeper, back on the wall.
            new Job{models=new[]{"OfficePigeonholes"},match=t=>t.name=="Pigeonholes",uniform=true,flush=true,
                extras=t=>t.parent.Cast<Transform>().Where(s=>(s.name=="Pigeonhole"||s.name=="Post")&&Mathf.Abs(s.position.z-t.position.z)<1f).Select(s=>s.GetComponent<Renderer>()).Where(r=>r!=null)},
            // Four box-built "Filing cabinet" units in a row (their own drawer lines/handles are nested inside this group
            // already, no extras needed) for the user's own bank-of-4 model, in place of four separate single-cabinet swaps.
            new Job{models=new[]{"FilingCabinets"},match=t=>t.name=="Filing cabinet bank",uniform=true},
            // Only the Art Room's own pupil stations (FurnishClass names each room's group "<TITLE> pupil station"):
            // swaps just the desktop+legs for the table model, leaves the chair and the exercise book/pencil dressing.
            new Job{models=new[]{"ArtRoomTable"},match=t=>t.name=="ART ROOM pupil station",uniform=true,facing=Facing.AlongForward,
                keep=p=>new[]{"Chair seat","Chair back","Chair leg","Exercise book","Pencil"}.Contains(p.name)},
        };

        [MenuItem("Confiscated/School Run/Apply Furniture Models (Codex batch 4)")]
        public static void ApplyMenu()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play first.");
            int n=ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[Furniture] "+n+" pieces now use a model.");
        }

        /// <summary>Does not save. Returns how many pieces now use a model. `only` limits it to jobs using those models.</summary>
        static MeshRenderer[] wallCache;
        static MeshRenderer[] AllWalls()=>wallCache??=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r=>r.name.StartsWith("Wall_")).ToArray();
        public static int ApplyToScene(string[] only=null)
        {
            AssetDatabase.Refresh();int count=0;wallCache=null; // walls get destroyed/rebuilt by earlier passes; re-scan each run
            var all=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var job in Jobs)
            {
                if(only!=null&&!job.models.Any(only.Contains))continue;
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
            if(job.extras!=null)parts.AddRange(job.extras(target));
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
            // Largest that fits, standing on the footprint's middle; `flush` leaves depth free (the back goes to the wall below).
            Vector3 front=Quaternion.Euler(0,yaw,0)*Vector3.left;bool frontX=Mathf.Abs(front.x)>Mathf.Abs(front.z);
            if(job.uniform)fit=Vector3.one*(job.flush?Mathf.Min(fit.y,frontX?fit.z:fit.x):Mathf.Min(fit.x,fit.y,fit.z));
            var axes=new[]{holder.right,holder.up,holder.forward};
            for(int w=0;w<3;w++)
            {
                var world=w==0?Vector3.right:w==1?Vector3.up:Vector3.forward;
                int best=0;for(int i=1;i<3;i++)if(Mathf.Abs(Vector3.Dot(axes[i],world))>Mathf.Abs(Vector3.Dot(axes[best],world)))best=i;
                scale[best]*=fit[w];
            }
            holder.localScale=scale;
            var now=Bounds(holder);holder.position+=new Vector3(space.center.x-now.center.x,space.min.y-now.min.y,space.center.z-now.center.z);
            if(job.flush){now=Bounds(holder);var f=frontX?Vector3.right*Mathf.Sign(front.x):Vector3.forward*Mathf.Sign(front.z);float back=Vector3.Dot(space.center,f)-Vector3.Dot(space.extents,Abs(f)),mine=Vector3.Dot(now.center,f)-Vector3.Dot(now.extents,Abs(f));holder.position+=f*(back-mine);}
            // Furniture follows the room's light, like the office props.
            foreach(var r in holder.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;SchoolLightingSetup.ConfigureArtworkMaterial(r.sharedMaterial,false);
                // A thin prop seen from both sides (a hatch, stood between two rooms): the Tripo mesh only has front-facing
                // triangles, so the far side is invisible with back-face culling on.
                if(job.doubleSided){r.sharedMaterial.SetFloat("_Cull",(float)UnityEngine.Rendering.CullMode.Off);EditorUtility.SetDirty(r.sharedMaterial);}
            }
            foreach(var r in parts){r.enabled=false;EditorUtility.SetDirty(r);}
            return true;
        }

        /// <summary>
        /// Every batch-4 model has its front (drawers, doors, vents, the VISITORS panel) on its own -X, width along Z
        /// (checked from four-side renders, Docs/Furniture_Views_*.png). The front faces away from the nearest wall, towards
        /// it (teacher desks), or along the piece's own forward (the moving dinner trolley).
        /// </summary>
        const float NoWall=99;
        static float Yaw(Job job,Bounds space,Transform target)
        {
            bool longX=space.size.x>=space.size.z;
            Vector3 across=longX?Vector3.forward:Vector3.right,front=across;
            Vector3 Authored()
            {
                var f=target.forward;f.y=0;
                // A trolley's front is its long axis; everything else faces across it.
                Vector3 axis=Mathf.Abs(Vector3.Dot(f.normalized,across))>.7f?across:(longX?Vector3.right:Vector3.forward);
                return Mathf.Sign(Vector3.Dot(f,axis))*axis;
            }
            if(job.front.HasValue)front=job.front.Value; // the public side, when no wall says which way it faces
            else if(job.facing!=Facing.AlongForward)
            {
                var from=new Vector3(space.center.x,space.min.y+.5f,space.center.z);
                float Hit(Vector3 d)
                {
                    if(Physics.Raycast(from,d,out var h,4f,~0,QueryTriggerInteraction.Ignore)&&!h.transform.IsChildOf(target))return h.distance;
                    // Some room walls carry no collider; fall back to the wall renderers' own bounds along this ray.
                    float best=NoWall;
                    foreach(var wall in AllWalls())
                    {
                        if(!wall.bounds.IntersectRay(new Ray(from,d),out float dist)||dist>4f)continue;
                        if(dist<best)best=dist;
                    }
                    return best;
                }
                float hitAcross=Hit(across),hitBack=Hit(-across);
                if(hitAcross>=NoWall&&hitBack>=NoWall)
                {
                    // No wall in reach either way (a large or open room): keep whatever yaw the room-furnishing code
                    // deliberately gave it, rather than picking an arbitrary side.
                    front=Authored();
                }
                else
                {
                    var wall=hitAcross<hitBack?across:-across;
                    // Or whichever of the four sides is closest to a wall, whatever the footprint's long side.
                    if(job.facing==Facing.AwayFromNearestWall)wall=new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back}.OrderBy(Hit).First();
                    front=job.facing==Facing.TowardWall?wall:-wall;
                }
            }
            else front=Authored();
            // Turn the model's -X onto `front`: +90 about Y takes -X to +Z, then LookRotation takes +Z to front.
            return (Quaternion.LookRotation(front)*Quaternion.Euler(0,90,0)).eulerAngles.y;
        }

        static Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static Bounds Bounds(Transform root){var rs=root.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    }
}
