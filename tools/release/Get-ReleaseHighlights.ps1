@'

## What's changed

- Fan curves check sustained RPM against measured output and return to Auto on excessive output or a failed control tick. Physical reproduction of the reported full-speed issue remains unverified.
- Compact CPU/Sensors open Diagnostics. Mode selection is separate from Saved modes; System links/history preferences are visible directly and the redundant Advanced fan controls disclosure is removed.
- Compact is 64 pixels shorter. Mode replaces Keyboard light by default; the layout editor also offers Keyboard light and Automation.
- Layout dragging dims the source, shows the actual tile under the pointer and outlines valid swap targets.
- Turn off mode is directly accessible; restoration preserves the actual pre-mode Windows power overlay, with retryable readback failures.
- Mode and rule editors keep Save/Cancel below their tabs, with larger Fluent remove icons.
- Advanced battery indicators have clearer reversible diagonal charge/discharge flow. Battery animation can be On, Off or follow System in General preferences; Compact remains static.
- Custom battery preservation limits have a visible selected Custom segment and a separate Edit action.
- Saved Modes can prepare the required Windows Balanced plan and restore the previous plan when leaving the mode.
- Cooling curves pause in firmware Auto during temporary sensor gaps and resume after stable recovery. Hot-system curve selections are retained while firmware cools the system; real write/ownership failures still return to Auto.
- Cooling failures give more specific guidance and service diagnostics record the actual handoff reason.
- Recorded sessions moves below Battery details, with readable time, battery and energy summaries. Battery details correctly labels learned charging power.
- A shared native redesign with IBM Plex Sans, Fluent icons, light/dark themes and a cleaner Compact layout.
- Custom fan curves and manual percentages on the calibrated ThinkPad X9 21Q6 / BIOS N4CET45W controller. Five measured running states span approximately 3500–9400 RPM. Auto recovery and a custom curve were physically tested through the production service.
- Separate Modes and Automation, consistent navigation resets and scoped restoration of Lenovo keyboard notifications.
- Battery cycle history, improved chart inspection, an estimated 0→limit versus 0→100 charging-wear comparison, and moving charge/discharge indicators that respect reduced motion.
- Compact status and quick-control layout customization with preserved settings.

## Alpha limitations

Fan percentages select discrete calibrated states, not continuous PWM; 0% retains the lowest accepted running state. Lower states may mildly pulse. Unknown firmware stays capability-gated. Shared RPM does not represent two independent fan sensors. The charging-wear percentage is a generic illustrative model, not measured battery damage, firmware cycles saved or a lifespan prediction. Broad reboot/wake/AC/DC coverage, manual chart/drag acceptance and installed redesign acceptance remain UNVERIFIED; see RELEASE_READINESS.md and issue #113. This prerelease is not a bug-free certification.
'@
