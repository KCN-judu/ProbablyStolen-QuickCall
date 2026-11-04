using System;
using System.Reflection;
using HarmonyLib;

namespace QuickCall
{
    // Phone calls go straight to the choices: no wait after the last digit, no ringing, no pickup pause,
    // no greeting lines and no pause before the choices appear.
    // The game already builds a greeting-free dialog for its quick dial (isQuickDialCall); every call uses it.
    // Intro, cooldown and story calls have no quick variant and play unchanged.
    //
    // Game types patched (Harmony prefixes and postfixes; no method is replaced outright):
    //   PhoneUIManager.PressNumber               dial as soon as the digits form a whole number
    //   PhoneUIManager.StartCalling              ringing time
    //   PhoneUIManager.StartPhoneDialogSequence  pickup pause
    //   PhoneUIManager.StartPhoneDialog          sets isQuickDialCall while the call's dialog is built
    //   DialoguePhoneUIManager.OnCurrentTextDisplayed, InterruptDialog   show the choices with the first line
    // Read only: PlayerStore.Instance.PhoneClientDict (the contact numbers) and PhoneUIManager.WillAnswerCall.
    // The rule for dialling at once is in Dial.Pure.cs; tests/DialTest.cs checks it.
    //
    // Code standard: the mod keeps no store objects. Its statics hold only reflection handles, the Harmony
    // instance and one bool, so nothing outlives the store and there is nothing to release on a scene change.
    public sealed class QuickCallMod : IMod
    {
        public const string ModId = "quick_call";
        const string HarmonyId = "mod." + ModId;

        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Non-null while the patches are installed; OnEnable uses it to ignore a second enable.
        static Harmony harmony;
        // Game members the patches call by reflection: the isQuickDialCall setter and the two
        // DialoguePhoneUIManager methods that finish the typewriter. Looked up once in Install. A null handle
        // switches off only the feature that needs it.
        static MethodInfo setQuickDial, isAnyBubbleTyping, skipTypewriters;

        // Set while a call's first line is shown, so its choices appear as soon as the line is.
        // Cleared when that line is displayed, when the dialog is interrupted and when the mod is taken down,
        // so a later dialog (a story call, say) never gets the shortcut.
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

        // Finds the game members and applies every patch. Throws when a target is missing; OnEnable catches that
        // and calls TearDown, so a game update that renames a method leaves the mod off instead of half on.
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

        // Patches one method; fails with its name when the game no longer has it (GetMethod returned null).
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

        // The mod's own patch method by name, so Install can list each game method literally. A typo or rename
        // throws here and is handled like a missing game method.
        static HarmonyMethod Hook(string name)
        {
            var m = typeof(QuickCallMod).GetMethod(name, AnyStatic);
            if (m == null) throw new MissingMethodException("QuickCallMod." + name + " not found");
            return new HarmonyMethod(m);
        }

        // Typed digits: as soon as they form a whole contact number that answers, call it the way the
        // phone book's quick dial does instead of waiting 2.25 s for more digits.
        // Prefix: remembers whether this key press counts (a digit, and no call in progress), because the
        // game's state has changed by the time the postfix runs. Harmony passes a private field by its name
        // with three leading underscores (___isTalking) and hands __state on to the postfix.
        static void PressNumberPrefix(int number, bool ___isTalking, out bool __state)
        {
            __state = number >= 0 && !___isTalking;
        }

        // Postfix: runs after the game added the digit to currentNumber. AutoCall is the game's own call entry
        // for the phone book's quick dial. The contact numbers are read live from PlayerStore and not kept.
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
        // callTimerRoll is the ringing time the game just rolled (1 to 5 s); the mod overwrites it with zero.
        static void StartCallingPostfix(ref float ___callTimerRoll)
        {
            ___callTimerRoll = 0f;
        }

        // sequenceTimer is the pickup wait (1.5 s); the mod overwrites it with 0.01 s.
        static void StartPhoneDialogSequencePostfix(ref float ___sequenceTimer)
        {
            ___sequenceTimer = 0.01f;
        }

        // Turns isQuickDialCall on while this call's dialog is built: the contact's dialog builder reads the
        // flag then and returns the line with the choices instead of the greeting. The flag is set through
        // its setter by reflection and is switched off again in the postfix.
        static void StartPhoneDialogPrefix(PhoneUIManager __instance)
        {
            if (setQuickDial != null) setQuickDial.Invoke(__instance, new object[] { true });
            openingLine = true;
        }

        // Switches the flag off and, if the call is talking, shows the first line whole. SkipTypewriters
        // finishes the typewriter, after which the game calls OnCurrentTextDisplayed (patched below).
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

        // Called when the opening line has been displayed. The game shows the choices 0.85 s later; for a
        // choice line without an end action the mod shows them now and skips the original. In every other
        // case (no choice, or an end action such as a story step) the original runs unchanged.
        // The two ref fields are the game's marks for a line still being typed; they are reset here before
        // the choices are shown.
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

        // A dialog the game cuts short must not leave the shortcut armed for the next one.
        static void InterruptDialogPostfix()
        {
            openingLine = false;
        }
    }
}
