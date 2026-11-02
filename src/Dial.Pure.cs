using System;
using System.Collections.Generic;

namespace QuickCall
{
    // When typed digits are dialled at once, on plain data only (System types, no Unity, no game objects), so
    // tests/DialTest.cs can run it outside the game.
    public static class Dial
    {
        // Call now when the digits form a whole number that will answer and no other contact's number starts
        // with them: then no further digit can change which contact is meant.
        public static bool ShouldDialNow(long number, bool answers, IEnumerable<long> contacts)
        {
            return number > 0 && answers && !IsPrefixOfOther(number, contacts);
        }

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
