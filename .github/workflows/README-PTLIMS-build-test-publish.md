# PT-LIMS Build, Test, Publish and Versioning

This document explains how to install and use
`PTLIMS-build-test-publish-images.yml` for the PT-LIMS monorepository.

The workflow builds and publishes three independent Windows container images:

| Component | Source | ECR repository variable | Git tag pattern |
|---|---|---|---|
| API | `src/PTL.Api/**` | `ECR_API_REPOSITORY` | `api-vX.Y.Z` |
| Internal Web | `src/PTL.InternalWeb/**` | `ECR_INTERNAL_REPOSITORY` | `internalweb-vX.Y.Z` |
| External Web | `src/PTL.ExternalWeb/**` | `ECR_EXTERNAL_REPOSITORY` | `externalweb-vX.Y.Z` |

Each component has its own semantic version. Releasing one component does not
increase the versions of components that did not change.

## What the updated workflow provides

- Change detection for API, Internal Web and External Web.
- Independent semantic versions for all three components.
- Component-specific pull-request release labels.
- Live PR-label retrieval so rerunning a failed job sees newly applied labels.
- Windows container validation on `windows-2022`.
- SonarCloud, build and test quality gates.
- AWS authentication through GitHub OIDC.
- Immutable ECR tags containing the image-digest suffix.
- Component Git tags created only after successful image publication.
- Manual releases where each component can be selected independently.
- Idempotent handling when a release job is rerun for an already tagged commit.
- Atomic Git-tag publication when multiple components are released together.

## Step 1: Install the workflow


.github/workflows/build-test-publish-images.yml
```

Disable any older workflow that also creates release tags after a merge. The
new `finalise-release` job owns component Git-tag creation.

The workflow file itself is intentionally not included in the application path
filters. Therefore, a pipeline-only PR runs the solution quality checks but
does not require component release labels or publish application images.

## Step 2:  GitHub labels

Open **Repository > Issues > Labels > New label** and create these nine labels:

| Label | Purpose |
|---|---|
| `release:api:patch` | API bug fix |
| `release:api:minor` | API backward-compatible feature |
| `release:api:major` | API breaking change |
| `release:internalweb:patch` | Internal Web bug fix |
| `release:internalweb:minor` | Internal Web backward-compatible feature |
| `release:internalweb:major` | Internal Web breaking change |
| `release:externalweb:patch` | External Web bug fix |
| `release:externalweb:minor` | External Web backward-compatible feature |
| `release:externalweb:major` | External Web breaking change |

Generic labels such as `release:patch`, `release:minor` and `release:major` are
not valid.

## Step 3: Configure variables and secrets which are configured already

repository or environment variables:

```text
EXPECTED_AWS_ACCOUNT_ID
EXPECTED_AWS_REGION
ECR_API_REPOSITORY
ECR_INTERNAL_REPOSITORY
ECR_EXTERNAL_REPOSITORY
SONAR_ENABLED
SONAR_PROJECT_KEY
SONAR_ORGANIZATION
```

Configure these secrets in the `ecr-production` GitHub environment:

```text
AWS_ENV_REGION
AWS_ENV_ACCOUNT
AWS_ENV_OIDC_ROLE
SONAR_TOKEN
```

`SONAR_TOKEN` is required when `SONAR_ENABLED` is `true`.

## Step 4: Configure repository rules

### Protect the `main` branch

Configure the `main` branch ruleset to require:

```text
Validate component release labels
Format, build and test solution
CI and publication gate
```

Require SonarCloud when it is enabled. Also require PR approval, code-owner
review and resolved conversations according to the repository policy.

### Protect release tags

In the GitHub repository, navigate to:

```text
Repository
→ Settings
→ Rules
→ Rulesets
→ New ruleset
→ New tag ruleset
```

Create one tag ruleset named `PT-LIMS release tag protection` and set its
enforcement status to `Active`.

Under **Target tags**, select **Add target** and add each of these inclusion
patterns:

```text
api-v*
internalweb-v*
externalweb-v*
```

The current workflow publishes Internal Web and External Web independently,
so the generic pattern `web-v*` would not protect either of their tags.

Configure the ruleset to:

- prevent release-tag deletion;
- block force pushes to release tags; and
- restrict tag creation only when the approved release automation can bypass
  that restriction.

Under **Bypass list**, add only the approved release GitHub App, team or role
that is permitted to create release tags. Avoid a broad write-role bypass. If
the workflow identity is not available as a selectable bypass actor, do not
enable restricted creation until a dedicated approved GitHub App or release
identity has been configured; otherwise the workflow's tag push will fail.

Select **Create** to save the ruleset, then run a controlled release to verify
that automation can create a new tag while ordinary users cannot delete or
force-update it.

## Step 5: Raise a pull request

1. Create a branch from `main`.
2. Make the component changes.
3. Push the branch and create a PR targeting `main`.
4. Review the files changed in the PR.
5. Apply exactly one release label for every affected component.
6. Do not apply a label for an unaffected component.
7. Wait for every required check and approval.
8. Merge only after the release-label validation succeeds.

Do not create Git or ECR tags manually.

## Label-selection rules

| Files changed | Required labels |
|---|---|
| API only | Exactly one `release:api:*` |
| Internal Web only | Exactly one `release:internalweb:*` |
| External Web only | Exactly one `release:externalweb:*` |
| API and Internal Web | One API and one Internal Web label |
| API and External Web | One API and one External Web label |
| Both Web components | One Internal Web and one External Web label |
| All three components | One label for each component |
| Pipeline YAML or documentation only | No component release label |

Changes under `src/PTL.ApiClient/**` or `tests/PTL.ApiClient.Tests/**` affect both
Web applications and therefore require Internal Web and External Web labels.

Changes to these shared solution/build files affect all three components:

```text
PTL.slnx
Directory.Build.*
Directory.Packages.props
NuGet.config
global.json
.github/actions/strip-nuget-cache-mount/**
```

## Selecting patch, minor or major

| Release type | Example | Use when |
|---|---|---|
| Patch | `1.2.3` to `1.2.4` | Backward-compatible defect correction |
| Minor | `1.2.3` to `1.3.0` | Backward-compatible functionality |
| Major | `1.2.3` to `2.0.0` | Breaking or incompatible change |

For a component's first `1.0.0` release, apply its `major` label. Without an
existing component tag, version calculation starts at `0.0.0`.

## PR examples

### API patch only

```text
release:api:patch
```

If the current API tag is `api-v1.2.3`, the workflow creates:

```text
Git tag: api-v1.2.4
ECR tag: v1.2.4-<last-8-digest>
```

### Internal Web feature only

```text
release:internalweb:minor
```

The API and External Web versions remain unchanged.

### Shared API client change

```text
release:internalweb:patch
release:externalweb:patch
```

Both Web images are built and published because both consume the shared API
client code.

### All components changed

```text
release:api:minor
release:internalweb:patch
release:externalweb:minor
```

Each component receives its independently calculated version.

### Workflow-only change

No component label is required. The general quality checks run, but no PT-LIMS
container image or component Git tag is published.

## Automated PR sequence

1. `Detect affected components` calculates the component list.
2. `Validate component release labels` retrieves current PR labels through the
   GitHub API and checks them against the changed components.
3. `Format, build and test solution` validates `PTL.slnx`.
4. `SonarCloud analysis` runs when enabled.
5. `Validate <component> container` builds every affected Windows image.
6. PR runs stop without publishing production images or Git tags.

## Automated sequence after merge

1. Identify the merged PR for the `main` commit.
2. Retrieve and validate the component release labels again.
3. Read the latest `api-v*`, `internalweb-v*` and `externalweb-v*` tags.
4. Calculate each affected component's next semantic version.
5. Reuse the validated container artifact instead of rebuilding it.
6. Assume the AWS publishing role through OIDC.
7. Verify the expected AWS account and Region.
8. Push a unique candidate image to the component ECR repository.
9. Retrieve and validate the full ECR SHA-256 image digest.
10. Create `vX.Y.Z-<last-8-digest>` in the component repository.
11. Verify that the release tag resolves to the validated digest.
12. Create the component Git tags after the publication gate succeeds.
13. Push multiple Git tags atomically when necessary.

## Published identifiers

| Component | Git tag example | ECR tag example |
|---|---|---|
| API | `api-v1.3.0` | `v1.3.0-a1b2c3d4` |
| Internal Web | `internalweb-v2.4.1` | `v2.4.1-e5f6a7b8` |
| External Web | `externalweb-v3.1.0` | `v3.1.0-c9d0e1f2` |

The ECR repositories are separate, so the same semantic version may exist in
more than one repository without conflict.

Deployment automation should resolve the approved ECR tag and deploy the full
SHA-256 digest. Do not deploy a mutable `latest` tag.

## Manual release

Open **Actions > Build, Test and Publish PT-LIMS Images > Run workflow** and
select `main`.

Choose one value for each component:

```text
none
patch
minor
major
```

At least one component must be selected. A manual production release from a
branch other than `main` is rejected.

## Troubleshooting

### Changed component requires exactly one label

The named component has zero or more than one matching label. Remove incorrect
labels and leave exactly one patch, minor or major label for that component.

### A label was added but the rerun still fails

The updated workflow retrieves current labels from GitHub during every
validation run. Confirm the exact label spelling and use **Re-run failed jobs**.

### Label exists for an unchanged component

Remove the label. A release version must not advance when the component did not
change.

### Direct push to main

Application publication must originate from a merged PR. Confirm branch rules
block direct pushes to `main`.

### Git-tag creation denied

Check `contents: write` permission and tag rulesets covering `api-v*`,
`internalweb-v*` and `externalweb-v*`.

### Immutable ECR tag conflict

Do not overwrite the existing tag. Verify the previous publication and create
a new semantic version when appropriate.

## Ownership

| Responsibility | Suggested owner |
|---|---|
| Select release increment | PR author and reviewers |
| Apply PR labels | PR author, reviewer or repository triage role |
| Approve component changes | Component code owners |
| Maintain workflow and GitHub controls | DevOps/platform team |
| Maintain OIDC role and ECR access | Cloud/platform team |
| Approve and deploy an image digest | Release/deployment owner |

## Quick reference

```text
API bug fix             -> release:api:patch
API feature             -> release:api:minor
API breaking change     -> release:api:major
Internal Web bug fix    -> release:internalweb:patch
Internal Web feature    -> release:internalweb:minor
Internal Web breaking   -> release:internalweb:major
External Web bug fix    -> release:externalweb:patch
External Web feature    -> release:externalweb:minor
External Web breaking   -> release:externalweb:major
Pipeline-only change    -> no component release label
```