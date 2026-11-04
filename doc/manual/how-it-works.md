# How it works

The game builds a dialog without greeting lines for the phone book's quick dial. Quick Call uses that dialog for every call and sets the waits around it to almost zero.

## Dialing

`PhoneUIManager.PressNumber` waits 2.25 s after each digit for more digits. The mod places the call right away, as the phone book's quick dial does, when the typed digits form a whole contact number that will answer and no longer number starts with them.

## Ringing and pickup

`StartCalling` rolls 1 to 5 s of ringing and `StartPhoneDialogSequence` waits 1.5 s for the pickup. The mod sets both timers to almost zero. Numbers that don't answer still go through the normal flow, so "No Answer" appears at once.

## Greeting lines

Each contact's dialog builder checks `isQuickDialCall`. When it is set, the builder returns the line with the choices instead of the greeting. For Wild that is `wild2` instead of `wild1` followed by `wild2`. The mod sets the flag while each call's dialog is built. Intro, cooldown and story dialogs have no quick variant, so they play in full.

## Choices

The mod shows the first line whole instead of typing it out, and shows the choices with it instead of 0.85 s later.

The mod patches only `PhoneUIManager` and `DialoguePhoneUIManager`.

## Tested logic

The rule for dialing at once lives in `src/Dial.Pure.cs`, apart from the code that reads the game: call when the digits form a whole number that will answer and no other contact's number starts with them. `tests/DialTest.cs` checks it outside the game with nothing but the C# compiler, and prints ok or FAIL for each case.

Starting is all or nothing. If a method the mod hooks is missing, for example after a game update, the mod removes everything it had already set up, writes one line to `Player.log` and stays off.
