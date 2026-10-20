# Calculator

A native Windows Forms calculator with 18 modes, light and dark themes, six accent colors, and shared keyboard and clipboard controls.

## Run and build

Open `Calculator.sln` in Visual Studio, select **Calculator** as the startup project, and run it. The solution targets .NET 8 for Windows and pins SDK 9.0.318 (with patch roll-forward) in `global.json`.

```powershell
dotnet restore Calculator.sln
dotnet run --project Calculator/Calculator.csproj
```

The self-contained Windows package requires no separate .NET installation:

```powershell
.\scripts\publish.ps1
```

Run `artifacts\publish\win-x64\Calculator.exe`. Keep the published folder together when copying the app to another computer.

## Modes and controls

Navigation order: Standard, Scientific, Graphing, Programmer, Date Calculation, Currency, Volume, Length, Weight and Mass, Temperature, Energy, Area, Speed, Time, Power, Data, Pressure, Angle.

- **Standard** executes operations immediately: `2 + 3 × 4 = 20`. It supports repeated equals, contextual percent, square/root/reciprocal, C/CE, and shared memory.
- **Scientific** uses expression precedence: `2 + 3 * 4 = 14`. Functions require parentheses and multiplication is explicit (`2*pi`). Supported functions are `sqrt`, `abs`, `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `ln`, and `log`; operators include powers, factorial, and percent. DEG/RAD is selectable.
- **Programmer** supports BIN/OCT/DEC/HEX, signed 8/16/32/64-bit words, wrapping arithmetic, bitwise operations, shifts, and an expandable bit editor. AC clears; C is a hexadecimal digit. Paste uses the selected base.
- **Date Calculation** computes signed differences or adjusts years, months, then days with month-end clamping. Use the calendar buttons or ISO date text. Results support years beyond the native calendar's range.
- **Currency and unit converters** update immediately. Activate either amount to reverse entry direction, use the shared keypad, and preserve native selection/editing. Swap exchanges quantities and units. Copy returns the converted amount. The catalog contains 75 unit entries across twelve static categories.
- **Graphing** accepts up to six `y=f(x)` expressions using Scientific's grammar plus `x`. Plot or Enter applies edits. Drag to pan; focus the plot before wheel zoom. Arrows pan, plus/minus zoom, and Home resets. Open **Ranges and trace** to set bounds and inspect coordinates. Graphing has its own RAD default.

| Shortcut | Action |
| --- | --- |
| Digits and arithmetic keys | Enter numbers and operations |
| Enter | Calculate, or plot from an expression field |
| Backspace | Delete from the active entry |
| Escape | Clear calculation; close an open menu without committing |
| Delete | Clear entry |
| Ctrl+C / Ctrl+V | Copy result / paste a number outside editable fields |
| Tab / Shift+Tab | Navigate controls |
| Space | Activate the focused button |

Editable fields keep native text selection and clipboard shortcuts. Scientific/Programmer menu commands restore calculator keyboard input synchronously without changing the saved expression selection.

Currency dropdowns use country/region labels. Repeated letters cycle matches; letters within one second form a prefix. Enter or a click commits, and Escape keeps the previously selected currency. Arrow, Home/End, and page navigation remain available.

## Numbers and limits

Decimal modes use `PeterO.Numbers` with 34 significant digits and half-even rounding. This intentionally replaces the original basic calculator's `double` arithmetic. Display formatting never feeds back into the accumulator or converter source. Trigonometry, logarithms, constants, and fractional powers use approximately 15 significant digits; graph viewport coordinates use `double`.

Expressions allow 4096 characters, 512 tokens, and 128 nesting levels. Factorials accept integers from 0 through 1000; powers use exponents between -10000 and 10000. Converter and Programmer entries allow 128 characters. Decimal scale and magnitude follow the shared 10000-digit bounds. Undefined, nonfinite, overflowing, and unsupported underflowing operations produce recoverable errors.

Programmer division truncates toward zero; arithmetic wraps at the chosen word width. Data converters distinguish decimal and binary prefixes. Temperature conversion rejects values below absolute zero.

Graph sampling is adaptive and bounded to 16000 evaluations, approximately two seconds per curve, and eight subdivision levels. Unresolved or undefined intervals become gaps. Finite sampling cannot detect arbitrarily narrow features. Cancelling, editing, changing ranges, or leaving Graphing invalidates older work.

## Layout and appearance

The ordinary launch size is 400×600 logical pixels with a 320×500 minimum. Graphing enforces an 800×640 minimum and remains side by side. Leaving Graphing restores the ordinary minimum without shrinking the window.

Converters use stacked controls below an actual workspace width of 640 logical pixels; wider workspaces use equal columns and expand through the available height. Other modes keep their workspace caps: Standard 520, Scientific/Programmer 640, Date 560, and Graphing 960.

Settings provides Light/Dark themes and Blue, Purple, Pink, Teal, Yellow, and Coral accents. The app uses native controls and visible keyboard focus.

## Saved data

Preferences and one validated ECB rate snapshot are stored in `%LOCALAPPDATA%\Calculator\settings.json`. No history, operands, converter quantities, graph expressions, clipboard contents, or memory register is saved. Mode views retain edits while the application remains open. Session history keeps the newest 200 successful Standard, Scientific, and Programmer calculations.

The first launch imports known settings from a uniquely identifiable compatible Java preferences profile. The importer discovers the profile by its keys, decodes Java's Windows registry escaping, and never modifies the original profile. Existing C# preferences win. Ambiguous profiles are left untouched. Future settings-file versions are not overwritten.

Currency uses the European Central Bank daily reference-rate feed. Cached rates remain usable while refreshing and after failures. The published rate date is always shown. Invalid, duplicate, nonpositive, future-dated, or older replacement data cannot displace the current valid snapshot. Refresh/Retry is available in Currency.

For isolated testing, set `CALCULATOR_DATA_DIR` to a separate settings directory and `CALCULATOR_SKIP_LEGACY_IMPORT=1`.

## Verification

```powershell
.\scripts\verify.ps1
# Domain tests without opening test windows:
.\scripts\verify.ps1 -SkipUi
# Require the actual display scale to match the verification environment:
.\scripts\verify.ps1 -ExpectedDpi 120
```

UI tests open real native windows and send keyboard input, so run them on an unlocked desktop without simultaneously using the keyboard or mouse. Test settings and currency fixtures are isolated from the normal profile; tests restore clipboard content after clipboard checks. The suite includes menu focus regressions, converter direction/selection, currency preview/commit, graph interactions/cancellation, and rendered keypad bounds. Screenshots are written to `artifacts\screenshots`.

Run DPI verification separately at Windows display scales 100% (`96`), 125% (`120`), and 150% (`144`). The `ExpectedDpi` option checks the actual DPI; it does not simulate a different Windows display scale.

The latest implementation results and verification limits are recorded in [docs/verification.md](docs/verification.md). Run `.\scripts\smoke-package.ps1` after publishing to check the packaged executable with isolated settings.

## Structure

- `Calculator.Core`: UI-independent engines, decimal policy, unit/currency services, input state, and graph sampling.
- `Calculator`: WinForms shell, shared controls, mode views, settings, and legacy import.
- `Calculator.Core.Tests`: deterministic arithmetic, state, conversion, currency, and graph tests.
- `Calculator.Ui.Tests`: STA native-control and UI Automation integration tests.

The behavior follows the supplied Java calculator handoff. Windows Calculator is a layout reference; its C++ engine is not embedded. Numeric operations use PeterO.Numbers, and UI verification uses MSTest and FlaUI.
