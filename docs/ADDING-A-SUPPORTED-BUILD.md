# Adding a supported build

Unknown builds must never be enabled from hashes alone or by weakening the
structural checks.

1. Obtain the updated DLL from a legitimate local Bongo Cat installation and
   preserve a pristine backup. Never add it to this repository.
2. Record the Steam build ID, SHA-256, module MVID, and relevant file metadata.
3. Decompile and audit `BongoCat.Shop`, `TimerUpdate`, `ShopItem.Buy`,
   `ShopItem.CanBuy`, readiness state, timer reset logic, and exchange
   serialization.
4. Confirm the exact manual-click path and both chest types.
5. Add a new compatibility JSON definition and, if the IL shape changed, a new
   explicitly named patch definition. Do not silently reuse an incompatible
   patch.
6. Generate output only in a temporary validation directory. Reopen it,
   validate every branch, compare all methods, and record the deterministic
   patched SHA-256.
7. Test normal launch and multiple genuine 30-minute claim cycles, including
   simultaneous readiness. Do not shorten the cooldown for acceptance.
8. Document the evidence and only then publish support.
