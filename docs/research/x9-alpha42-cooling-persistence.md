# X9 alpha.42 cooling persistence evidence

Scope: ThinkPad X9-15 Gen 1 `21Q6/21Q7`. This note records the restart/source-transition evidence that changed alpha.42. It does **not** authorize any new low-level fan writer.

## Physical observation

After selecting a ThinkControl firmware profile such as **Quiet** and restarting, the saved selector could still show Quiet while the real fan behavior was audibly much harder and more consistent with Lenovo Auto/base policy. The same class of mismatch could plausibly affect Balanced or Max because the UI previously allowed the persisted preference to stand in for applied state during initialization.

This observation does not by itself prove which Lenovo component changed the policy. It is sufficient to reject the product behavior where the UI can claim a saved profile without ensuring the current OEM policy was actually re-applied.

## Architecture findings

Three independent product issues were found:

1. `FansPanel` initialized its selector from `UserSettings.CoolingProfile` even when runtime/service state still said Auto. This could make a failed or pending restore look successful.
2. `LenovoCoolingPolicyCoordinator.SetBasePowerMode` updated the stored Auto baseline while an override was active, but intentionally skipped an OEM write. Lenovo's reviewed LITSSvc command is source-specific (AC 502/503/504 vs DC 507/508/509), so AC/DC or resume policy work could leave the physical OEM policy different from the in-memory Quiet/Balanced/Max label.
3. The normal-user UI exit path always requested full fan Auto. That was correct for temporary/direct fan ownership but wrong for a durable service-owned firmware profile: merely closing/restarting ThinkControl could erase Quiet/Balanced/Max.

A login/startup race is also possible because Lenovo services may finish their own policy initialization shortly after ThinkControl first becomes reachable. There is no reviewed LITSSvc operation that truthfully reads back the active semantic Quiet/Balanced/Performance policy, so pretending otherwise would be unsafe.

## Alpha.42 correction

Alpha.42 keeps the existing reviewed hardware contracts and changes ownership/convergence only:

- the Fans selector follows runtime/service state until restoration succeeds;
- a saved built-in profile is actively restored after the firmware-policy capability appears;
- the service reasserts an active firmware cooling override whenever the Windows baseline is refreshed after power-mode, AC/DC or resume events;
- the UI performs one bounded seven-second settle reassert after successful restore/selection to cover late Lenovo login policy work;
- that settle action is generation-guarded so a stale saved choice cannot overwrite a newer user selection;
- closing/restarting only the UI preserves service-owned firmware policy;
- direct/manual output still returns to Auto on UI exit;
- hardware-service disposal remains the ownership-aware cleanup boundary;
- Max still uses only the exact-X9 `0x04020000` boolean contract and the rejected `fanX_target` writer remains read-only.

No new EC register, Other Mode attribute, IOCTL or LITSSvc command was introduced.

## Physical validation still required

Hosted CI can prove the control flow but cannot prove Lenovo's physical response. On the real X9, check:

- save Quiet, reboot/sign in, and verify the UI does not claim Quiet before restore; after restore/settle the fan should physically behave like Quiet;
- repeat for Balanced and Max cooling;
- close/reopen only the UI with Quiet active and verify the profile does not revert to Auto;
- switch AC to battery and back while a non-Auto profile is active;
- sleep/resume with a non-Auto profile active;
- confirm Auto still restores the latest Windows/Lenovo baseline;
- confirm Max still reaches the separately verified full-speed semantic without reviving per-fan target writes.

Treat any remaining UI/physical mismatch as a release-blocking runtime-truth bug, not as permission to broaden low-level writes.
