@'

## What's changed

- Audio sliders retain their continuous thumb position after release; older in-flight reads cannot overwrite a newer adjustment.
- Navigation tabs keep one selection when revisiting a page. The green update check sits beside the current version.
- Compact has a quieter notification action and left-aligned wordmark. Battery preservation uses a calmer green gauge with the modeled wear-cycle basis visible beside the comparison.
- A shared native redesign with IBM Plex Sans, Fluent icons, light/dark themes and a cleaner Compact layout.
- Custom fan curves and manual percentages on the calibrated ThinkPad X9 21Q6 / BIOS N4CET45W controller. Five measured running states span approximately 3500–9400 RPM. Auto recovery and a custom curve were physically tested through the production service.
- Separate Modes and Automation, consistent navigation resets and scoped restoration of Lenovo keyboard notifications.
- Battery cycle history, improved chart inspection, an estimated 0→limit versus 0→100 charging-wear comparison, and moving charge/discharge indicators that respect reduced motion.
- Compact status and quick-control layout customization with preserved settings.

## Alpha limitations

Fan percentages select discrete calibrated states, not continuous PWM; 0% retains the lowest accepted running state. Lower states may mildly pulse. Unknown firmware stays capability-gated. Shared RPM does not represent two independent fan sensors. The charging-wear percentage is a generic illustrative model, not measured battery damage, firmware cycles saved or a lifespan prediction. Broad reboot/wake/AC/DC coverage, manual chart/drag acceptance and installed redesign acceptance remain UNVERIFIED; see RELEASE_READINESS.md and issue #113. This prerelease is not a bug-free certification.
'@
