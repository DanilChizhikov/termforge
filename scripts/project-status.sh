#!/usr/bin/env bash
set -euo pipefail

owner='DanilChizhikov'
project=31
repository='DanilChizhikov/termforge'
field='Status'
status=''
issues=()

usage() {
  echo 'Usage: project-status.sh -i <issue> [-i <issue>...] -s <status> [-o owner] [-p project] [-R owner/repo] [-h]'
  echo 'Sets the Status field of repository issues in a GitHub Project. Requires gh with the project scope.'
}

while getopts 'i:s:o:p:R:h' option; do
  case "$option" in
    i) issues+=("$OPTARG") ;;
    s) status="$OPTARG" ;;
    o) owner="$OPTARG" ;;
    p) project="$OPTARG" ;;
    R) repository="$OPTARG" ;;
    h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done
shift $((OPTIND - 1))
[[ $# -eq 0 ]] || { echo 'error: unexpected argument' >&2; exit 2; }
[[ ${#issues[@]} -gt 0 ]] || { echo 'error: at least one -i issue is required' >&2; exit 2; }
[[ -n "$status" ]] || { echo 'error: -s status is required' >&2; exit 2; }
for issue in "${issues[@]}"; do
  [[ "$issue" =~ ^[0-9]+$ ]] || { echo "error: invalid issue number: $issue" >&2; exit 2; }
done

project_id="$(gh project view "$project" --owner "$owner" --format json --jq '.id')"
fields="$(gh project field-list "$project" --owner "$owner" --format json --limit 100)"
field_id="$(jq -r --arg field "$field" '.fields[] | select(.name == $field) | .id' <<<"$fields")"
[[ -n "$field_id" ]] || { echo "error: field '$field' not found in project $project" >&2; exit 1; }
option_id="$(jq -r --arg field "$field" --arg status "$status" '.fields[] | select(.name == $field) | .options[] | select(.name == $status) | .id' <<<"$fields")"
if [[ -z "$option_id" ]]; then
  echo "error: status '$status' not found; available: $(jq -r --arg field "$field" '[.fields[] | select(.name == $field) | .options[].name] | join(", ")' <<<"$fields")" >&2
  exit 1
fi
items="$(gh project item-list "$project" --owner "$owner" --format json --limit 1000)"

failed=0
for issue in "${issues[@]}"; do
  item_id="$(jq -r --arg repository "$repository" --argjson number "$issue" '.items[] | select(.content.type == "Issue" and .content.repository == $repository and .content.number == $number) | .id' <<<"$items")"
  if [[ -z "$item_id" ]]; then
    echo "error: issue #$issue of $repository is not in project $project" >&2
    failed=1
    continue
  fi
  gh project item-edit --id "$item_id" --project-id "$project_id" --field-id "$field_id" --single-select-option-id "$option_id" >/dev/null
  echo "#$issue: $status"
done
exit "$failed"
