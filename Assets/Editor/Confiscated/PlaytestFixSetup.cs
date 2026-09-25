using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>
    /// Fixes from the 2026-09-19 playtest (Docs/Playtest_2026-09-19.md). Chained into SchoolRunSetup.Build.
    /// 1. Locked-doorway barriers no longer hide their door from the player's interaction ray.
    /// 2. The main entrance doors are the exit: shut for the run, Hold F on them with 5/5 to escape. No separate plate.
    /// 3. The office key is mounted to the moving trolley tray and the hold bar has reliable fill geometry.
    /// </summary>
    public static class PlaytestFixSetup
    {
        public const string BarrierName="Locked doorway collision", MainDoorName="South yard doors", ExitBlockerName="Door blocker - "+MainDoorName;
        public const int IgnoreRaycastLayer=2;

        [MenuItem("Confiscated/School Run/Apply Playtest Fixes")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing.");
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("[PlaytestFix] Door barriers, main exit, moving trolley key and hold-progress display repaired.");
        }

        public static void ApplyToScene()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            if(run==null)throw new InvalidOperationException("Open the SchoolLayout scene first.");

            foreach(var barrier in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name==BarrierName))
            {barrier.gameObject.layer=IgnoreRaycastLayer;EditorUtility.SetDirty(barrier.gameObject);}

            foreach(var plate in Object.FindObjectsByType<RunGate>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(g=>g.kind==RunGate.Kind.Exit).ToArray())
                Object.DestroyImmediate(plate.gameObject);

            var door=Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).FirstOrDefault(d=>d.name==MainDoorName);
            if(door==null)throw new InvalidOperationException(MainDoorName+" is missing.");
            door.mainExit=true;door.holdSeconds=1;
            EditorUtility.SetDirty(door);PrefabUtility.RecordPrefabInstancePropertyModifications(door);

            // Shut for the whole run, so staff must never plan a route through it.
            var old=run.transform.Find(ExitBlockerName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var blocker=new GameObject(ExitBlockerName).transform;blocker.SetParent(run.transform,false);
            blocker.SetPositionAndRotation(door.transform.position,door.transform.rotation);
            var obstacle=blocker.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
            obstacle.center=new Vector3(0,1.1f,0);obstacle.size=new Vector3(2.6f,2.2f,.5f);obstacle.carving=true;obstacle.carveOnlyStationary=false;

            // A padlock on the inside of the meeting stile: it drops when all five belongings (and the phone) are in hand.
            var feedback=door.GetComponent<ProgressPropFeedback>();
            if(feedback!=null&&feedback.padlock!=null)Object.DestroyImmediate(feedback.padlock.gameObject);
            if(feedback==null)feedback=door.gameObject.AddComponent<ProgressPropFeedback>();
            feedback.door=door;
            var frame=door.hinge.Find("Entrance glazing");
            Transform parent=frame!=null?frame:door.hinge;
            float inside=Mathf.Sign(parent.InverseTransformDirection(Vector3.forward).z);if(inside==0)inside=1;
            ChaseFeedbackSetup.Lock(feedback,parent,frame!=null?new Vector3(.57f,-.02f,inside*.08f):new Vector3(.8f,.9f,inside*.067f));
            // Lock() models the padlock facing -z; turn it to face the corridor.
            if(inside>0)feedback.padlock.localRotation=Quaternion.Euler(0,180,0);
            EditorUtility.SetDirty(feedback);

            MountKeyOnTrolley(run);
            RepairHoldBar();
        }

        static void MountKeyOnTrolley(SchoolRunController run)
        {
            var cart=run.trolley!=null?run.trolley:GameObject.Find("School/Details/Spare key trolley")?.transform;
            var trolley=cart!=null?cart.Find("MaintenanceTrolley"):null;
            var key=Object.FindFirstObjectByType<OfficeKeyPickup>(FindObjectsInactive.Include);
            if(trolley==null||key==null)throw new InvalidOperationException("Moving trolley or its office key is missing.");
            key.transform.SetParent(trolley,false);
            key.transform.localPosition=new Vector3(.06f,.875f,-.08f);
            key.transform.localRotation=Quaternion.Euler(90,180,0);
            key.transform.localScale=Vector3.one;
            EditorUtility.SetDirty(key.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(key.transform);
        }

        static void RepairHoldBar()
        {
            var hud=Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
            if(hud==null)throw new InvalidOperationException("HUD is missing.");
            if(hud.holdBar==null)
            {
                var bar=GameObject.Find("HUD/HoldBarBg/HoldBar");
                if(bar!=null)hud.holdBar=bar.GetComponent<UnityEngine.UI.Image>();
            }
            if(hud.holdBar==null)throw new InvalidOperationException("HUD hold-progress bar is missing.");
            hud.ConfigureHoldBar();
            EditorUtility.SetDirty(hud.holdBar);EditorUtility.SetDirty(hud);
        }

        [MenuItem("Confiscated/Tests/Validate Moving Key and Hold Progress")]
        public static void ValidateInteractionPolish()
        {
            var run=Object.FindFirstObjectByType<SchoolRunController>();
            var key=Object.FindFirstObjectByType<OfficeKeyPickup>(FindObjectsInactive.Include);
            var trolley=run!=null&&run.trolley!=null?run.trolley.Find("MaintenanceTrolley"):null;
            if(run==null||key==null||trolley==null||key.transform.parent!=trolley)
                throw new InvalidOperationException("Office key is not mounted to the moving trolley.");
            Vector3 originalPosition=run.trolley.position;Quaternion originalRotation=run.trolley.rotation;
            Vector3 mountedLocal=key.transform.localPosition;
            try
            {
                run.trolley.SetPositionAndRotation(originalPosition+new Vector3(1.7f,0,-.8f),originalRotation*Quaternion.Euler(0,83,0));
                if(Vector3.Distance(key.transform.position,trolley.TransformPoint(mountedLocal))>.001f)
                    throw new InvalidOperationException("Office key did not follow trolley motion and rotation.");
            }
            finally{run.trolley.SetPositionAndRotation(originalPosition,originalRotation);}

            var hud=Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
            if(hud==null||hud.holdBar==null)throw new InvalidOperationException("Hold-progress UI is missing.");
            hud.SetHoldProgress(.5f);
            bool halfShown=hud.holdBar.transform.parent.gameObject.activeSelf&&Mathf.Abs(hud.holdBar.rectTransform.localScale.x-.5f)<.001f;
            hud.SetHoldProgress(-1f);
            if(!halfShown||hud.holdBar.transform.parent.gameObject.activeSelf)
                throw new InvalidOperationException("Hold-progress UI did not show a half fill and then hide.");
            Debug.Log("[InteractionPolishTest] PASS: mounted key follows translated/rotated trolley; hold bar shows 50% fill and hides cleanly.");
        }
    }
}
