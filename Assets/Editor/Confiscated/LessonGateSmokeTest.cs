using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Confiscated.EditorTools
{
    /// <summary>West-wing errand containment, then a fully open school once the run begins.</summary>
    [InitializeOnLoad]
    public static class LessonGateSmokeTest
    {
        const string Marker = "Temp/run_lesson_gates", Report = "../Docs/LessonGates_Validation.txt";
        static int stage, errors, lastFrame;
        static double started, at;
        static bool background;
        static SchoolRunController R => Object.FindFirstObjectByType<SchoolRunController>();
        static SchoolPeriodController S => R.period;
        static PlayerInteractor P => S.Player;
        static FirstPersonController F => P.GetComponent<FirstPersonController>();
        static LessonCorridorDoors[] Doors => Object.FindObjectsByType<LessonCorridorDoors>(FindObjectsSortMode.None);
        static readonly Vector3 Classroom = new Vector3(-19.6f, 0, 29);

        static LessonGateSmokeTest() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); Begin(); } }; }
        [MenuItem("Confiscated/School Run/Arm Lesson Gate Test")]
        public static void Arm() { File.WriteAllText(Marker, "armed"); }

        static void Begin()
        {
            stage = errors = 0; lastFrame = -1; started = at = EditorApplication.timeSinceStartup;
            background = Application.runInBackground; Application.runInBackground = true;
            File.WriteAllText(Report, "Lesson gates: real scene, natural caretaker navigation with gates shut, player collider passage checks.\n");
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) { errors++; File.AppendAllText(Report, "ERROR " + message + "\n"); } }
        static void Need(bool value, string label) { File.AppendAllText(Report, (value ? "ok   " : "FAIL ") + label + "\n"); if (!value) throw new Exception(label); }
        static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
        static void Warp(Vector3 p) { F.Controller.enabled = false; P.transform.position = p; F.Controller.enabled = true; Physics.SyncTransforms(); }
        static OfficeDoor Door(string name) => Object.FindObjectsByType<OfficeDoor>(FindObjectsSortMode.None).First(d => d.name == name);

        static bool Route(Vector3 from, Vector3 to, out float length)
        {
            length = 0;
            if (!NavMesh.SamplePosition(from, out var a, 2, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 2, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return true;
        }
        static void Reachable(Vector3 from, Vector3 to, string label) { Need(Route(from, to, out float m), label + " reachable (" + m.ToString("F0") + " m)"); }
        static void Sealed(Vector3 from, Vector3 to, string label) { Need(!Route(from, to, out _), label + " sealed off during lessons"); }
        static void Passage(Vector3 center, Vector3 forward, bool open, string label)
        {
            center.y = .05f; Warp(center - forward * 1.3f);
            for (int i = 0; i < 100; i++) F.Controller.Move(forward * .026f);
            float crossed = Vector3.Dot(P.transform.position - center, forward);
            Need((crossed > F.Controller.radius) == open, label + " player collider " + (open ? "passes" : "blocked") + " (beyond threshold: " + crossed.ToString("F2") + " m)");
        }
        static string LookPrompt(Vector3 from, Vector3 target)
        {
            Vector3 eye = from + Vector3.up * 1.2f; target.y = 1.2f;
            if (!Physics.Raycast(eye, (target - eye).normalized, out var hit, 2.6f, ~0, QueryTriggerInteraction.Collide)) return null;
            var found = hit.collider.GetComponentInParent<Interactable>();
            return found != null ? found.GetPrompt(P) : null;
        }
        // Approach points for every run objective, in order.
        static readonly (string label, Vector3 point)[] Objectives =
        {
            ("dining property cage", new Vector3(-10, 0, 47)),
            ("resources corridor", SchoolPlan.Point(659, 467)),
            ("equipment corridor", SchoolPlan.Point(892, 672)),
            ("store approach", SchoolPlan.Point(779, 1092)),
            ("main entrance", SchoolPlan.Point(586, 1167))
        };

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            double now = EditorApplication.timeSinceStartup; if (now - at < .4) return;
            try
            {
                if (now - started > 200) throw new Exception("Timeout at stage " + stage + " phase " + S.Current);
                if (SchoolTitleMenu.IsActive) { SchoolTitleMenu.Instance.StartGame(); SchoolTitleMenu.Instance.SkipIntro(); return; }
                if (ComicDialogue.IsActive) { ComicDialogue.Instance.Advance(); return; }
                switch (stage)
                {
                    case 0:
                        Need(S.Current == SchoolPeriodController.Phase.PhoneRinging && SchoolRunController.LessonsInProgress, "short opening starts during lessons");
                        Need(Doors.Length == LessonGateSetup.Gates.Length, "four corridor lesson gates installed");
                        Need(Doors.All(d => !d.IsOpen && d.barrier.activeSelf), "corridor gates start shut with barriers");
                        Time.timeScale = 4; Next(1); break;
                    case 1:
                        if (!S.IsRoaming) return;
                        Need(!S.worksheetUI.IsOpen, "automatic handoff releases the player into the corridor");
                        R.caretaker.ResumeAfterDetention(999); Warp(Classroom); Next(2); break;
                    case 2:
                        // Errand targets, all inside the west wing.
                        Reachable(Classroom, S.deliveryTray.transform.position, "office drop tray");
                        Reachable(Classroom, SchoolPlan.Point(280, 444), "caretaker office door");
                        Reachable(Classroom, R.trolleyDock.position, "dining hall trolley dock (office key)");
                        Reachable(S.deliveryTray.transform.position, Classroom, "return to Year 6");
                        Reachable(Classroom, SchoolPlan.Point(465, 1100), "south-west loop for evading a pre-run chase");
                        Reachable(Classroom, SchoolPlan.Point(415, 467), "dining corridor with its puddle and dinner trolley");
                        Need(Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None).Length >= 2 && Object.FindFirstObjectByType<DinnerTrolleyPatrol>() != null, "puddles and the dinner trolley are installed");
                        Need(Object.FindObjectsByType<WetFloorHazard>(FindObjectsSortMode.None).All(w => w.transform.Cast<Transform>().Count(t => t.name == "Wet floor sign") == 2 && w.transform.Find("Yellow caution stand") == null), "each puddle has an illustrated sign at both ends and no placeholder stand");
                        Need(!Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t => t.GetComponent<WorldLabel>() == null && t.GetComponentInParent<WetFloorHazard>() != null), "no wet-floor lettering that draws through walls");
                        Need(Object.FindObjectsByType<ProgressPropFeedback>(FindObjectsSortMode.None).Any(f => f.gate != null && f.padlock != null), "cage padlock feedback is installed");
                        foreach (var o in Objectives.Skip(1)) Sealed(Classroom, o.point, o.label);
                        Sealed(Classroom, SchoolPlan.Point(538, 710), "central spine");
                        Sealed(Classroom, SchoolPlan.Point(538, 1000), "art room");
                        foreach (var pp in R.caretaker.patrol) Reachable(R.caretaker.transform.position, pp.point.position, "caretaker routine: " + pp.point.name);
                        foreach (var g in LessonGateSetup.Gates)
                        {
                            Passage(g.Position, -g.wing, false, g.name);
                            Need(LookPrompt(g.Position + g.wing * 1.3f, g.Position) == "Corridor doors are kept shut during lessons.", g.name + " explains itself when looked at");
                        }
                        foreach (string name in LessonGateSetup.LessonDoors)
                        {
                            var d = Door(name); Need(d.LessonLocked && !d.CanInteract(P) && d.GetPrompt(P) == "Closed during lessons.", name + " is shut and says why");
                        }
                        Warp(Classroom); Next(3); break;
                    case 3:
                        if (!S.PhoneDeposited || !R.TrolleyParked) { if (Vector3.Distance(P.transform.position, Classroom) > 1) Warp(Classroom); return; }
                        Need(R.KeyAvailable, "caretaker deposits the phone and parks the trolley by real navigation with every gate shut");
                        R.caretaker.Freeze();
                        var key = Object.FindFirstObjectByType<OfficeKeyPickup>(); var inventory = P.GetComponent<PlayerInventory>();
                        Reachable(Classroom, key.transform.position, "office key on the parked trolley");
                        Warp(key.transform.position + new Vector3(0, -key.transform.position.y, -1.2f)); key.Interact(P);
                        Need(inventory.HasCarried(InventoryItemKind.OfficeKey), "office key taken inside the wing");
                        Need(inventory.Move(InventoryContainer.Satchel, inventory.Find(InventoryContainer.Satchel, InventoryItemKind.OfficeKey), InventoryContainer.Use, 0, out _), "office key equipped");
                        Door("Caretaker office").Interact(P); Need(Door("Caretaker office").IsUnlocked, "office unlocks with the gates shut");
                        Warp(new Vector3(-44.6f, 0, 80.1f)); Need(S.phonePickup.CanInteract(P), "phone recoverable in the office"); S.phonePickup.Interact(P);
                        Need(R.RoundStarted && !SchoolRunController.LessonsInProgress, "phone recovery starts the run and ends lessons");
                        R.caretaker.Freeze(); Next(4); break;
                    case 4:
                        if (now - at < 1.2) return;
                        R.caretaker.Freeze();
                        Need(Doors.All(d => d.IsOpen && !d.barrier.activeSelf), "every corridor gate has swung open and dropped its barrier");
                        foreach (var g in LessonGateSetup.Gates)
                        {
                            var d = Doors.First(x => x.name == g.name);
                            foreach (var hinge in new[] { d.hinge, d.secondHinge })
                            {
                                var leaf = hinge.Find("Leaf"); Vector3 offset = leaf.position - g.Position; offset.y = 0;
                                Need(Vector3.Dot(offset, g.swing) > .3f && Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, g.swing))) > 1f, g.name + " leaf folded clear of the walking line");
                            }
                            Passage(g.Position, -g.wing, true, g.name);
                        }
                        Vector3 office = new Vector3(-44.6f, 0, 80.1f);
                        foreach (var o in Objectives) Reachable(office, o.point, "run objective: " + o.label);
                        foreach (string name in LessonGateSetup.LessonDoors) Need(!Door(name).LessonLocked && Door(name).CanInteract(P), name + " is an ordinary door again");
                        Need(Route(new Vector3(-8.6f, 0, 60), SchoolPlan.Point(538, 657), out float through) && through < 12, "dining east door is a real route again (" + through.ToString("F1") + " m)");
                        foreach (var pp in R.secondStaff.patrol) Reachable(SchoolPlan.Point(659, 874), pp.point.position, "second staff: " + pp.point.name);
                        Need(errors == 0, "no runtime errors"); Finish(true); break;
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL stage " + stage + ": " + e + "\n"); Finish(false); }
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1; Application.runInBackground = background;
            File.AppendAllText(Report, success ? "PASS\n" : "FAIL\n"); EditorApplication.isPlaying = false;
        }
    }
}
