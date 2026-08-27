# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for physically testing ThinkControl `v0.1.0-alpha.23` on the X9 reference device, machine type `21Q6` or `21Q7`.

## Before installation

- Close other direct fan or EC-control utilities.
- Keep Lenovo platform drivers/services installed, especially Lenovo Intelligent Thermal Solution and Lenovo Power Management.
- Start the first low-level test on AC power without a heavy workload.

## Installer and startup

Install the published `ThinkControl-Setup-0.1.0-alpha.23.exe`, not a loose development build.

Confirm:

1. Setup downloads the matching `ThinkControl-Payload-0.1.0-alpha.23.zip` and verifies SHA-256.
2. `ThinkControlService` installs and reaches a real app-to-service `Ping` state.
3. ThinkControl identifies `21Q6/21Q7` as the verified X9 profile.
4. If PawnIO or another prerequisite is required, Hardware Setup reports the actual missing layer rather than generic “offline”.
5. **Startup never shows a large empty/black ThinkControl window.** The small ThinkControl loading surface must appear first when startup discovery takes noticeable time and remain until the actual Compact/full surface is painted.
6. Repeat a cold launch several times with the default opening view set once to Compact and once to Full.

## Compact and full shell regression

This is a release-blocking alpha.23 check because alpha.22 regressed this route.

1. Open Compact from the notification area.
2. Use the outward-arrow action to enter Full view.
3. Confirm Full is already rendered when Compact disappears; there must be no black intermediate window.
4. Use the inward-arrow action to return to Compact.
5. Confirm Compact is already visible when Full disappears.
6. Repeat Compact → Full → Compact at least ten times, including quick but deliberate clicks.
7. The app must not terminate, vanish into the tray unexpectedly, remain transparent, or leave both surfaces stuck visible.
8. If Full is the configured startup view, explicitly switching to Compact must still work; startup preference must never suppress a user-requested Compact view.
9. Tray left-click while Full is open should switch safely to Compact.
10. Native Full-window maximize/restore, Snap Layouts and system menu must continue to work.

CI also executes this transition automatically, but physical testing is still required because WPF/DWM composition differs from a hosted runner.

## Sidebar and icons

Confirm:

- the larger ThinkControl wordmark is cleanly aligned at the top of the sidebar;
- Compact view + Notifications are visually separated from page navigation;
- Compact/Full use inward/outward diagonal arrows, never a sidebar-layout icon;
- icon-only actions show short ThinkControl hover labels;
- icon language remains the curated **Google Material Symbols Outlined** set; sensitivity uses the `tune`/adjustment glyph rather than a performance gauge.

## Home and terminology

Verify the Home overview at minimum, normal and maximized widths:

- no duplicate mini-strip for Mode / Display / Keyboard above telemetry;
- Battery / CPU / Fans / Power / Sensors have consistent separators;
- Fans shows **profile/mode first** and RPM underneath;
- selected fan state is neutral, not red;
- power terminology is consistently **Efficiency / Balanced / Performance** on Home and Performance;
- battery ETA is readable and not rendered as tiny metadata.

## Read-only telemetry

Check telemetry before changing fan state:

- CPU/control temperature is plausible and honestly labelled;
- fan RPM appears only from a real provider;
- fan state starts in Lenovo Auto before manual control is used;
- battery percentage and charge/discharge power are plausible;
- time estimates settle gradually rather than jumping on every sample.

An unavailable value is not automatically a failure. Record the provider/capability explanation shown by ThinkControl.

## Cooling and fan behavior

Continue only when ThinkControl identifies the verified X9 profile and fan control is available.

1. Start in Lenovo Auto.
2. Switch supervised cooling profiles, then return to Auto.
3. Confirm Compact, Home and Fans agree on the selected profile.
4. Listen for state hunting: the fan should not repeatedly wave up/down around a threshold under a steady workload.
5. Confirm ordinary profile control does not create periodic whole-laptop lag or hitches.
6. Verify genuinely hot/large upshifts still react promptly.
7. If testing manual control, use Levels 1–7 one at a time and return to Lenovo Auto afterward.
8. After a manual level, quit/uninstall/service-stop and confirm normal Lenovo fan behavior resumes.

ThinkControl does not expose fan-off `0x00` or the unverified `0x40` override family.

## Performance

Test **Efficiency, Balanced and Performance** on AC and battery. These are policy controls, not fixed fan-RPM targets. On the X9, confirm Windows mode and reviewed Lenovo thermal-policy coordination both settle without UI or system lag.

## Keyboard

1. Test Off, Low and High.
2. Confirm each state matches the physical keyboard.
3. Test Auto and user-session effects when available.
4. Direct static changes must not be silently dropped behind an in-flight effect write.

## Touchpad visualization and gestures

Start with Edge gestures disabled and confirm ordinary Windows touchpad behavior. Then enable gestures.

### Trail integrity

1. Touch and move on one side of the pad; confirm a short fading trail follows the real contact.
2. Lift the finger completely.
3. Touch far away on the other side.
4. **No straight line may connect the old and new contacts.** A new finger-down is a new trail segment.
5. Repeat with several quick lift/re-touch patterns and with a large coordinate jump.

### Edge controls

- Left/right continuous controls should highlight only the relevant active edge.
- While moving, `+` or `−` should make the direction obvious.
- On release, the final absolute value should remain briefly and fade rather than disappear immediately.
- Starting a new gesture must clear any old final-value feedback immediately.
- Media seek should show a meaningful signed time change after release.
- Previous/next should report **Previous track** or **Next track**, never generic `Triggered`.
- The old transient center value popup should not appear.

### Touchpad settings UI

- Sensitivity value uses one decimal, e.g. `1.0x`.
- The sensitivity setting uses the Google Material `tune` icon.
- When a setting differs from default, its reset action aligns beside the value/header and does not shorten or shift the slider track.
- Click sensitivity has only the real discrete positions and fills in the same direction as other sliders.

The cursor must restore correctly after release, cancellation, disabling gestures, application exit, lock and sleep/resume.

## Haptic touchpad settings

On Windows 11 24H2 or newer:

- haptic controls remain visible even when unsupported;
- feedback intensity uses real reported discrete levels;
- click sensitivity is enabled only if Windows/HID exposes it;
- unsupported capability disables only that specific control.

## Display, battery and Windows Settings links

Verify refresh switching, brightness and adaptive brightness where exposed.

On Battery:

- ETA/status text is comfortably readable;
- telemetry sections use consistent vertical dividers;
- **Open Power & battery** opens Windows directly to the Screen & sleep / Power & battery surface;
- Windows-owned presence features such as screen-off when away or wake-on-approach remain controlled by Windows rather than duplicated through undocumented registry writes.

On Display, confirm Night light opens the supported Windows Night light Settings surface.

## Audio and Dolby

- output and microphone controls paint immediately when Audio opens;
- normal Windows audio controls work even when Dolby direct control is unavailable;
- on the X9 OEM DAX3 installation, supported fallback profile actions can use Dolby Access without leaving it open when ThinkControl launched it;
- unsupported private DAX controls remain explicit instead of using guessed numeric IDs.

## Updates

- Run **Check for updates** and confirm normal status/last-checked behavior.
- A successful update confirmation is passive and disappears automatically; it must not offer an Ignore/Later action that minimizes ThinkControl.
- Automatic checks never install or open UAC by themselves.

## Sleep/resume

1. Sleep/resume while Lenovo Auto is active and confirm telemetry recovers.
2. Return any manual fan state to Auto before sleep while validating recovery behavior.
3. Test touchpad gestures after resume and confirm input/cursor/trail state has not leaked across sleep.

## Uninstall

Confirm:

- `ThinkControlService` disappears;
- ThinkControl-owned UI/service files are removed;
- shared providers such as PawnIO remain installed if appropriate;
- Lenovo/Intel vendor software is untouched.

## Reporting results

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml). Include ThinkControl version, exact model/machine type, affected section, expected/actual behavior, screenshots for visual issues and the privacy-safe support bundle when relevant.
