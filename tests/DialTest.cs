// Checks when typed digits are dialled at once (src/Dial.Pure.cs), outside the game.
//   csc -out:DialTest.exe tests\DialTest.cs src\Dial.Pure.cs && DialTest.exe
using System;
using QuickCall;

static class DialTest
{
    static int failed;

    static void Check(string what, bool expected, bool got)
    {
        if (got != expected) failed++;
        Console.WriteLine((got == expected ? "ok    " : "FAIL  ") + what);
    }

    static int Main()
    {
        var contacts = new long[] { 4471, 447123, 5550, 81 };
        Check("a whole number that answers dials at once", true, Dial.ShouldDialNow(5550, true, contacts));
        Check("a number that does not answer waits", false, Dial.ShouldDialNow(5550, false, contacts));
        Check("the start of a longer contact number waits", false, Dial.ShouldDialNow(4471, true, contacts));
        Check("the longer number itself dials at once", true, Dial.ShouldDialNow(447123, true, contacts));
        Check("a short number dials at once when nothing else starts with it", true, Dial.ShouldDialNow(81, true, contacts));
        Check("digits that are only part of a number wait", false, Dial.ShouldDialNow(44, true, contacts));
        Check("zero never dials", false, Dial.ShouldDialNow(0, true, contacts));
        Check("no contact list: a number that answers dials", true, Dial.ShouldDialNow(4471, true, null));
        Check("a number is not a prefix of itself", false, Dial.IsPrefixOfOther(5550, new long[] { 5550 }));
        Check("a prefix is matched on digits, not value", true, Dial.IsPrefixOfOther(12, new long[] { 1299 }));
        Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
