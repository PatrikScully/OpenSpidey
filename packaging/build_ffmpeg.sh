#!/usr/bin/env bash
set -euo pipefail

# Only Bink input and the game's raw video / PCM output are needed.
repo_dir=$(cd "$(dirname "$0")/.." && pwd)
build_dir=${1:?Provide a build directory}
install_dir=${2:?Provide an install directory}
target=${3:-linux}
mkdir -p "$build_dir" "$install_dir"
build_dir=$(cd "$build_dir" && pwd)
install_dir=$(cd "$install_dir" && pwd)
archive=$("${SPIDEY_PYTHON:-python3}" "$repo_dir/packaging/download.py" ffmpeg "$build_dir/downloads")
archive=${archive%$'\r'}
if command -v cygpath >/dev/null 2>&1; then
  archive=$(cygpath -u "$archive")
fi
tar -xf "$archive" -C "$build_dir"
source_dir="$build_dir/ffmpeg-8.0.1"
cd "$source_dir"
platform_args=()
if [[ "$target" == windows ]]; then
  platform_args+=(--target-os=mingw32 --arch=x86 --cc=gcc --cxx=g++ --disable-pthreads --enable-w32threads)
elif [[ "$target" == windows-cross ]]; then
  platform_args+=(--target-os=mingw32 --arch=x86 --cross-prefix=i686-w64-mingw32- --enable-cross-compile --disable-pthreads --enable-w32threads)
fi
./configure --prefix="$install_dir" --disable-autodetect --disable-everything \
  --disable-doc --disable-debug --disable-network --disable-x86asm \
  --disable-shared --enable-static --disable-avdevice \
  --disable-ffplay --disable-ffprobe --enable-ffmpeg \
  --enable-decoder=bink,binkaudio_dct,binkaudio_rdft \
  --enable-demuxer=bink --enable-protocol=file,pipe \
  --enable-encoder=rawvideo,pcm_s16le --enable-muxer=rawvideo,pcm_s16le \
  --enable-filter=aformat,aresample,format,scale,null,anull \
  "${platform_args[@]}"
make -j"${SPIDEY_BUILD_JOBS:-8}"
make install
mkdir -p "$install_dir/share/openspidey/ffmpeg"
cp COPYING.LGPLv2.1 "$install_dir/share/openspidey/ffmpeg/"
cp "$archive" "$install_dir/share/openspidey/ffmpeg/"
cp ffbuild/config.log config.h "$install_dir/share/openspidey/ffmpeg/"
cp "$repo_dir/packaging/build_ffmpeg.sh" "$install_dir/share/openspidey/ffmpeg/"

mkdir -p "$install_dir/share/openspidey/ffmpeg/packaging"
cp "$repo_dir/packaging/build_ffmpeg.sh" "$repo_dir/packaging/download.py" "$repo_dir/packaging/versions.json" "$install_dir/share/openspidey/ffmpeg/packaging/"
