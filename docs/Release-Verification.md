# Smartie 0.9 Beta verification

Release preparation uses the existing Chat/Knowledge Base design and preserves
Community Edition local storage and BYOK. No Voice, Mobile, Cloud Sync, Agents or
Marketplace features were introduced.

## Automated baseline and regression coverage

Baseline: 112 passing tests. Added coverage checks Windows DPAPI round-trip/corrupt
blob handling, connection tests without persisted chats, allowed/blocked browser
origins, corrupt PDF/DOCX input, large-text chunk bounds and cancellation.
The existing suite covers streaming, stop/partial-message persistence, message
editing/regeneration, attachments, TXT/MD/PDF/DOCX extraction, chunking, mocked
embeddings/search, repositories, memory, tasks, automations, plugins and appearance.

Live provider success, billing, rate limits, semantic relevance and citation
quality are not established by mocked tests.

## Changes

- Correct theme-dependent control/dialog colors, focus visibility, reduced motion,
  narrow-window wrapping, status/error announcements and accent button labels.
- Correct welcome wizard rendering/navigation, put provider setup first, and add
  a bounded provider connection test with a synthetic prompt.
- Restrict browser origins to desktop WebView and the documented dev profiles;
  reject untrusted Origin headers even on simple requests.
- Keep Windows DPAPI, report undecryptable credentials as unset, omit provider
  exception payloads from logs, and disable shipped prompt previews.
- Fresh portable staging, content rejection checks, SHA256, deterministic beta ZIP
  naming; MSIX build output isolated from old packages.
- Correct repository URL, beta labels, runtime-derived build date, setup and privacy
  documentation. MIT license retained.

## Manual release gates

Still required before calling this a fully validated RC:
- Clean Windows 10/11 x64 VM launch with WebView2 and without development SDKs.
- Signed MSIX installation/uninstallation and certificate-chain validation.
- Live Gemini/OpenAI/OpenRouter and installed Ollama models, including invalid key,
  offline, quota, streaming stop, and provider model availability.
- Native file dialog and drag/drop with TXT/MD/PDF/DOCX; corrupt/unsupported and
  50 MB boundary inputs; check scanned PDFs are explained as lacking OCR.
- Full keyboard focus trapping/restoration in all dialogs and broad screen-reader QA.
- Visual screenshot review at desktop and narrow widths in each theme.
- Real-document RAG citation correctness and semantic-search quality.

## Known limitations and risks

Embeddings use Gemini regardless of chat provider. Google credentials and network
access are needed for that workflow. Uploaded images/CSV may be stored but are not
supported by the document text-extraction router. Scanned PDFs have no OCR.
Automations run while the application is running; plugins execute trusted local
code. Local data other than API keys is unencrypted. The loopback API is not an
authentication boundary against other processes on the same machine.
Existing Semantic Kernel embedding API deprecation warnings remain.

## Recorded results (2026-09-28)

- Final Release suite: 124 passed, 0 failed, 0 skipped.
- Browser host Release build: succeeded with 0 warnings and 0 errors.
- Portable self-contained Windows publish: succeeded; content checks passed.
- Unsigned MSIX publish: succeeded; missing symbol-tool warning only.
- Isolated API: fresh schema migrations completed, onboarding reports incomplete,
  settings and all dashboard sections load from the isolated data directory.
- Narrow browser render reviewed for Settings and Automations. Interactive browser
  clicks did not yield verifiable state changes in this preview; native interaction
  and full visual acceptance remain separate release gates.

Portable native smoke checks also passed: process/window creation, embedded API
health, fresh onboarding status, task creation, TXT extraction/chunking, corrupt
PDF recorded as Failed, unsupported extension rejected with HTTP 400, and missing
provider configuration reported as HTTP 400. Native screen inspection could not
complete because the computer-use app approval timed out; these are process/API
checks, not proof of native UI interaction.

Final metadata testing exposed a nested src/Directory.Build.props that did not
import the root product properties. It now imports them, and a regression test
checks assembly beta version and the About build date. MSIX contents are checked
as an archive before the script reports a distributable artifact.
