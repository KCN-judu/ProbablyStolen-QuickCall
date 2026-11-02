# Changelog

## 1.0.1

- Starting the mod is all or nothing: if the game no longer has a method the mod hooks (after a game update, for example), everything it already set up is taken down again, one line is written to Player.log and the mod stays off, instead of running half enabled.
- The rule for dialling at once is separated from the game code and covered by a test that runs outside the game. Nothing changes in play.

## 1.0.0 (pre-release)

First public release.

- Calls go straight to the choices: no wait after the last digit, no ringing, no pickup pause and no greeting lines.
- Typed numbers dial as soon as they form a whole contact number.
- Intro, cooldown and story calls still play in full.
