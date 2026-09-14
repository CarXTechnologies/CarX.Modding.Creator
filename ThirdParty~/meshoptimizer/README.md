meshoptimizer v1.2
Upstream: https://github.com/zeux/meshoptimizer
Commit: 9d9890c73011d75920af614485296d1e03e95448
License: MIT, see LICENSE.md.

The source is vendored for reproducible SDK builds. The prebuilt Windows x64 library
is Editor-only; no native dependency is introduced into the client or exported mod.

Build from this directory (Visual Studio 2022 C++ and CMake):
cmake -S . -B build -G "Visual Studio 17 2022" -A x64 -DMESHOPT_BUILD_SHARED_LIBS=ON -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
cmake --build build --config Release
Copy build/Release/meshoptimizer.dll to ../../Editor/Native/meshoptimizer.dll.
Other editor operating systems require their own native build before using optimization.
