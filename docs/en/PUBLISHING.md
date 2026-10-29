# NuGet publishing

[Русский](../PUBLISHING.md) · [English](../en/PUBLISHING.md) · [Deutsch](../de/PUBLISHING.md)

[Index](README.md) · [Development](DEVELOPMENT.md)

This guide describes release steps after the working copy is ready. Tag/push
commands are instructions for a separately authorized release. Builds and
documentation checks publish nothing.

## GitHub Actions

ci.yml handles main pushes and PRs: restore, Release build, net8/net10 tests,
and packages on Linux, Windows, and macOS, with downloadable artifacts.
publish.yml handles pushed v* tags: validate version and main ancestry, build/test
on Ubuntu, pack, then publish nupkg and snupkg. Failed tests block packaging/publication.
The publish job uses the `nuget` environment and OIDC. NuGet/login obtains a
temporary key; no permanent API key is stored. It publishes the package job's
artifacts without rebuilding them.

Before the release build, the workflow also checks all three documentation languages
and compiles/runs marked examples. A failed check stops the release.

## One-time setup

1. Sign in to [nuget.org](https://www.nuget.org/) and confirm your email. Ensure
   publication rights for Trexon.Logging; a public GitHub repository does not reserve its NuGet name.
2. Under Trusted Publishing, create a GitHub policy:

   | Field | Value |
   | --- | --- |
   | Repository Owner | Trexoncorp |
   | Repository | Trexon.Logging |
   | Workflow File | publish.yml |
   | Environment | nuget |
   | Package scope / glob | Trexon.Logging |

   Select **Push new packages and package versions**. Unlist/relist is unnecessary.
   The first release needs permission to create the package; later ones add versions.
   Choose the NuGet user/organization that should own it. GitHub organizations do
   not automatically create NuGet organizations.
3. GitHub Settings → Secrets and variables → Actions → Variables: add repository
   variable `NUGET_USER`, the publishing NuGet profile name, not an email or API key.
   It must match the policy account.
4. Settings → Environments: create `nuget`. For automatic releases, omit required
   reviewers. If deployment rules exist, allow tags matching `v*`.
5. Ensure GitHub Actions is allowed by the repository and organization.

Without the binding, builds can succeed while publication fails. Official references:
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
and [symbol packages](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg).

## Verify locally

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -p:Version=1.0.0 -p:ContinuousIntegrationBuild=true -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=1.0.0 -p:ContinuousIntegrationBuild=true -warnaserror
```

Build and pack --no-build must use matching versions. nupkg includes the net8.0 DLL,
XML docs, multilingual READMEs, MIT license, and repository metadata; snupkg includes
portable PDBs. SDK Source Link connects committed repository sources.

## Release a version

Check the API, tests, and documentation before the first release. These commands
are instructions, not automatic preparation actions:

```sh
git switch main
git pull --ff-only origin main
git tag -a v1.0.0 -m "Trexon.Logging 1.0.0"
git push origin v1.0.0
```

The tag alone determines the version; no csproj edit is needed. Accepted examples:
v1.2.3, v1.2.3-alpha.1, v1.2.3-rc.2. Leading zeroes, missing numeric components,
and +build metadata are rejected. Validate without publishing:

```sh
bash scripts/release-version.sh v1.0.0
```

Wait for **Publish to NuGet** to complete, then verify the package page. NuGet
validation/indexing may take more time. The workflow does not create a GitHub Release.

## Reruns

Rerun failed jobs after transient errors. --skip-duplicate permits retrying an
already uploaded package; symbols have a separate step. If artifacts expired,
rerun all jobs for the same tag. Do not repurpose published versions or release tags;
ship fixes under a new tag. Ordinary CI packs 1.0.0-dev without publishing.
Choose the version and release channel explicitly before creating the tag.

Check main CI on all three operating systems before tagging. The release workflow
retests on Ubuntu and does not automatically wait for a separate CI run.

## Diagnose failures

| Symptom | Check |
| --- | --- |
| Invalid release tag | Three numeric parts and valid prerelease syntax |
| Commit not reachable from main | Release commit must be in main history |
| Failed tests | Target, .NET 8 runtime, current binaries and runner |
| Environment rejected deployment | nuget permits Tag / v* |
| NUGET_USER missing | Repository variable with NuGet profile name |
| OIDC login failure | Account, owner/repository, publish.yml, environment, scope |
| Rejected package ID | Ownership, name availability/reservation, package scope |
| Version exists | Cannot overwrite; use a new tag |
| Symbols failed | Rerun failed job; separate snupkg step |

An active policy proves configuration, not an actual OIDC exchange or NuGet
acceptance. Only a successful release run verifies that path; green ordinary CI does not.
