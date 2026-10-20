# Implementation verification

The C# implementation uses .NET 8 Windows Forms and preserves all 18 mode identifiers and the requested navigation order. The application, resources, project metadata, documentation, and scripts use the name **Calculator**.

## Completed checks

- Release compilation with warnings treated as errors: **0 warnings, 0 errors**.
- Core tests: **136 passed**, none skipped.
- Native UI tests: **41 passed**, none skipped, at **120 DPI / 125% display scale**.
- Self-contained Windows x64 package: Standard launched at **400×600 logical pixels**, saved Graphing launched at **800×640**, and both closed with exit code **0**. The smoke check left the normal settings profile unchanged.
- Rendered layout checks cover minimum, default, large, maximized, and restored windows; the converter breakpoint is measured using actual workspace width.
- Scientific and Programmer tests select commands through native popup keyboard navigation, then send digits, Backspace, Enter, Copy, and Paste through the current focus owner. The tests do not restore focus after the command under test.
- Currency tests exercise both dropdowns, preview versus committed values, prefix matching, feed replacement while a popup is open, reverse entry, rejected edits, and cached results after refresh failure.
- Graph tests cover expression selection, six-row limits, row removal, the visible keypad, native function menus, trace/ranges, pan/zoom shortcuts, scrolling, cancellation, and stale result rejection.
- Date tests exercise the native calendar and extended ISO dates. Programmer tests cover disabled digits, word sizes, pending-expression feedback, and editing a scrolled bit.
- Both themes and all six accents are checked. Settings content and all accent choices remain reachable at minimum size.
- Source, metadata, scripts, and documentation scan: no remaining surname references or unfinished implementation markers.

The native test harness uses a separate STA message loop for each fixture and consistent DPI awareness for native mouse coordinates. This prevents test setup from clicking a caption button or retaining popup state across fixtures.

## Artifacts and reproduction

```powershell
.\scripts\verify.ps1 -ExpectedDpi 120
.\scripts\publish.ps1
.\scripts\smoke-package.ps1
```

Test reports are written under each test project's `TestResults` folder. Representative screenshots are in `artifacts/screenshots`. The portable application is published to `artifacts/publish/win-x64` and distributed as `artifacts/Calculator-win-x64.zip`; keep its runtime files and `licenses` folder together.

The package smoke check uses isolated settings for Standard and saved Graphing startup, including native window bounds and clean shutdown.

## Verification limits

Actual Windows display-scale runs at **100% and 150% remain unverified** in this environment. Repeat `verify.ps1` at those display scales with `-ExpectedDpi 96` and `-ExpectedDpi 144`. The option checks the real DPI and does not simulate it.

Currency network behavior is covered using deterministic feed fixtures, cancellation, and offline failure tests. An independent live ECB download is not part of this verification.
