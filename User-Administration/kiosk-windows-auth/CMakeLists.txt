cmake_minimum_required(VERSION 3.15)
project(NexorsysCredentialProvider VERSION 1.0 LANGUAGES CXX)

# Settings for Windows DLL
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /DEF:${CMAKE_CURRENT_SOURCE_DIR}/NexorsysProvider.def")
set(CMAKE_CXX_STANDARD 17)

# Statically link the Visual C++ runtime to avoid missing VCRUNTIME140.dll on target machines
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

# Add definitions
add_definitions(-D_UNICODE -DUNICODE)

# Include directories
include_directories(include)

# Source files
set(SOURCES
    src/dllmain.cpp
    src/NexorsysCredentialProvider.cpp
    src/NexorsysCredential.cpp
    src/CryptoHelper.cpp
    src/SmartCardReader.cpp
    resources.rc
)

set(HEADERS
    include/guid.h
    include/common.h
    include/NexorsysCredentialProvider.h
    include/NexorsysCredential.h
    include/CryptoHelper.h
    include/SmartCardReader.h
)

# Create the DLL
add_library(NexorsysCredentialProvider SHARED ${SOURCES} ${HEADERS})

# Link against required Windows libraries
target_link_libraries(NexorsysCredentialProvider 
    Secur32.lib 
    Credui.lib 
    Shlwapi.lib 
    crypt32.lib 
    Bcrypt.lib
    Ncrypt.lib
    Winscard.lib
    Winhttp.lib
)
