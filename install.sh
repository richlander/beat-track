#!/bin/sh
# Install script for beat-track.
# Usage: curl --proto '=https' --tlsv1.2 -sSf https://raw.githubusercontent.com/richlander/beat-track/main/install.sh | sh
#
# Downloads a pre-built Native AOT binary from GitHub Releases
# and places it in ~/.local/bin/.
#
# Environment variables:
#   BEAT_TRACK_FEED        Override the download base URL
#   BEAT_TRACK_INSTALL_DIR Override the install directory (default: ~/.local/bin)

set -u

FEED="${BEAT_TRACK_FEED:-https://github.com/richlander/beat-track/releases/download}"
INSTALL_DIR="${BEAT_TRACK_INSTALL_DIR:-$HOME/.local/bin}"

main() {
    downloader --check
    need_cmd uname
    need_cmd tar
    need_cmd mktemp
    need_cmd chmod
    need_cmd mkdir
    need_cmd rm

    get_rid || return 1
    local _rid="$RETVAL"
    assert_nz "$_rid" "rid"

    local _version="0.1.2"
    local _url="${FEED}/v${_version}/beat-track-${_version}-${_rid}.tar.gz"

    local _dir
    _dir="$(ensure mktemp -d)"
    local _archive="${_dir}/beat-track.tar.gz"

    say "downloading beat-track ${_version} (${_rid})"

    ensure mkdir -p "$_dir"
    ensure downloader "$_url" "$_archive" "$_rid"
    ensure tar -xzf "$_archive" -C "$_dir"

    local _bin="${_dir}/beat-track"
    ensure chmod u+x "$_bin"

    if [ ! -x "$_bin" ]; then
        err "cannot execute $_bin (may be noexec mount)"
    fi

    # Place binary in install directory
    ensure mkdir -p "$INSTALL_DIR"
    ensure cp "$_bin" "$INSTALL_DIR/beat-track"
    ensure chmod +x "$INSTALL_DIR/beat-track"

    say "installed to ${INSTALL_DIR}/beat-track"

    # Check if install dir is on PATH
    case ":${PATH}:" in
        *":${INSTALL_DIR}:"*) ;;
        *)
            say ""
            say "Add ${INSTALL_DIR} to your PATH:"
            say "  export PATH=\"${INSTALL_DIR}:\$PATH\""
            say ""
            say "Or add it to your shell profile (~/.bashrc, ~/.zshrc, etc.)"
            ;;
    esac

    # Clean up
    rm -rf "$_dir"

    return 0
}

get_rid() {
    local _os _arch _libc
    _os="$(uname -s)"
    _arch="$(uname -m)"
    _libc=""

    case "$_os" in
        Linux)
            _os="linux"
            if ldd --version 2>&1 | grep -q 'musl'; then
                _libc="-musl"
            fi
            ;;
        Darwin)
            _os="osx"
            ;;
        *)
            err "unsupported OS: $_os"
            ;;
    esac

    case "$_arch" in
        aarch64 | arm64)
            _arch="arm64"
            ;;
        x86_64 | x86-64 | x64 | amd64)
            _arch="x64"
            ;;
        *)
            err "unsupported architecture: $_arch"
            ;;
    esac

    RETVAL="${_os}${_libc}-${_arch}"
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

assert_nz() {
    if [ -z "$1" ]; then err "assert_nz $2"; fi
}

ensure() {
    if ! "$@"; then err "command failed: $*"; fi
}

downloader() {
    local _dld
    if command -v curl > /dev/null 2>&1; then
        _dld=curl
    elif command -v wget > /dev/null 2>&1; then
        _dld=wget
    else
        _dld='curl or wget'
    fi

    if [ "$1" = --check ]; then
        need_cmd "$_dld"
    elif [ "$_dld" = curl ]; then
        curl --proto '=https' --tlsv1.2 \
            --silent --show-error --fail --location \
            --retry 3 \
            "$1" --output "$2" || {
            err "download failed for platform '$3'"
        }
    elif [ "$_dld" = wget ]; then
        wget --https-only --secure-protocol=TLSv1_2 \
            "$1" -O "$2" || {
            err "download failed for platform '$3'"
        }
    fi
}

main "$@" || exit 1
