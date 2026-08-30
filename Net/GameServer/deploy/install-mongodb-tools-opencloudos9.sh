#!/usr/bin/env bash
set -euo pipefail

TOOLS_VERSION="${MONGODB_TOOLS_VERSION:-100.17.0}"

if command -v mongodump >/dev/null 2>&1; then
    echo "MongoDB Database Tools already installed: $(command -v mongodump)"
    mongodump --version
    exit 0
fi

if [[ ! -r /etc/os-release ]]; then
    echo "Cannot detect Linux distribution: /etc/os-release is missing." >&2
    exit 1
fi

# shellcheck disable=SC1091
source /etc/os-release
if [[ "${ID:-}" != "opencloudos" || "${VERSION_ID%%.*}" != "9" ]]; then
    echo "Unsupported system: ID=${ID:-unknown}, VERSION_ID=${VERSION_ID:-unknown}." >&2
    echo "This installer only supports OpenCloudOS 9." >&2
    exit 1
fi

if [[ "${EUID}" -ne 0 ]]; then
    echo "MongoDB Database Tools are missing. Run this installer as root:" >&2
    echo "  sudo bash $0" >&2
    exit 1
fi

case "$(uname -m)" in
    x86_64)
        PLATFORM="rhel93-x86_64"
        ;;
    aarch64|arm64)
        PLATFORM="rhel93-aarch64"
        ;;
    *)
        echo "Unsupported CPU architecture: $(uname -m)" >&2
        exit 1
        ;;
esac

if ! command -v curl >/dev/null 2>&1; then
    dnf install -y curl
fi

WORK_DIR="$(mktemp -d /tmp/mongodb-tools-install.XXXXXX)"
trap 'rm -rf -- "$WORK_DIR"' EXIT

RPM_NAME="mongodb-database-tools-${PLATFORM}-${TOOLS_VERSION}.rpm"
DOWNLOAD_URL="https://fastdl.mongodb.org/tools/db/${RPM_NAME}"
RPM_PATH="${WORK_DIR}/${RPM_NAME}"

echo "Downloading MongoDB Database Tools ${TOOLS_VERSION} for ${PLATFORM}..."
curl --fail --location --proto '=https' --tlsv1.2 \
    --output "${RPM_PATH}" \
    "${DOWNLOAD_URL}"

echo "Installing ${RPM_NAME}..."
dnf install -y "${RPM_PATH}"

if ! command -v mongodump >/dev/null 2>&1; then
    echo "Installation finished but mongodump is not available in PATH." >&2
    exit 1
fi

echo "MongoDB Database Tools installed successfully."
mongodump --version
mongorestore --version
