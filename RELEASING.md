# Releasing Cleanuparr

Versioning rules and the release process are detailed in the user-facing version of this policy at [docs/docs/installation/versioning.mdx](docs/docs/installation/versioning.mdx) and published under [Versioning](https://cleanuparr.github.io/docs/installation/versioning).

## Versioning

This policy takes effect starting with **v2.10.0**. Previous releases did not follow this approach, and their version numbers have no compatibility meaning.

| Part | Bump when |
| --- | --- |
| Major | Probably never, unless something significant changes. |
| Minor | The release contains a breaking change. |
| Patch | Anything else: bug fixes and new features. |

## Deciding the number

Every pull request includes a category label, which is enforced by [pr-label.yml](.github/workflows/pr-label.yml). The `breaking` label determines the version number.

From the `code` directory, use this command to list every breaking pull request that was merged since the last release and indicate which number to bump:

```bash
make release-check
```

To view the full release notes for a version before tagging it, without creating anything:

```bash
make release-notes version=2.11.0
```

A `## Breaking Changes` section means the release is a minor version. If this section is absent, the release is a patch.

[release.yml](.github/workflows/release.yml) performs the same check and stops the release process before anything is built if breaking pull requests were merged but the minor version did not increment. It issues a warning but continues if the minor version moves without a breaking pull request leading it.

Examples of breaking changes:

- A migration that drops or rewrites data, preventing rollback to the previous version
- Renaming or removing an environment variable
- Dropping a platform, architecture, installer, or database provider
- A default value that alters how an existing configuration interacts with user data
- A setting whose unit or interpretation changes and cannot be altered by a migration
- Dropping a download client or *arr application, or increasing the minimum supported version
- A change to the public REST API (e.g., the `stats` endpoint)

Examples of non-breaking changes:

- A migration that simply adds columns or tables
- Renaming any value stored in the database and configured through the UI, accompanied by a migration
- Any change to the private REST API, which is intended only for the UI
- Modifications to the Web UI layout, internal structure, or logging

## Auditing migrations before a release

The easiest breaking change to overlook is a migration that prevents rollback. To see the migrations added since the previous tag:

```bash
git diff --name-only <last-tag>..main -- '*/Migrations/**' \
  | grep -v -e Designer -e Snapshot \
  | xargs grep -l 'DropColumn\|DropTable\|AlterColumn\|RenameColumn\|DeleteData\|migrationBuilder\.Sql'
```

Only `Up()` matters, since every migration drops things in `Down()` by definition, leading to false positives in that grep.

Additive changes are safe. `AddColumn` and `CreateTable` allow previous versions to start. However, a conversion or rewrite does not.

## Release process

Tag pushes that match `v*.*.*` trigger [release.yml](.github/workflows/release.yml).

```bash
git tag v2.10.0
git push origin v2.10.0
```

### Release notes

GitHub generates notes from the pull requests merged since the last tag, grouped into sections by label according to [release.yml](.github/release.yml):

| Label | Section |
| --- | --- |
| `breaking` | Breaking Changes |
| `enhancement` | Features |
| `bug` | Bug Fixes |
| `documentation` | Documentation |
| `chore` | Maintenance |

The status check for `Require category label` is mandatory on `main`, so a missing label will be caught before merging. Labels added after merging still apply because notes are generated at release time.