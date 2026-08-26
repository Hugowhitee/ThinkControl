# G-Helper fan/profile research for ThinkControl alpha.16

Research source: public `seerge/g-helper` repository (GPL-3.0). ThinkControl does **not** copy G-Helper code. This note records behavioral ideas and the X9-specific decisions derived independently from them.

## What G-Helper does well

- Uses an 8-point temperature/fan graph instead of a pile of independent sliders.
- Lets points be dragged while keeping temperature/output ordering valid.
- Supports keyboard editing of graph points for precise/accessibility-friendly tuning.
- Saves/applies a curve at a deliberate commit point rather than sending hardware writes for every mouse movement.
- Keeps factory modes but lets users add custom modes by cloning the current configuration, then rename/delete them.
- Offers factory-reset behavior for the original profiles.
- Exposes hysteresis as a first-class concept where firmware supports it.
- Calibrates real fan maximum RPM and can express fan speed as a percentage relative to that measured maximum instead of assuming a control value is already a physical percentage.
- Keeps optional calibration work scoped to the calibration flow rather than continuously polling hardware.

## What ThinkControl should use

Alpha.16 uses the same *product ideas*, implemented independently for the ThinkPad X9 hardware contract:

1. Quiet, Balanced and Max cooling remain useful editable starting profiles.
2. Named custom profiles clone an existing graph and appear in both the full Fans page and compact/tray controls.
3. Curves use 8 ordered points with explicit Save and apply.
4. The final graph point is locked to 100%; the independent 94 °C firmware safety handoff cannot be edited.
5. Characterization measures the seven physically verified EC steps.
6. When calibration is complete, lower EC steps are expressed relative to measured step-7 RPM. The UI's normalized 100% target means this verified maximum, not continuous PWM.
7. A requested cooling percentage is a floor. ThinkControl chooses the lowest calibrated hardware state that can meet it; it never silently selects a weaker state merely because the EC has gaps between discrete outputs.
8. Unstable characterized normal states are skipped upward when thermally safe.
9. Auto mode remains event-driven/idle. Custom curves keep the existing 2-second safety supervisor only while ThinkControl actually owns fan control.

## X9 maximum-state distinction

ThinkPad EC normal manual states are 1–7. Upstream Linux `thinkpad_acpi` also documents a full-speed/disengaged bit (`0x40`) and can combine it with level 7. On the X9 reference unit, `0x47` echoed in the control register but left the fan at 0 RPM, while level 7 physically ramped to approximately 4400 RPM. ThinkControl therefore blocks `0x47` and maps 100% to verified level 7.

Safety requirements in ThinkControl:

- X9 21Q6/21Q7 model gate must pass.
- PawnIO/EC transport must already have passed the existing read-only validation.
- The unverified `0x47` override is never written; readback echo alone is not treated as proof of physical fan output.
- A failed write/readback immediately attempts Lenovo Auto (`0x80`).
- Fan-off/raw `0x00` remains blocked.
- The 94 °C firmware safety handoff stays independent of every user curve and manual target.

## Deliberate differences from G-Helper

G-Helper's modes combine ASUS BIOS performance behavior, power settings and fan curves. ThinkControl keeps **Windows Performance** and **Fan profile** separate because they are independent concepts on the X9 and combining them previously made the compact UI misleading.

ThinkControl also does not copy ASUS-specific fan percentages, RPM defaults, ACPI endpoints, power limits or model tables. X9 percentages come only from X9 characterization data; before calibration the UI treats them as targets mapped to verified discrete states.
