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

        public void OnEnable()
        {
            if (harmony != null) return;
            setQuickDial = typeof(PhoneUIManager).GetMethod("set_isQuickDialCall", AnyInstance);
            isAnyBubbleTyping = typeof(DialoguePhoneUIManager).GetMethod("IsAnyBubbleTyping", AnyInstance);
            skipTypewriters = typeof(DialoguePhoneUIManager).GetMethod("SkipTypewriters", AnyInstance);

            // Types are named at each call: the game's mod scanner asks for a permission when reflection
            // goes through a Type it cannot see.
            harmony = new Harmony(HarmonyId);
            harmony.Patch(typeof(PhoneUIManager).GetMethod("PressNumber", AnyInstance),
                Hook("PressNumberPrefix"), Hook("PressNumberPostfix"));
            harmony.Patch(typeof(PhoneUIManager).GetMethod("StartCalling", AnyInstance),
                null, Hook("StartCallingPostfix"));
            harmony.Patch(typeof(PhoneUIManager).GetMethod("StartPhoneDialogSequence", AnyInstance),
                null, Hook("StartPhoneDialogSequencePostfix"));
            harmony.Patch(typeof(PhoneUIManager).GetMethod("StartPhoneDialog", AnyInstance),
                Hook("StartPhoneDialogPrefix"), Hook("StartPhoneDialogPostfix"));
            harmony.Patch(typeof(DialoguePhoneUIManager).GetMethod("OnCurrentTextDisplayed", AnyInstance),
                Hook("OnCurrentTextDisplayedPrefix"));
            harmony.Patch(typeof(DialoguePhoneUIManager).GetMethod("InterruptDialog", AnyInstance),
                null, Hook("InterruptDialogPostfix"));
        }

        public void OnDisable()
        {
            if (harmony != null) { harmony.UnpatchAll(HarmonyId); harmony = null; }
            openingLine = false;
        }

        static HarmonyMethod Hook(string name)
        {
            return new HarmonyMethod(typeof(QuickCallMod).GetMethod(name, AnyStatic));
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
            if (!PhoneUIManager.WillAnswerCall(___currentNumber) || IsPrefixOfOtherNumber(___currentNumber)) return;
            __instance.AutoCall(___currentNumber);
        }

        static bool IsPrefixOfOtherNumber(long number)
        {
            var store = PlayerStore.Instance;
            if (store == null || store.PhoneClientDict == null) return false;
            string digits = number.ToString();
            foreach (long other in store.PhoneClientDict.Keys)
                if (other != number && other.ToString().StartsWith(digits, StringComparison.Ordinal)) return true;
            return false;
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
