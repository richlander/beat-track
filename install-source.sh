#!/bin/sh
# Install beat-track from local source (developer workflow).
# Usage: ./install-source.sh
#
# Publishes a Native AOT binary from the local source tree and places
# it in ~/.local/bin/.
#
# Requires the .NET SDK.

set -eu

INSTALL_DIR="${BEAT_TRACK_INSTALL_DIR:-$HOME/.local/bin}"

main() {
    need_cmd dotnet
    need_cmd uname

    get_rid || return 1
    local _rid="$RETVAL"

    echo "=== Installing beat-track from source ==="

    # Publish Native AOT binary
    dotnet publish src/BeatTrack.App -c Release -r "$_rid" --nologo -v:q

    # Copy binary to install directory
    local _pub="artifacts/publish/BeatTrack.App/release_${_rid}"
    local _bin="${_pub}/beat-track"

    if [ ! -f "$_bin" ]; then
        err "binary not found at $_bin"
    fi

    mkdir -p "$INSTALL_DIR"
    cp "$_bin" "$INSTALL_DIR/beat-track"
    chmod +x "$INSTALL_DIR/beat-track"

    say "installed to ${INSTALL_DIR}/beat-track"

    # Check if install dir is on PATH
    case ":${PATH}:" in
        *":${INSTALL_DIR}:"*) ;;
        *)
            say ""
            say "Add ${INSTALL_DIR} to your PATH:"
            say "  export PATH=\"${INSTALL_DIR}:\$PATH\""
            ;;
    esac
}

get_rid() {
    local _os _arch
    _os="$(uname -s)"
    _arch="$(uname -m)"

    case "$_os" in
        Linux)  _os="linux" ;;
        Darwin) _os="osx" ;;
        *)      err "unsupported OS: $_os" ;;
    esac

    case "$_arch" in
        aarch64 | arm64)                _arch="arm64" ;;
        x86_64 | x86-64 | x64 | amd64) _arch="x64" ;;
        *)                              err "unsupported architecture: $_arch" ;;
    esac

    RETVAL="${_os}-${_arch}"
}

say() {
    printf 'beat-track: %s\n' "$1" 1>&2
}

err() {
    say "error: $1"
    exit 1
}

need_cmd() {
    if ! command -v "$1" > /dev/null 2>&1; then
        err "need '$1' (command not found)"
    fi
}

main "$@" || exit 1
