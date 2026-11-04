using System;
using System.Collections.Generic;

namespace QuickCall
{
    // The "Pure" half of the mod (code standard: decisions live in .Pure.cs files with headless tests).
    // It decides when typed digits are dialled at once, on plain data only (System types, no Unity, no game
    // objects), so tests/DialTest.cs can compile and run it outside the game.
    // QuickCallMod.PressNumberPostfix feeds it the digits typed so far in PhoneUIManager, whether the game says
    // that number answers (PhoneUIManager.WillAnswerCall) and the keys of PlayerStore.PhoneClientDict.
    public static class Dial
    {
        // Call now when the digits form a whole number that will answer and no other contact's number starts
        // with them: then no further digit can change which contact is meant.
        public static bool ShouldDialNow(long number, bool answers, IEnumerable<long> contacts)
        {
            return number > 0 && answers && !IsPrefixOfOther(number, contacts);
        }

        // True when another number in the list starts with the same digits. Compared as text: 12 is a prefix
        // of 1299 but not of 2100. A null list means no other contacts. A number is never a prefix of itself,
        // so the list may contain it.
        public static bool IsPrefixOfOther(long number, IEnumerable<long> numbers)
        {
            if (numbers == null) return false;
            string digits = number.ToString();
            foreach (long other in numbers)
                if (other != number && other.ToString().StartsWith(digits, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
