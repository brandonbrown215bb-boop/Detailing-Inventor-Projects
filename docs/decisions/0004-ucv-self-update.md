# 4. Unit Construction Verifier: self-update from the team share

* **Status**: Accepted. Shipped in UCV v1.0.37.0.
* **Date**: 2026-11-11

## Context and Problem Statement

Detailers install the verifier per user, without admin rights, from `UnitConstructionVerifier_Dist.zip` on
`P:\Detailing\Detailing Tools` (roadmap phase 5). Asking every detailer to re-run `install.bat` for each release
leaves people on old versions. Inventor keeps an add-in's DLL loaded and locked for the whole session; .NET
Framework cannot unload it, so a running add-in can never replace itself in place.

## Decision Drivers

* No admin rights, no IT deployment, no change to how detailers start Inventor.
* Never delay Inventor start-up or break the installed add-in if the share is slow, missing or half-written.
* Keep the release process to "build, run `create_dist.bat`, copy the zip".

## Considered Options

1. **Notify only.** Show "a newer version is available"; the detailer runs `install.bat`.
2. **Self-staging update.** The add-in copies a newer release into the Addins folder itself; it loads next start.
3. **Loader shim.** A stable DLL in the `.addin` copies the latest release locally and loads it by reflection.
4. **Load from the share.** Point the `.addin` `<Assembly>` at the share.

## Decision

Option 2. On `Activate`, `Deployment.SelfUpdater` runs on a background task:

* It reads `version.txt` from the release zip (written by `create_dist.bat` from the DLL's file version) and
  compares it with the file version of the DLL on disk, not the loaded one, so a release already copied this
  session is not copied again.
* If newer, every `.dll`, `.addin` and `.json` at the zip's top level is extracted beside its target as `*.new`
  first. Only then is each old file renamed to `*.old_<stamp>` (Windows allows renaming a loaded DLL, as
  `install.bat` already relied on) and the new file moved in. A failed rename puts back the files already
  swapped. A named mutex keeps two Inventor sessions from updating at once.
* Leftover `*.old_*` files are deleted at the next start; ones still loaded by another session stay until later.
* The share is addressed by server name (`\\C4050MV010.go.johnsoncontrols.com\Public\...`), not the P: letter.

Updates only move forward. Rolling back means publishing the old code under a higher version number.

## Consequences

* A release reaches a detailer after two Inventor starts: one copies it, the next loads it. The verifier shows a
  one-time notice after the copy.
* Detailers on v1.0.36 or earlier must run `install.bat` once to get the updater.
* The share path is compiled in. Moving the release folder needs one last release from the old location.
* Options 3 and 4 were rejected: the shim is more code and harder to debug than one team needs, and a DLL loaded
  from the share is locked there while anyone runs Inventor, is unavailable off the network, and may be refused by
  .NET's remote-load policy.
* `install.bat` and the `.addin` still target Inventor 2020 only; an Inventor upgrade needs a new install path.
