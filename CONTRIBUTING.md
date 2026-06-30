# Contributing to Smartie

Thank you for your interest in Smartie Community Edition.

## Getting started

1. Clone the repository
2. Install [.NET 9 SDK](https://dotnet.microsoft.com/download)
3. Run tests: `dotnet test tests/Smartie.Tests`
4. Run desktop app: `dotnet run --project src/Smartie.Maui`
5. Run web dev UI: `dotnet run --project src/Smartie.Web`

## Sample content

Use documents in `TestData/` for manual QA:

- `Smartie_Test_Document.md` — rich semantic search fixture
- `CompanyPolicy.md` — short policy doc
- `ASPNetNotes.md` — technical notes

Import via **Onboarding → Import Sample Documents** or upload manually.

## Pull requests

- Keep changes focused and tested
- Follow existing Clean Architecture layers
- Run `dotnet test` before submitting
- Community Edition must remain local-only (no telemetry, no cloud dependencies)

## Code of conduct

Be respectful and constructive. Smartie is an open portfolio project meant to be shared and learned from.
