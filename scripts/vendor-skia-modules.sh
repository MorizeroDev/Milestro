#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
readonly REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd -P)"

skia_source="${REPO_ROOT}/ext/skia"
skia_base=""
dry_run=false

usage() {
    cat <<'EOF'
Usage: scripts/vendor-skia-modules.sh [--source PATH] [--base COMMIT] [--dry-run]

Vendor Skia's committed modules/skunicode and modules/skparagraph trees into
ext/skmodules/modules.

Options:
  --source PATH  Skia checkout to read (default: ext/skia)
  --base COMMIT  Compatible Skia core commit (default: existing metadata or HEAD)
  --dry-run      Show changes without updating vendored files
  -h, --help     Show this help
EOF
}

while (($# > 0)); do
    case "$1" in
        --source)
            if (($# < 2)); then
                echo "error: --source requires a path" >&2
                exit 2
            fi
            skia_source="$2"
            shift 2
            ;;
        --dry-run)
            dry_run=true
            shift
            ;;
        --base)
            if (($# < 2)); then
                echo "error: --base requires a commit" >&2
                exit 2
            fi
            skia_base="$2"
            shift 2
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "error: unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

for command in git mktemp rsync sed tar; do
    if ! command -v "${command}" >/dev/null 2>&1; then
        echo "error: required command not found: ${command}" >&2
        exit 1
    fi
done

if ! git -C "${skia_source}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "error: not a Git checkout: ${skia_source}" >&2
    exit 1
fi

readonly skia_commit="$(git -C "${skia_source}" rev-parse --verify HEAD)"
readonly metadata_path="${REPO_ROOT}/ext/skmodules/vendor-meta"

if [[ -z "${skia_base}" && -f "${metadata_path}" ]]; then
    skia_base="$(sed -n 's/^skia-base: //p' "${metadata_path}")"
fi
if [[ -z "${skia_base}" ]]; then
    skia_base="${skia_commit}"
fi
if ! skia_base="$(git -C "${skia_source}" rev-parse --verify "${skia_base}^{commit}")"; then
    echo "error: invalid Skia base commit" >&2
    exit 1
fi
readonly skia_base

for module in skunicode skparagraph; do
    if ! git -C "${skia_source}" cat-file -e "${skia_commit}:modules/${module}" 2>/dev/null; then
        echo "error: modules/${module} does not exist at ${skia_commit}" >&2
        exit 1
    fi
done

if ! git -C "${skia_source}" merge-base --is-ancestor "${skia_base}" "${skia_commit}"; then
    echo "error: Skia base ${skia_base} is not an ancestor of ${skia_commit}" >&2
    exit 1
fi
if ! git -C "${skia_source}" diff --quiet "${skia_base}..${skia_commit}" -- \
    . ':(exclude)modules/skunicode' ':(exclude)modules/skparagraph'; then
    echo "error: changes after the Skia base are not confined to the vendored modules" >&2
    exit 1
fi

if [[ -n "$(git -C "${skia_source}" status --porcelain --untracked-files=all -- \
    modules/skunicode modules/skparagraph)" ]]; then
    echo "error: Skia modules have uncommitted changes; commit or stash them before vendoring" >&2
    exit 1
fi

readonly temporary_dir="$(mktemp -d "${TMPDIR:-/tmp}/milestro-skia-vendor.XXXXXX")"
trap 'rm -rf "${temporary_dir}"' EXIT

git -C "${skia_source}" archive "${skia_commit}" \
    LICENSE modules/skunicode modules/skparagraph | tar -xf - -C "${temporary_dir}"

# Milestro compiles the vendored SkUnicode implementation against headers from
# the matching Skia checkout, so keep the existing source-only vendor layout.
rm -rf "${temporary_dir}/modules/skunicode/include"

printf 'skia-base: %s\nsource-commit: %s\n' "${skia_base}" "${skia_commit}" \
    >"${temporary_dir}/vendor-meta"

sync_module() {
    local module="$1"
    local destination="$2"

    if [[ "${dry_run}" == true ]]; then
        if [[ ! -d "${destination}" ]]; then
            echo "Only in source: ${destination}"
        else
            # Apple openrsync itemizes unchanged checksummed files as a
            # timestamp-only difference even without -t; filter only that row.
            rsync -rlpcni --delete \
                "${temporary_dir}/modules/${module}/" \
                "${destination}/" | sed '/^\.f\.\.T\.\.\.\. /d'
        fi
    else
        mkdir -p "${destination}"
        rsync -rlpc --delete \
            "${temporary_dir}/modules/${module}/" \
            "${destination}/"
    fi
}

echo "Vendoring Skia ${skia_commit}"

sync_module skunicode "${REPO_ROOT}/ext/skmodules/modules/skunicode"
sync_module skparagraph "${REPO_ROOT}/ext/skmodules/modules/skparagraph"

if [[ "${dry_run}" == true ]]; then
    rsync -pcni "${temporary_dir}/LICENSE" "${REPO_ROOT}/ext/skmodules/LICENSE" |
        sed '/^\.f\.\.T\.\.\.\. /d'
    rsync -pcni "${temporary_dir}/vendor-meta" "${metadata_path}" |
        sed '/^\.f\.\.T\.\.\.\. /d'
else
    rsync -pc "${temporary_dir}/LICENSE" "${REPO_ROOT}/ext/skmodules/LICENSE"
    rsync -pc "${temporary_dir}/vendor-meta" "${metadata_path}"
fi

if [[ "${dry_run}" == true ]]; then
    echo "Dry run complete; no files were changed."
else
    echo "Updated ext/skmodules."
fi
