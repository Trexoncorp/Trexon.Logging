# Development and local verification

[Русский](../DEVELOPMENT.md) · [English](../en/DEVELOPMENT.md) · [Deutsch](../de/DEVELOPMENT.md)

[Index](README.md) · [Architecture](ARCHITECTURE.md) · [Publishing](PUBLISHING.md)

## Tools

Use SDK 10. `global.json` selects `10.0.100`, `rollForward: latestFeature` within
the chosen major/minor, and disallows prereleases. Tests target net8.0 and net10.0;
install both runtimes, since .NET 10 alone does not run the older target. Python 3
is needed for documentation checks; actionlint is optional for workflow validation.

```sh
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

## Layout

| Path | Purpose |
| --- | --- |
| Trexon.Logging.slnx | Solution |
| Trexon.Logging/ | net8.0 core library and Internal/ implementation |
| tests/Trexon.Logging.Tests/ | xUnit v3 / Microsoft Testing Platform |
| examples/Trexon.Logging.Sample/ | Runnable net8.0 console sample |
| README.md / README.en.md / README.de.md | Russian, English, and German project pages |
| docs/ / docs/en/ / docs/de/ | Guides in the three languages |
| scripts/check-docs.py | Translation presence, links, and runnable examples |
| scripts/release-version.sh | Release tag validation and version extraction |
| .github/workflows/ | CI and tag publication |
| global.json | SDK and test-runner selection |

The core has no PackageReferences. Test dependencies stay in the non-packable test
project; the sample is also non-packable.

## Development loop

From the repository root:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
git diff --check
```

The selected runner is Microsoft Testing Platform; use its `--solution` syntax.
Build fresh binaries before `--no-build`, especially after package or runner changes.
Tests run on both targets. Theory cases and both runtimes contribute to the reported
count; the runner output, not a fixed documentation number, is authoritative.

## Test coverage

| File | Scenarios |
| --- | --- |
| [LoggerTests.cs](../../tests/Trexon.Logging.Tests/LoggerTests.cs) | Levels, templates, culture, snapshots, caller, validation |
| [ContextTests.cs](../../tests/Trexon.Logging.Tests/ContextTests.cs) | Nested scopes, async isolation, LIFO, child ExecutionContext |
| [OutputTests.cs](../../tests/Trexon.Logging.Tests/OutputTests.cs) | Dates, zones, colors, JSON, Unicode, UTF-8 append, concurrency |
| [RedactionTests.cs](../../tests/Trexon.Logging.Tests/RedactionTests.cs) | Nested redaction, escaping, order, regex timeout |
| [LifecycleTests.cs](../../tests/Trexon.Logging.Tests/LifecycleTests.cs) | Failures, ownership, flush/dispose, initialization, recursion, races |
| [StaticLogTests.cs](../../tests/Trexon.Logging.Tests/StaticLogTests.cs) | Defaults, Configure, Shutdown, old views, failed reconfiguration |

Process-global tests use a non-parallel collection. Temporary folders are cleaned
up. Caller tests use non-inlined methods with PDBs. Passing tests establish the
covered behaviors, not a performance SLA or Native AOT support.

## Documentation and translations

```sh
python3 scripts/check-docs.py --links-only
python3 scripts/check-docs.py
```

The first command checks language completeness, local file/heading links, fences,
and markers. Full mode extracts complete C# programs marked
`<!-- verify: unique-name -->`, builds temporary net8.0 apps referencing this project,
and executes them. Each program includes its own usings, statements, and any types.
Signature blocks and unmarked fragments are not run. Shell blocks, Git tags,
package pushes, and network configuration are never executed by this script.
The SDK can perform normal NuGet restore during temporary builds.

Russian pages remain in docs/, with matching filenames in docs/en/ and docs/de/.
Every page links to its counterparts. Update all three versions for API or behavior
changes. Use globally unique verify names, prefixed `en-` and `de-` in translations.
Adding a Russian guide requires both translations; the checker reports omissions.
Checks validate structure and runnable examples, not the linguistic accuracy of prose.
Adding another language requires extending `LANGUAGES` in the checker, creating
matching pages and a root README, and updating language selectors.

## Local packages

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:ContinuousIntegrationBuild=true -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:ContinuousIntegrationBuild=true -warnaserror
```

`artifacts/` receives the nupkg and snupkg for `0.1.0-dev`. The main package contains
the DLL, XML docs, multilingual READMEs, MIT license, and repository metadata.
The symbol package contains portable PDBs. SDK Source Link associates committed
sources with the repository; untracked sources can be embedded in PDBs. Uncommitted
working-copy changes do not become a new published commit. Use the same Version
property in build and pack. See [local consumption](GETTING_STARTED.md).
Git ignores bin, obj, TestResults, and artifacts.

## CI

Documentation checks run in Linux CI and in the release workflow before the main build.
They validate language versions, links, and runnable examples.

The local workflows define:

- ci.yml: main pushes/PRs; Linux, Windows, macOS; restore, Release build, net8/net10
  tests, pack, package artifacts retained for 14 days.
- publish.yml: v* tag; validate version/main ancestry, build, test, pack, artifacts
  for 30 days, OIDC login, nupkg/snupkg push.
- dependabot.yml: monthly grouped GitHub Actions updates.

Actions are SHA-pinned. setup-dotnet installs the global.json SDK and .NET 8.
Changed workflows run remotely only after those changes are pushed.
If actionlint is installed:

```sh
actionlint
```

Publishing remains a separate [release operation](PUBLISHING.md).
