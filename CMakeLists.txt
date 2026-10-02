cmake_minimum_required(VERSION 3.5)
project(spider LANGUAGES CXX)

message ("Will create project for decomp! ${CMAKE_SYSTEM_PROCESSOR}")


if (MSVC)
	message("Creating Windows project")
	if(NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
		message(FATAL_ERROR "Please switch to x32 build.")
	endif()
else()
	string (APPEND CMAKE_CXX_FLAGS " -w -fpermissive -std=c++11")
	if (CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
		message("Creating for G++")
		# MSVC __inline functions are still callable from other translation
		# units. Make GCC emit them too (see INLINE in my_types.h).
		string (APPEND CMAKE_CXX_FLAGS " -fkeep-inline-functions")
		# CBit and CClass clear object storage in operator new, as the
		# original binary does (CBit: 0x4088A0). Constructors rely on it.
		# Preserve those stores when allocation and construction are inlined.
		string (APPEND CMAKE_CXX_FLAGS " -flifetime-dse=1")
	elseif (CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
		message("Creating for Clang++")
		string (APPEND CMAKE_CXX_FLAGS " -fms-extensions -Wno-address-of-temporary")
	else()
		message(FATAL_ERROR "Could not detect compiler")
	endif()

	if (MINGW)
		if (NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
			message(FATAL_ERROR "The native Windows game needs a 32 bit MinGW toolchain.")
		endif()
		find_package(ZLIB REQUIRED)
		set(LIBZ_NAME ZLIB::ZLIB)
	elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL "x86_64")
		string (APPEND CMAKE_CXX_FLAGS  " -m32")
		set (LIBZ_NAME ${CMAKE_SOURCE_DIR}/lib/libz_x86.a)
	elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64")
		set (LIBZ_NAME ${CMAKE_SOURCE_DIR}/lib/libz_aarch64.a)
	else()
		message(FATAL_ERROR "Could not detect architecture")
	endif()
endif()



file(GLOB SRC_FILES *.cpp)

# Phase 2: the standalone game. OFF = the old assertion-only sanity build.
option(SPIDEY_STANDALONE "Build the standalone game (platform/ + a backend)" OFF)
set(SPIDEY_BACKEND "null" CACHE STRING "Platform backend: null (headless) or sdl3")

if (SPIDEY_STANDALONE)
	message("Standalone build, backend: ${SPIDEY_BACKEND}")
	add_compile_definitions(SPIDEY_STANDALONE)
	file(GLOB PLAT_FILES platform/*.cpp platform/${SPIDEY_BACKEND}/*.cpp)
	list(APPEND SRC_FILES ${PLAT_FILES})
endif()

add_executable(${PROJECT_NAME} ${SRC_FILES})

if (UNIX OR MINGW)
	target_link_libraries(${PROJECT_NAME} ${LIBZ_NAME})
elseif(WIN32)
	target_link_libraries(${PROJECT_NAME} ${CMAKE_SOURCE_DIR}/lib/safesehzlib.lib winmm.lib dinput8.lib dxguid.lib dsound.lib ddraw.lib)
endif ()

if (SPIDEY_STANDALONE)
	if (UNIX)
		# exemem.cpp maps the exe's data range at 0x53B000, below the
		# 0x08048000 load address of a non-PIE 32-bit binary.
		target_compile_options(${PROJECT_NAME} PRIVATE -fno-pie)
		target_link_options(${PROJECT_NAME} PRIVATE -no-pie)
		# the 1999 code aliases byte arrays on purpose (PSX data, packed
		# structs); glibc's fortified string functions abort on that
		target_compile_options(${PROJECT_NAME} PRIVATE -U_FORTIFY_SOURCE -D_FORTIFY_SOURCE=0)
		# implicit linker script: places the G_*-twinned variables at their
		# exe addresses (see platform/exemem.h)
		target_link_libraries(${PROJECT_NAME} pthread ${CMAKE_SOURCE_DIR}/platform/exemem_syms.ld)
	elseif (MINGW)
		enable_language(RC)
		target_compile_definitions(${PROJECT_NAME} PRIVATE _WIN32_WINNT=0x0601 WINVER=0x0601 NTDDI_VERSION=0x06010000)
		target_sources(${PROJECT_NAME} PRIVATE platform/windows/app.rc)
		# The PE loader reserves game data before loading any DLLs. Our code
		# lives above that data and never loads the original code section.
		# PE sections must be adjacent. The zero-filled reservation starts
		# after the headers and spans the gaps around the game data.
		target_link_options(${PROJECT_NAME} PRIVATE -Wl,--image-base,0x00400000 -Wl,--section-start,.text=0x08001000 -Wl,--section-start,.exemem=0x00401000 -Wl,--disable-dynamicbase -Wl,--disable-reloc-section)
		file(READ "${CMAKE_SOURCE_DIR}/platform/exemem_syms.ld" EXEMEM_SYMBOLS)
		string(REGEX REPLACE "(^|\n)([A-Za-z_][A-Za-z_0-9]*) =" "\\1_\\2 =" EXEMEM_SYMBOLS "${EXEMEM_SYMBOLS}")
		file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/exemem_syms_mingw.ld" "${EXEMEM_SYMBOLS}")
		target_link_libraries(${PROJECT_NAME} pthread winmm dinput8 dxguid dsound ddraw "${CMAKE_CURRENT_BINARY_DIR}/exemem_syms_mingw.ld")
	endif()
	if (SPIDEY_BACKEND STREQUAL "sdl3")
		target_compile_definitions(${PROJECT_NAME} PRIVATE SPIDEY_SETUP_LAUNCHER)
		# Source builds and release packages use the same setup scripts.
		file(GLOB LAUNCHER_SCRIPTS CONFIGURE_DEPENDS "${CMAKE_SOURCE_DIR}/platform/launcher/*.py")
		foreach(LAUNCHER_SCRIPT IN LISTS LAUNCHER_SCRIPTS)
			get_filename_component(LAUNCHER_NAME "${LAUNCHER_SCRIPT}" NAME)
			configure_file("${LAUNCHER_SCRIPT}" "${CMAKE_CURRENT_BINARY_DIR}/launcher/${LAUNCHER_NAME}" COPYONLY)
		endforeach()
		find_package(PkgConfig REQUIRED)
		if (UNIX AND CMAKE_SYSTEM_PROCESSOR STREQUAL "x86_64")
			# 32 bit build on a 64 bit host: ask pkg-config for the i386 libs
			set(ENV{PKG_CONFIG_LIBDIR} "/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig")
		endif()
		pkg_check_modules(SDL3 REQUIRED sdl3)
		target_include_directories(${PROJECT_NAME} PRIVATE ${SDL3_INCLUDE_DIRS})
		target_link_directories(${PROJECT_NAME} PRIVATE ${SDL3_LIBRARY_DIRS})
		if (MINGW)
			target_link_libraries(${PROJECT_NAME} ${SDL3_LIBRARIES} opengl32)
		else()
			target_link_libraries(${PROJECT_NAME} ${SDL3_LIBRARIES} GL)
		endif()
	endif()
endif()
