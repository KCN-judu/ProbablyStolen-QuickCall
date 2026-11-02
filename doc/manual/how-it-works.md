# How it works

The game already builds a greeting-free dialog for its phone-book quick dial. Quick Call uses that dialog for every call and removes the timers around it.

## Dialing

`PhoneUIManager.PressNumber` waits 2.25 s after each digit for more digits. Once the typed digits form a whole contact number that will answer, and no longer number starts with them, the mod places the call right away, the way the phone book's quick dial does.

## Ringing and pickup

`StartCalling` rolls 1 to 5 s of ringing and `StartPhoneDialogSequence` waits 1.5 s for the pickup. The mod sets both timers to almost nothing. Numbers that don't answer still go through the normal flow, so "no answer" appears at once.

## Greeting lines

Each contact's dialog builder checks `isQuickDialCall` and, when it is set, returns the line with the choices instead of the greeting. For Wild that is `wild2` instead of `wild1` followed by `wild2`. The mod sets the flag around every call. Intro, cooldown and story dialogs have no quick variant, so they play in full.

## Choices

The first line is shown whole instead of typed out, and the choices appear as soon as it is shown, instead of 0.85 s later.

The mod patches only `PhoneUIManager` and `DialoguePhoneUIManager`.

## Tested logic

The rule for dialling at once lives in `src/Dial.Pure.cs`, apart from the code that reads the game: call when the digits form a whole number that will answer and no other contact's number starts with them. `tests/DialTest.cs` checks it outside the game with nothing but the C# compiler, and prints ok or FAIL per case.

Starting is all or nothing: if a method the mod hooks is missing, for example after a game update, everything already set up is taken down again, one line is written to `Player.log` and the mod stays off.
