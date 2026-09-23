#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
cmake=${CMAKE:-cmake}
ninja=${NINJA:-ninja}
build=${BUILD_DIR:-"$root/build/ruby-prototype"}
output=${OUTPUT_DIR:-"$build/results"}
: "${ICU_SOURCE:?Set ICU_SOURCE to the exact ext/icu checkout}"
: "${ICU_ARCHIVES:?Set ICU_ARCHIVES to the matching libicu.a directory}"
test "$(git -C "$root/ext/skia" rev-parse HEAD)" = 7184e167115bf8b7000ba1582f57709aaa4b565f
test "$(git -C "$ICU_SOURCE" rev-parse HEAD)" = 457157a92aa053e632cc7fcfd0e12f8a943b2d11
mkdir -p "$root/ext/skia/out/ruby-prototype" "$output"
cp "$root/tests/ruby-prototype/args.gn" "$root/ext/skia/out/ruby-prototype/args.gn"
(cd "$root/ext/skia" && bin/gn gen out/ruby-prototype)
"$ninja" -C "$root/ext/skia/out/ruby-prototype" -j "${JOBS:-6}" skia skshaper
"$cmake" -S "$root/tests/ruby-prototype" -B "$build" -G Ninja \
    -DCMAKE_MAKE_PROGRAM="$ninja" -DCMAKE_BUILD_TYPE=Debug \
    -DICU_SOURCE="$ICU_SOURCE" -DICU_ARCHIVES="$ICU_ARCHIVES"
"$cmake" --build "$build" -j "${JOBS:-6}"
(cd "$output" && "$build/ruby-prototype")
