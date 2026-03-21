#!/bin/sh
set -e

INPUT_FILE="$1"
OUTPUT_FILE="$2"

if [ -z "$INPUT_FILE" ] || [ -z "$OUTPUT_FILE" ]; then
    echo "Usage: $0 <input.json> <output.json>"
    exit 1
fi

if [ ! -f "$INPUT_FILE" ]; then
    echo "Input file not found: $INPUT_FILE"
    exit 1
fi

FIELDS=$(jq -r '
  # recursive traversal of properties
  def collect(prefix; node):
    if (node | type) != "object" then
      empty
    else
      node
      | to_entries[]
      | . as $e

      # if a value has a type and it is not object/nested, then it is a leaf field
      | if ($e.value.type? // "" | IN("object","nested") | not) then
          # skip keyword multi-fields
          if $e.key == "keyword" then empty
          else (prefix + $e.key)
          end

        # if it is object/nested, recursively traverse properties
        else
          ($e.value.properties? // {}) as $props
          | collect(prefix + $e.key + "."; $props)
        end
    end
  ;

  collect("" ; .mappings.properties)
' "$INPUT_FILE")

if [ -z "$FIELDS" ]; then
  FIELDS_JSON="[]"
else
  FIELDS_JSON=$(printf "%s\n" "$FIELDS" | jq -R . | jq -s .)
fi

jq --argjson includes "$FIELDS_JSON" '
  .mappings["_source"] = { "includes": $includes }
' "$INPUT_FILE" > "$OUTPUT_FILE"

echo "Generated: $OUTPUT_FILE"
