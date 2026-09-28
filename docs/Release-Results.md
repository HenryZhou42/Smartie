# Final artifact results — 2026-09-28

Repository: C:\Users\zyhhe\source\repos\Smartie
Source changes are uncommitted for review.

## Deliverable artifacts
- Portable ZIP: C:\Users\zyhhe\source\repos\Smartie\dist\Smartie-v0.9.0-beta-win-x64-portable.zip
- SHA256: same path with .sha256 appended
- Extracted portable: C:\Users\zyhhe\source\repos\Smartie\dist\Smartie-v0.9.0-beta-win-x64-portable-babc5542\publish\Smartie.exe
- Unsigned MSIX: C:\Users\zyhhe\source\repos\Smartie\dist\Smartie-v0.9.0-beta-msix-d99f6f69\Smartie_0.9.0_x64.msix

Use these outputs, not earlier diagnostic packages retained in dist.
Portable ZIP size: 140311795 bytes. SHA256 verified against the adjacent file.
Portable archive safety check passed (863 entries); MSIX passed (861 entries).
The app reported 0.9.0 Beta and build date 2026.09.28.
Synthetic task and extracted TXT document persisted across native process restart.
The final test app was stopped.

## Validation
124 tests passed, 0 failed, 0 skipped.
Windows portable and MSIX publish succeeded.
Browser host build succeeded.
Release scripts passed PowerShell syntax checks; git diff whitespace check passed.
Secret-pattern scan of tracked text found no matching production key/private-key
patterns. This is a bounded scan, not a guarantee about all Git history.
The release guard was verified to reject a synthetic database file.

## Remaining gates
This is a beta candidate, not a fully manually certified RC.
Native screen automation approval timed out, so broad native UI/drag-drop/theme
interaction validation remains. Browser rendered Settings and Automations were
inspected at narrow width, but interactive state changes were not established.
No clean Windows VM or signing certificate was available. MSIX remains unsigned
and was not installed. WebView2 is still a prerequisite.
Live provider calls, real invalid-key/quota behavior and real-document RAG/citation
quality need credentialed testing. Existing embedding API deprecation and missing
MSIX symbol-tool warnings remain. See docs/Release-Verification.md.
