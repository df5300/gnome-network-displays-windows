#!/bin/bash
# setup-mingw-cross.sh
# Setup script for MinGW-w64 cross-compilation environment for Windows
# This script sets up the environment for cross-compiling GTK4 applications

set -e

TARGET_DIR="${HOME}/cross-windows"
PREFIX="${TARGET_DIR}/usr/x86_64-w64-mingw32"

echo "=========================================="
echo "MinGW-w64 Cross-Compilation Setup"
echo "=========================================="

# Create target directory
mkdir -p "${PREFIX}"

# Check for required tools
echo "Checking MinGW toolchain..."
if ! command -v x86_64-w64-mingw32-gcc &> /dev/null; then
    echo "ERROR: x86_64-w64-mingw32-gcc not found"
    echo "Install with: sudo apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64"
    exit 1
fi

echo "MinGW toolchain found: $(x86_64-w64-mingw32-gcc --version | head -1)"

# Download GTK4 Windows build from wingtk
echo ""
echo "Downloading GTK4 Windows libraries..."
cd "${TARGET_DIR}"

# Try multiple sources for GTK4
GTK4_URLS=(
    "https://github.com/wingtk/gtk-win32/releases/download/vc143/gtk-build.zip"
    "https://github.com/wingtk/gtk-win32/releases/download/vc142/gtk-build.zip"
)

GTK4_DOWNLOADED=false
for url in "${GTK4_URLS[@]}"; do
    echo "Trying: ${url}"
    if curl -L -o gtk-build.zip "${url}" 2>&1 | tail -3; then
        if [ -s gtk-build.zip ] && [ "$(stat -c%s gtk-build.zip 2>/dev/null || echo 0)" -gt 1000 ]; then
            echo "Downloaded successfully"
            GTK4_DOWNLOADED=true
            break
        fi
    fi
done

if [ "$GTK4_DOWNLOADED" = false ]; then
    echo ""
    echo "WARNING: Could not download GTK4 from automated sources."
    echo "Please manually download GTK4 Windows from:"
    echo "  https://github.com/wingtk/gtk-win32/releases"
    echo ""
    echo "Or use MSYS2 to install and repackage:"
    echo "  pacman -S mingw-w64-x86_64-gtk4"
    echo "  cd /mingw64 && tar czf ~/gtk4-mingw.tar.gz lib/*.dll lib/*.a lib/pkgconfig share/"
    echo ""
    echo "Continuing without GTK4..."
fi

# Try downloading GStreamer
echo ""
echo "Downloading GStreamer Windows libraries..."
GST_URLS=(
    "https://github.com/wingtk/gstreamer/releases/download/vc143-1.18.0/gstreamer-1.0-msvc-1.18.0-x86_64.zip"
    "https://github.com/wingtk/gst-plugins-base/releases/download/vc143-1.18.0/gst-plugins-base-1.18.0-msvc-x86_64.zip"
)

for url in "${GST_URLS[@]}"; do
    echo "Trying: ${url}"
    filename=$(basename "${url}")
    if curl -L -o "${filename}" "${url}" 2>&1 | tail -3; then
        if [ -s "${filename}" ] && [ "$(stat -c%s "${filename}" 2>/dev/null || echo 0)" -gt 1000 ]; then
            echo "Downloaded ${filename}"
        fi
    fi
done

# Create Meson cross-compilation file
echo ""
echo "Creating Meson cross-compilation file..."
mkdir -p "${TARGET_DIR}/meson"

cat > "${TARGET_DIR}/meson/windows-mingw64.txt" << 'CROSSFILE'
[binaries]
c = 'x86_64-w64-mingw32-gcc'
cpp = 'x86_64-w64-mingw32-g++'
ar = 'x86_64-w64-mingw32-ar'
strip = 'x86_64-w64-mingw32-strip'
pkg-config = 'pkg-config'
cmake = '/usr/bin/cmake'

[host_machine]
system = 'windows'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'

[properties]
sys_root = '/home/'{{USERNAME}}'/cross-windows/usr/x86_64-w64-mingw32'
prefix = '/home/'{{USERNAME}}'/cross-windows/usr/x86_64-w64-mingw32'
pkgincludedir = '/home/'{{USERNAME}}'/cross-windows/usr/x86_64-w64-mingw32/include'
pkglibdir = '/home/'{{USERNAME}}'/cross-windows/usr/x86_64-w64-mingw32/lib'

[paths]
prefix = 'C:/cross-windows/usr/x86_64-w64-mingw32'
libdir = 'lib'
includedir = 'include'
CROSSFILE

echo "Created: ${TARGET_DIR}/meson/windows-mingw64.txt"

# Create a simple test project
echo ""
echo "Creating test project..."
mkdir -p "${TARGET_DIR}/test-project"
cat > "${TARGET_DIR}/test-project/meson.build" << 'MESON'
project('test', 'c')

gtk4_dep = dependency('gtk4', required: false)
if gtk4_dep.found()
    message('GTK4 found!')
else
    message('GTK4 not found - install Windows libraries first')
endif
MESON

cat > "${TARGET_DIR}/test-project/main.c" << 'MAIN'
#include <stdio.h>
int main() {
#ifdef _WIN32
    printf("Windows build!\n");
#else
    printf("This should not happen\n");
#endif
    return 0;
}
MAIN

echo ""
echo "=========================================="
echo "Setup complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Download GTK4 Windows libraries from:"
echo "   https://github.com/wingtk/gtk-win32/releases"
echo ""
echo "2. Extract to: ${PREFIX}"
echo ""
echo "3. Build test project:"
echo "   cd ${TARGET_DIR}/test-project"
echo "   meson setup build --cross-file=${TARGET_DIR}/meson/windows-mingw64.txt"
echo "   ninja -C build"
echo ""
echo "Documentation: ${TARGET_DIR}/../src-windows/MSYS2-BUILD.md"
