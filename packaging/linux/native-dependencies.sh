#!/usr/bin/env bash
# Sourced by the launcher. No installation, root privileges or Python required.
morsecore_check_native_library() {
    local library="$1" search_path="$2" cache="$3"
    local directory candidate filename line output
    local -a directories candidates
    IFS=: read -r -a directories <<< "$search_path"
    for directory in "${directories[@]}"; do
        # Ignore empty/relative search entries; do not silently load from cwd.
        [[ "$directory" == /* && -d "$directory" ]] || continue
        if [[ "$library" == libicuuc.so ]]; then
            candidates=("$directory"/libicuuc.so*)
        else
            candidates=("$directory/$library")
        fi
        for candidate in "${candidates[@]}"; do
            [[ -r "$candidate" && -f "$candidate" ]] || continue
            filename="${candidate##*/}"
            if [[ "$library" == libicuuc.so && ! "$filename" =~ ^libicuuc\.so(\.[0-9]+)*$ ]]; then continue; fi
            output="$(LD_LIBRARY_PATH="$search_path" ldd "$candidate" 2>&1)" || return 1
            [[ "$output" != *'not found'* ]] || return 1
            return 0
        done
    done
    while IFS= read -r line; do
        filename="${line#"${line%%[![:space:]]*}"}"
        filename="${filename%%[[:space:]]*}"
        if [[ "$library" == libicuuc.so ]]; then
            [[ "$filename" =~ ^libicuuc\.so(\.[0-9]+)*$ ]] || continue
        else
            [[ "$filename" == "$library" ]] || continue
        fi
        [[ "$line" == *' => '* ]] || continue
        candidate="${line##* => }"
        [[ "$candidate" == /* && -r "$candidate" && -f "$candidate" ]] || continue
        output="$(LD_LIBRARY_PATH="$search_path" ldd "$candidate" 2>&1)" || return 1
        [[ "$output" != *'not found'* ]] || return 1
        return 0
    done <<< "$cache"
    return 1
}
