# Hardware safety policy

These rules are release requirements, not suggestions.

## 1. No unverified writes

A hardware-write feature is disabled unless the exact model/family and backend have been validated. Similar model names, shared marketing families and guessed WMI/ACPI interfaces are not sufficient.

## 2. No raw-write UI or IPC

ThinkControl exposes semantic operations only. The UI cannot request arbitrary EC registers, I/O ports, ACPI methods or IOCTL payloads.

## 3. X9 fan fail-safe

For the ThinkPad X9-15 Gen 1 research backend:

- fan control register: `0x2F`
- Lenovo BIOS/EC Auto: `0x80`
- allowed manual levels: `1` through `7`
- fan-off `0x00`: blocked
- disengaged/full-speed-style `0x40`: blocked until separately proven safe and needed

The implementation must never transform a percentage into values outside the explicit allowlist.

## 4. Always retain Lenovo Auto escape path

Any direct fan backend must be able to return ownership to Lenovo before it is enabled. Direct control is not considered available if Auto cannot be verified.

Attempt Auto on:

- failed manual write
- profile reset
- service stop
- app uninstall/update service replacement
- shutdown
- sleep/hibernate
- fatal provider error

## 5. Deduplicate writes

Do not rewrite an unchanged EC state on a timer. A user-visible graph may update every second while the hardware receives zero writes if the requested state has not changed.

## 6. Conservative tachometer access

Repeated X9 tachometer reads correlated with an audible periodic fan disturbance in prior testing. RPM polling therefore has a backend-specific budget. Manual mode should prefer settle-then-sample behavior rather than constant reads.

## 7. Shared EC locking

A validated ThinkPad EC backend must participate in known/shared EC mutex conventions where possible, including the ThinkPad/Windows EC locks used by the research implementation. Failure to obtain the lock is an error, not permission to bypass coordination.

## 8. Conflicting controllers

If another direct EC fan controller is active, ThinkControl direct fan control is disabled. The UI must name the conflict when possible.

## 9. Unknown devices are safe-mode devices

Unknown ThinkPads may use supported Windows APIs and read-only diagnostics. No remote profile, issue comment or downloaded JSON can turn on new EC writes.

## 10. Release-gated hardware knowledge

Write addresses, IOCTL contracts and ACPI methods that affect hardware are shipped in signed/reviewed application releases. A remote catalog may update labels, support links and read-only metadata only.

## 11. Privilege minimization

Only the service is privileged. Browser links, graphs, update discovery and normal settings remain in the user process.

## 12. Diagnostics privacy

Never include by default:

- serial number
- Windows user name
- host name
- MAC addresses
- disk serials
- account IDs
- personal file paths

An opt-in report must be previewable before submission.
