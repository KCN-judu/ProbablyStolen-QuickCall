using System;
using System.Reflection;
using HarmonyLib;

namespace QuickCall
{
    // Phone calls go straight to the choices: no wait after the last digit, no ringing, no pickup pause,
    // no greeting lines and no pause before the choices appear.
    // The game already builds a greeting-free dialog for its quick dial (isQuickDialCall); every call uses it.
    // Intro, cooldown and story calls have no quick variant and play unchanged.
    public sealed class QuickCallMod : IMod
    {
        public const string ModId = "quick_call";
        const string HarmonyId = "mod." + ModId;

        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static Harmony harmony;
        static MethodInfo setQuickDial, isAnyBubbleTyping, skipTypewriters;

        // Set while a call's first line is shown, so its choices appear as soon as the line is.
        static bool openingLine;

        public void Init(ModManifest manifest) { }

        // Enabling is all or nothing: if a patch target is missing (after a game update, say) or any step fails,
        // everything already installed is taken down again, one line goes to Player.log and the mod stays off.
        public void OnEnable()
        {
            if (harmony != null) return;
            try { Install(); }
            catch (Exception e)
            {
                TearDown();
                UnityEngine.Debug.LogWarning("[QuickCall] could not start, left off: " + e.Message);
            }
        }

        public void OnDisable()
        {
            TearDown();
        }

        static void Install()
        {
            setQuickDial = typeof(PhoneUIManager).GetMethod("set_isQuickDialCall", AnyInstance);
            isAnyBubbleTyping = typeof(DialoguePhoneUIManager).GetMethod("IsAnyBubbleTyping", AnyInstance);
            skipTypewriters = typeof(DialoguePhoneUIManager).GetMethod("SkipTypewriters", AnyInstance);

            // Types are named at each call: the game's mod scanner asks for a permission when reflection
            // goes through a Type it cannot see.
            var h = new Harmony(HarmonyId);
            harmony = h;
            Patch(h, typeof(PhoneUIManager).GetMethod("PressNumber", AnyInstance), "PhoneUIManager.PressNumber",
                Hook("PressNumberPrefix"), Hook("PressNumberPostfix"));
            Patch(h, typeof(PhoneUIManager).GetMethod("StartCalling", AnyInstance), "PhoneUIManager.StartCalling",
                null, Hook("StartCallingPostfix"));
            Patch(h, typeof(PhoneUIManager).GetMethod("StartPhoneDialogSequence", AnyInstance), "PhoneUIManager.StartPhoneDialogSequence",
                null, Hook("StartPhoneDialogSequencePostfix"));
            Patch(h, typeof(PhoneUIManager).GetMethod("StartPhoneDialog", AnyInstance), "PhoneUIManager.StartPhoneDialog",
                Hook("StartPhoneDialogPrefix"), Hook("StartPhoneDialogPostfix"));
            Patch(h, typeof(DialoguePhoneUIManager).GetMethod("OnCurrentTextDisplayed", AnyInstance), "DialoguePhoneUIManager.OnCurrentTextDisplayed",
                Hook("OnCurrentTextDisplayedPrefix"), null);
            Patch(h, typeof(DialoguePhoneUIManager).GetMethod("InterruptDialog", AnyInstance), "DialoguePhoneUIManager.InterruptDialog",
                null, Hook("InterruptDialogPostfix"));
        }

        static void Patch(Harmony h, MethodInfo target, string name, HarmonyMethod prefix, HarmonyMethod postfix)
        {
            if (target == null) throw new MissingMethodException(name + " not found");
            h.Patch(target, prefix, postfix);
        }

        // Takes down everything the mod installed; safe to call at any point, also mid-install.
        static void TearDown()
        {
            if (harmony != null)
            {
                try { harmony.UnpatchAll(HarmonyId); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[QuickCall] unpatching failed: " + e.Message); }
                harmony = null;
            }
            openingLine = false;
        }

        static HarmonyMethod Hook(string name)
        {
            var m = typeof(QuickCallMod).GetMethod(name, AnyStatic);
            if (m == null) throw new MissingMethodException("QuickCallMod." + name + " not found");
            return new HarmonyMethod(m);
        }

        // Typed digits: as soon as they form a whole contact number that answers, call it the way the
        // phone book's quick dial does instead of waiting 2.25 s for more digits.
        static void PressNumberPrefix(int number, bool ___isTalking, out bool __state)
        {
            __state = number >= 0 && !___isTalking;
        }

        static void PressNumberPostfix(PhoneUIManager __instance, bool __state, long ___currentNumber)
        {
            if (!__state || ___currentNumber <= 0) return;
            var store = PlayerStore.Instance;
            var contacts = store != null && store.PhoneClientDict != null ? store.PhoneClientDict.Keys : null;
            if (!Dial.ShouldDialNow(___currentNumber, PhoneUIManager.WillAnswerCall(___currentNumber), contacts)) return;
            __instance.AutoCall(___currentNumber);
        }

        // Numbers that do not answer (or are typed in full only after a pause) still go through the
        // normal flow, minus the ringing and the pickup pause.
        static void StartCallingPostfix(ref float ___callTimerRoll)
        {
            ___callTimerRoll = 0f;
        }

        static void StartPhoneDialogSequencePostfix(ref float ___sequenceTimer)
        {
            ___sequenceTimer = 0.01f;
        }

        static void StartPhoneDialogPrefix(PhoneUIManager __instance)
        {
            if (setQuickDial != null) setQuickDial.Invoke(__instance, new object[] { true });
            openingLine = true;
        }

        static void StartPhoneDialogPostfix(PhoneUIManager __instance, bool ___isTalking)
        {
            if (setQuickDial != null) setQuickDial.Invoke(__instance, new object[] { false });
            if (!___isTalking) { openingLine = false; return; }
            // Show the whole first line at once; the typewriter then reports it displayed.
            var dialog = DialoguePhoneUIManager.Instance;
            if (dialog != null && openingLine && isAnyBubbleTyping != null && skipTypewriters != null &&
                (bool)isAnyBubbleTyping.Invoke(dialog, null))
                skipTypewriters.Invoke(dialog, null);
        }

        static bool OnCurrentTextDisplayedPrefix(DialoguePhoneUIManager __instance, Dialogue ___currentDialog,
            ref Dialogue ___displayingUncompletedDialog, ref bool ___isSkipCurrentDialog)
        {
            if (!openingLine) return true;
            openingLine = false;
            if (___currentDialog == null || ___currentDialog.endAction != null || !__instance.IsChoice()) return true;
            ___displayingUncompletedDialog = null;
            ___isSkipCurrentDialog = false;
            __instance.DisplayChoice();
            return false;
        }

        static void InterruptDialogPostfix()
        {
            openingLine = false;
        }
    }
}
