#!/bin/sh
set -eu
app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ "$#" -eq 0 ] || [ "${1-}" = "--settings" ]; then
    exec "$app_dir/OpenSpidey" "$@"
fi
export PATH="$app_dir:$PATH"
# A native graphics stack needs its own loader and libraries. Mixing new
# graphics drivers with the older bundled Mesa libraries can crash.
native_paths="$app_dir/runtime/sdl3:/lib/i386-linux-gnu:/usr/lib/i386-linux-gnu:/lib32:/usr/lib32:/usr/lib:/lib:$app_dir/runtime/lib32"
for native_loader in /lib/ld-linux.so.2 /lib32/ld-linux.so.2; do
    if [ -x "$native_loader" ] && [ "${LIBGL_ALWAYS_SOFTWARE:-0}" = 0 ]; then
        if "$native_loader" --library-path "$native_paths" \
            --preload libSDL3.so.0 \
            "$app_dir/runtime/graphics-check" \
            && "$native_loader" --library-path "$native_paths" \
                --preload libSDL3.so.0 --list "$app_dir/spider.bin" >/dev/null; then
            printf '%s\n' 'OpenSpidey: using native hardware graphics.'
            exec "$native_loader" --library-path "$native_paths" \
                --preload libSDL3.so.0 \
                "$app_dir/spider.bin" "$@"
        fi
        break
    fi
done
printf '%s\n' 'OpenSpidey: using the bundled graphics runtime.'
export LIBGL_DRIVERS_PATH="$app_dir/runtime/lib32/dri"
exec "$app_dir/runtime/ld-linux.so.2" --library-path "$app_dir/runtime/lib32" "$app_dir/spider.bin" "$@"
