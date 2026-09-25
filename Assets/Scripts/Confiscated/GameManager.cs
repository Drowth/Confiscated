using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Confiscated
{
    /// <summary>Round state: playing, caught, won. Restart reloads the scene for a clean slate.</summary>
    public class GameManager : MonoBehaviour
    {
        public enum State { Playing, Caught, Won, Detention, Menu }

        public static GameManager Instance { get; private set; }

        [SerializeField] FirstPersonController player;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PhoneRinger phoneRinger;
        [SerializeField] CaretakerAI caretaker;
        public OfficeMission officeMission;
        public DetentionController detention;
        public LockerStorageUI lockerUI;
        public SchoolPeriodController schoolPeriod;
        public bool PhoneStored => interactor != null && interactor.GetComponent<PlayerInventory>() != null && interactor.GetComponent<PlayerInventory>().PhoneStored;

        public State Current { get; private set; } = State.Playing;
        public bool IsPlaying => Current == State.Playing;
        public bool PhoneCollected { get; private set; }
        bool dayStarted;
        static bool retryChase;
        bool caretakerEndedRun;
        float retryAvailableAt;
        const float CreditsDelaySeconds = 6f;
        float creditsAt = -1f;
        bool creditsShown;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            MainEntranceLighting.Install();
            if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if(retryChase && SchoolRunController.Instance != null) { retryChase=false; StartCoroutine(BeginChaseRetry()); return; }
            var menu=GetComponent<SchoolTitleMenu>();
            if(menu!=null&&menu.enabled){Current=State.Menu;menu.Show(this);}
            else BeginSchoolDay();
        }

        public void BeginSchoolDay()
        {
            if(dayStarted)return;dayStarted=true;Current=State.Playing;
            GetComponent<DarkModeController>()?.Begin();
            HudController.Instance?.SetObjective("CONFISCATED!\nGet your phone back from the caretaker's office,\nthen escape through the window you came in.");
            HudController.Instance?.SetPhoneState(HudController.PhoneState.Hidden, 0f);
            if (schoolPeriod != null) schoolPeriod.Begin();
            else if (officeMission != null) officeMission.Begin();
        }

        void Update()
        {
            if (PauseMenu.IsOpen || ComicDialogue.IsActive || Current == State.Menu || Current == State.Playing || Current == State.Detention) return;
            var kb = Keyboard.current;
            if (Time.unscaledTime < retryAvailableAt) return;
            if(kb!=null&&kb.mKey.wasPressedThisFrame){ReturnToTitle();return;}
            if (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                Restart();
            if (!creditsShown && creditsAt >= 0 && Time.unscaledTime >= creditsAt)
            {
                creditsShown = true;
                HudController.Instance?.ShowOverlay("THANKS FOR PLAYING", "A game by Lee Grieve\nTesters: Jacob Grieve and Elliott King\n\nThanks for playing CONFISCATED!\n\nR: retry   M: title / game modes"+(SchoolGameMode.DarkUnlocked?"\nDARK MODE UNLOCKED":""));
            }
        }
        void ScheduleCredits() { creditsAt = Time.unscaledTime + CreditsDelaySeconds; creditsShown = false; }

        public void OnPhoneCollected(PlayerInteractor who)
        {
            PhoneCollected = true;
            if (SchoolRunController.Instance != null)
            {
                officeMission?.PhoneRecovered();
                phoneRinger?.Deactivate();
                SchoolRunController.Instance.Recover(0);
                return;
            }
            HudController.Instance?.SetObjective("CONFISCATED!\nYou have your phone. Get back to the window\nbefore it rings...");
            HudController.Instance?.SetStatus("Got it. Now get out before it rings.", 3f);
            HudController.Instance?.SetPhoneState(HudController.PhoneState.Idle, 999f);
            if (phoneRinger != null) phoneRinger.Activate();
            if (officeMission != null)
            {
                officeMission.PhoneRecovered();
                HudController.Instance?.SetStatus("Got it. Back to Year 6 before anyone notices.", 3f);
            }
        }

        AudioSource caughtAudio;
        void PlayCaughtSound()
        {
            if(caughtAudio==null)
            {
                caughtAudio=gameObject.AddComponent<AudioSource>();caughtAudio.playOnAwake=false;
                caughtAudio.loop=false;caughtAudio.spatialBlend=0;caughtAudio.volume=.8f;
                caughtAudio.clip=Resources.Load<AudioClip>("Audio/PlayerCaught");
            }
            if(caughtAudio.clip!=null)caughtAudio.Play();
        }

        public void Caught(CaretakerAI captor = null)
        {
            if (Current != State.Playing) return;
            lockerUI?.Close();
            if (captor != null && (captor == caretaker || captor == SchoolRunController.Instance?.caretaker))
            {
                caretakerEndedRun = true;
                Current = State.Caught;
                EndingUnlocks.Unlock(EndingUnlocks.Ending.Caught);
                retryAvailableAt = Time.unscaledTime + CaretakerCatchScare.Duration;
                SchoolRunController.Instance?.PauseStaff();
                schoolPeriod?.StopAllCoroutines();
                schoolPeriod?.worksheetUI.Close();
                EndRound();
                CaretakerCatchScare.Play(captor);
                PlayCaughtSound();
                HudController.Instance?.SetStatus(null);
                HudController.Instance?.SetObjective("RUN ENDED");
                int count = SchoolRunController.Instance != null ? SchoolRunController.Instance.Count : 0;
                PrepareTimedResultsLayout();
                HudController.Instance?.ShowOverlay("CAUGHT!", "The caretaker caught you.\nBelongings recovered: " + count + " / 5\n" + (SchoolRunController.Instance?.Timing.Result(false) ?? "") + "\n\nR / Enter: retry from the classroom   M: title / game modes");
                CaretakerCaughtResults.Show(HudController.Instance);
                ScheduleCredits();
                return;
            }
            if (detention != null && detention.Ready)
            {
                SchoolRunController.Instance?.PenalizeCatch();
                SchoolRunController.Instance?.PauseStaff();
                Current = State.Detention;
                caretaker?.Freeze();
                phoneRinger?.Deactivate();
                if (interactor.HeldBall != null) interactor.HeldBall.Throw(Vector3.zero);
                if (SchoolRunController.Instance == null && (PhoneCollected || interactor.HasPhone))
                {
                    PhoneCollected = false;
                    interactor.HasPhone = false;
                    interactor.GetComponent<PlayerInventory>()?.RemoveCarriedPhone();
                    detention.phonePickup.ReturnToOffice();
                    officeMission?.PhoneConfiscated();
                }
                detention.Begin(interactor, player);
                PlayCaughtSound();
                return;
            }
            Current = State.Caught;
            EndRound();
            PlayCaughtSound();
            HudController.Instance?.ShowOverlay("CAUGHT!", "\"That belongs in my office.\"\n\nPress R to try again");
        }

        public void FinishDetention()
        {
            if (Current != State.Detention) return;
            Current = State.Playing;
            officeMission?.RefreshObjective();
            caretaker?.ResumeAfterDetention(8f);
            SchoolRunController.Instance?.ResumeStaff();

        }

        public void SetPhoneLocation(bool carried, bool stored)
        {
            bool wasCarried=PhoneCollected;
            PhoneCollected=carried;interactor.HasPhone=carried;
            if(carried&&!wasCarried)phoneRinger?.Activate();
            else if(!carried)phoneRinger?.Deactivate();
            if(stored)officeMission?.PhoneStored();
            else if(carried)officeMission?.PhoneRecovered();
        }

        public void Win()
        {
            if(schoolPeriod!=null){schoolPeriod.ReturnToClass();return;}
            if (Current != State.Playing) return;
            if (officeMission != null && !officeMission.ReadyToFinish) return;
            Current = State.Won;
            EndRound();
            var schoolBells=Object.FindFirstObjectByType<SchoolBellSystem>();
            if(officeMission!=null && schoolBells!=null)schoolBells.Ring();
            else TempAudio.PlayAt(TempAudio.Win, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.8f);
            HudController.Instance?.ShowOverlay("LEGGED IT!", "You got your phone back and escaped.\n\nPress R to play again");
            if (officeMission != null)
                HudController.Instance?.ShowOverlay("BACK IN CLASS", "Phone hidden. Book open. Act normal.\nYou made it back before the lesson began.\n\nPress R to play again");
        }

        const long SpeedEscapeMs = 150000; // under 2:30 - a practiced, no-wasted-moves run
        const int EasterEggDecoysUsed = 3; // every wind-up toy you could carry, all wound and left behind
        static AudioClip quackVictoryClip, noDucksVictoryClip;
        public void CompleteSchoolRun(string report)
        {
            if (!IsPlaying || SchoolRunController.Instance == null || !SchoolRunController.Instance.ReadyToEscape) return;
            var timing = SchoolRunController.Instance.Timing;
            report += "\n" + timing.Result(true);
            long ms = timing.Milliseconds;
            Current = State.Won; EndRound();
            HudController.Instance?.SetObjective("ALL FIVE RECOVERED");
            PrepareTimedResultsLayout();
            string title, flavour;
            if (ClockworkDecoy.TotalDeployedThisRun >= EasterEggDecoysUsed)
            {
                title = "QUACK ESCAPE!";
                flavour = "SECRET ENDING UNLOCKED\nEvery wind-up duck in the building, wound and abandoned behind you.\nYou earned this one by using every single duck.";
                EndingUnlocks.Unlock(EndingUnlocks.Ending.QuackEscape);
                if (quackVictoryClip == null) quackVictoryClip = Resources.Load<AudioClip>("Audio/ThreeLittleDucks");
                if (quackVictoryClip != null) TempAudio.PlayAt(quackVictoryClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
            }
            else if (ms <= SpeedEscapeMs)
            {
                title = "SPEED DEMON!";
                flavour = "Out the door before the ink dried on his detention slip.";
                EndingUnlocks.Unlock(EndingUnlocks.Ending.FastRun);
            }
            else if (ClockworkDecoy.TotalDeployedThisRun == 0)
            {
                title = "LEGGED IT!";
                flavour = "NO DUCKS WERE HARMED IN THE MAKING OF THIS ESCAPE.\n(They weren't even wound up. Frankly, they're a little offended.)";
                EndingUnlocks.Unlock(EndingUnlocks.Ending.Completed);
                if (noDucksVictoryClip == null) noDucksVictoryClip = Resources.Load<AudioClip>("Audio/QuackQuack");
                if (noDucksVictoryClip != null) TempAudio.PlayAt(noDucksVictoryClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
            }
            else
            {
                title = "LEGGED IT!";
                flavour = "You got your phone back and escaped.";
                EndingUnlocks.Unlock(EndingUnlocks.Ending.Completed);
            }
            HudController.Instance?.ShowOverlay(title, flavour + "\n" + report + "\n\nR: retry   M: title / game modes\n"+(SchoolGameMode.Dark?"DARK MODE COMPLETE":"DARK MODE UNLOCKED - select it on the title screen."));
            // After the time is frozen and the escape validated: exactly one upload per win. Caught runs never reach here.
            var hud = HudController.Instance;
            if (hud != null && hud.overlay != null) SteamLeaderboardView.Show(hud.overlay.transform, hud.overlayBody);
            SteamLeaderboard.Submit(timing.Category, ms);
            ScheduleCredits();
        }

        public void CompleteSchoolPeriod(string report)
        {
            if(schoolPeriod==null||!schoolPeriod.IsComplete||Current!=State.Playing)return;
            Current=State.Won;EndRound();
            Object.FindFirstObjectByType<SchoolBellSystem>()?.Ring();
            var hud=HudController.Instance;
            if(hud!=null)
            {
                hud.SetStatus(null);hud.SetObjective("FIRST PERIOD COMPLETE");
                if(hud.overlayTitle!=null)hud.overlayTitle.rectTransform.anchoredPosition=new Vector2(0,180);
                if(hud.overlayBody!=null){hud.overlayBody.rectTransform.anchoredPosition=new Vector2(0,-60);hud.overlayBody.rectTransform.sizeDelta=new Vector2(1400,300);}
            }
            HudController.Instance?.ShowOverlay("END OF ENGLISH",report+"\n\nFirst period complete.\nPress R to replay this period.");
        }

        void EndRound()
        {
            lockerUI?.Close();
            if (player != null) player.enabled = false;
            if (interactor != null) interactor.InputLocked = true;
            if (caretaker != null) caretaker.Freeze();
            if (phoneRinger != null) phoneRinger.Deactivate();
            HudController.Instance?.SetPrompt(null);
            HudController.Instance?.SetHoldProgress(-1f);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void PrepareTimedResultsLayout()
        {
            var hud=HudController.Instance;if(hud==null)return;
            if(hud.overlayTitle!=null)hud.overlayTitle.rectTransform.anchoredPosition=new Vector2(0,240);
            if(hud.overlayBody!=null){hud.overlayBody.rectTransform.anchoredPosition=new Vector2(0,-30);hud.overlayBody.rectTransform.sizeDelta=new Vector2(1400,440);}
        }

        System.Collections.IEnumerator BeginChaseRetry()
        {
            // Let scene components initialize before replacing the classroom introduction.
            yield return null;
            BeginSchoolDay();
            schoolPeriod.PrepareChaseRetry();
            SchoolRunController.Instance.PrepareChaseRetry();
            GetComponent<DarkModeController>()?.PrepareRetry();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Restart()
        {
            retryChase = caretakerEndedRun || (Current == State.Won && SchoolRunController.Instance != null);
            ComicDialogue.Cancel();
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
        /// <summary>From the pause menu: straight back into a fresh chase from the classroom, skipping the title and the lesson.</summary>
        public void RestartRun(){Restart();retryChase=SchoolRunController.Instance!=null;}
        public void ReturnToTitle()
        {
            retryChase=false;SchoolGameMode.Select(false);ComicDialogue.Cancel();Time.timeScale=1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
    }
}
