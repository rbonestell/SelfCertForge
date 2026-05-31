---
name: github-release
description: Create a GitHub release with auto-generated release notes matching the style of previous releases
allowed-tools: Bash, Read, Write, Edit, AskUserQuestion
argument-hint: "<version> (e.g., 1.2.3 or v1.2.3)"
---

# GitHub Release

## Input

**Version argument**: `$ARGUMENTS`
**Working directory**: !`pwd`

## Git Safety Rules

- This command creates tags and GitHub releases — confirm with the user before executing
- **NEVER** force-push, delete tags, or delete releases without explicit user request
- All tags and versions MUST have a leading `v` prefix (e.g., `v1.2.3`)

---

## Step 1: Parse & Validate Version

1. Extract the version from `$ARGUMENTS`
2. Strip leading `v` if present, then re-add it — the canonical version is always `v{major}.{minor}.{patch}`
3. Validate it looks like a semver version (e.g., `1.2.3`, `v1.2.3`)
4. If invalid or missing, ask the user for a valid version using `AskUserQuestion`
5. Confirm the repo has a GitHub remote: `git remote get-url origin`

## Step 2: Identify Current Repo Context

Run these commands to understand the repo:

```bash
# Get the GitHub owner/repo
gh repo view --json nameWithOwner -q '.nameWithOwner'

# Get the default branch
git symbolic-ref refs/remotes/origin/HEAD | sed 's@^refs/remotes/origin/@@'

# Get the latest 2 releases for style reference
gh release list --limit 2

# Get the most recent release tag
gh release view --json tagName -q '.tagName'
```

## Step 3: Study Previous Release Style

1. Fetch the last 2 releases' notes:
   ```bash
   gh release view <tag1> --json body -q '.body'
   gh release view <tag2> --json body -q '.body'
   ```
2. Analyze the format: heading style, grouping (features/fixes/etc.), bullet format, whether commit hashes or PR numbers are included, any intro/outro text, emoji usage, contributor mentions, etc.
3. Note the pattern so the new release notes match it precisely.

## Step 4: Gather Changes Since Last Release

1. Get the diff between the most recent release tag and the current HEAD of `main`:

   ```bash
   # Commit log
   git log <latest-tag>..origin/main --oneline --no-merges

   # Optionally, also check merge commits for PR titles
   git log <latest-tag>..origin/main --merges --oneline
   ```

2. If there are merged PRs, fetch their titles/numbers:
   ```bash
   gh pr list --state merged --base main --search "merged:>=$(git log -1 --format=%aI <latest-tag>)" --limit 50 --json number,title,labels
   ```
3. Collect the full picture of what changed.

## Step 5: Compose Release Notes

**Audience**: Release notes are **public-facing**. Write for users and integrators of the project, not for the developers who wrote the code. Treat them like marketing-adjacent changelog entries: clear, benefit-oriented, scannable.

**Tone & content rules** (apply to every bullet):

- ✅ DO describe **features, capabilities, behaviors, and user-visible changes** — what's new, what's fixed, what's improved from the user's perspective
- ✅ DO use plain language a non-author can understand without reading the diff
- ✅ DO group by user impact (e.g., "New Features", "Bug Fixes", "Performance", "Breaking Changes") if previous releases did
- ❌ DO NOT mention specific **file paths, module names, class names, method/function names, variable names, or line numbers**
- ❌ DO NOT reference internal implementation details (refactors, type renames, internal helpers, test scaffolding, lint fixes, dependency bumps that don't affect users)
- ❌ DO NOT paste raw commit subjects verbatim — rewrite them in user-facing language
- ❌ DO NOT include internal ticket IDs (e.g., `SB-123`) unless previous releases established that convention
- ✅ DO keep PR numbers and contributor mentions **only if** previous releases include them (style-match)
- ✅ DO omit purely internal commits entirely — if a change has no user-visible effect, it doesn't belong in the notes

**Translation examples** (commit/PR title → release note bullet):

| Internal phrasing (avoid)                                     | Public-facing phrasing (use)                              |
| ------------------------------------------------------------- | --------------------------------------------------------- |
| `refactor(executor-registry.ts): extract ToolExecutorFactory` | _(omit — internal refactor)_                              |
| `fix: null check in UserService.findById at line 42`          | Fixed a crash when looking up users that no longer exist  |
| `feat(mcp): add searchSymbols.limit param`                    | Symbol search now supports limiting the number of results |
| `perf: cache Neo4j session in IndexService`                   | Faster indexing for large projects                        |
| `chore: bump @constellationdev/types to 1.4.0`                | _(omit unless it changes public API)_                     |

**Style-matching rules**:

1. Write release notes that **match the exact format and style** observed in Step 3 (headings, bullet style, emoji usage, intro/outro text)
2. Group changes the same way previous releases did
3. Match contributor/PR-reference conventions from prior releases

**Review gate**:

5. Present the draft to the user for review using `AskUserQuestion` with options:
   - "Looks good, create the release"
   - "Let me edit first" (then ask for their edits)

## Step 6: Create the Release

Once the user approves:

```bash
gh release create v{version} \
  --target main \
  --title "v{version}" \
  --notes "<approved-release-notes>"
```

Report the release URL back to the user:

```bash
gh release view v{version} --json url -q '.url'
```

---

## Error Handling

- If `gh` CLI is not authenticated, tell the user to run `! gh auth login`
- If there are no previous releases, ask the user what format they'd like and use that
- If the tag already exists, inform the user and ask how to proceed
- If there are no changes since the last release, warn the user before proceeding
