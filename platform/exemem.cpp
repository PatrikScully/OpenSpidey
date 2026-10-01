#include "exemem.h"
#include "launcher.h"
#include "../FontTools.h"
#include "../dcmodel.h"
#include "../dcmemcard.h"
#include <new>

#include <cstdio>
#include <cstring>
#include <cstdlib>

#ifdef _WIN32
#include <windows.h>
#include <direct.h>
#define chdir _chdir
#ifdef __GNUC__
// Reserve the original data addresses in the PE image before DLLs or heaps
// can take them. This is an uninitialized section with no file payload.
extern "C" u8 spideyExeData[EXEMEM_END - EXEMEM_START];
asm(".section .exemem,\"b\"\n.balign 4096\n.globl _spideyExeData\n_spideyExeData:\n.space 0x28d1000\n.text\n");
#endif
#else
#include <sys/mman.h>
#include <unistd.h>
#include <malloc.h>
#endif

static i32 gExeMemSeeded = 0;
static i32 gExeMemMapped = 0;

// Static initializers already read exe addresses (PCGfx.cpp: "i32 x =
// G_GAME_RESOLUTION_X;"), so the block has to exist before main. glibc
// passes argc/argv to constructors; priority 101 runs before the
// compiler-generated ones. Same argument handling as main_standalone.cpp.
// @Bogus
__attribute__((constructor(101)))
#ifdef _WIN32
static void exeMemEarlyInit(void)
{
	int argc = __argc;
	char** argv = __argv;
#else
static void exeMemEarlyInit(int argc, char** argv)
{
	// The PSX heritage masks pointers with 0x7FFFFFFF (db.cpp: G_PPOLY =
	// Polys & 0x7FFFFFFF) and stores 24/31 bit addresses in primitive tags.
	// A Windows exe never sees an address above 2 GB, but glibc serves big
	// allocations from mmap at 0xF6xxxxxx. Keep every malloc on the brk heap
	// (which grows from right after the binary, far below 2 GB).
	mallopt(M_MMAP_MAX, 0);
	mallopt(M_TRIM_THRESHOLD, -1);
#endif
	Launcher_Bootstrap(argc, argv);

	const char* gameDir = argc > 1 ? argv[1] : getenv("SPIDEY_GAME_DIR");
	if (gameDir && chdir(gameDir) != 0)
		perror(gameDir);

	const char* exe = getenv("SPIDEY_EXE");
	if (!ExeMem_Init(exe ? exe : "SpideyPC.exe"))
		exit(1);
}

// Minimal PE section reader. Only needs the three fields per section that
// matter for copying the initialized bytes to their virtual address.
struct SExeSection
{
	u32 virtualAddress;
	u32 virtualSize;
	u32 rawOffset;
	u32 rawSize;
};

static i32 readSections(FILE* f, u32* imageBase, SExeSection* out, i32 maxOut)
{
	u8 dos[0x40];
	if (fread(dos, 1, sizeof(dos), f) != sizeof(dos) || dos[0] != 'M' || dos[1] != 'Z')
		return 0;

	u32 peOff;
	memcpy(&peOff, dos + 0x3C, 4);
	if (fseek(f, peOff, SEEK_SET) != 0)
		return 0;

	u8 pe[0x18];
	if (fread(pe, 1, sizeof(pe), f) != sizeof(pe) || memcmp(pe, "PE\0\0", 4) != 0)
		return 0;

	u16 numSections, optSize;
	memcpy(&numSections, pe + 6, 2);
	memcpy(&optSize, pe + 0x14, 2);

	u8 opt[0x60];
	if (optSize < sizeof(opt) || fread(opt, 1, sizeof(opt), f) != sizeof(opt))
		return 0;
	memcpy(imageBase, opt + 0x1C, 4);

	if (fseek(f, peOff + 0x18 + optSize, SEEK_SET) != 0)
		return 0;

	i32 n = 0;
	for (i32 i = 0; i < numSections && n < maxOut; i++)
	{
		u8 sec[0x28];
		if (fread(sec, 1, sizeof(sec), f) != sizeof(sec))
			return 0;
		memcpy(&out[n].virtualSize, sec + 0x08, 4);
		memcpy(&out[n].virtualAddress, sec + 0x0C, 4);
		memcpy(&out[n].rawSize, sec + 0x10, 4);
		memcpy(&out[n].rawOffset, sec + 0x14, 4);
		n++;
	}
	return n;
}

// @Bogus
i32 ExeMem_Init(const char* exePath)
{
	const u32 size = EXEMEM_END - EXEMEM_START;

	if (gExeMemMapped)
		return 1;
	gExeMemMapped = 1;

#ifdef _WIN32
#ifdef __GNUC__
	void* p = spideyExeData;
	MEMORY_BASIC_INFORMATION info;
	DWORD protection;
	if (p != (void*)EXEMEM_START || !VirtualQuery(p, &info, sizeof(info))
		|| info.Type != MEM_IMAGE || info.AllocationBase != GetModuleHandleW(0)
		|| info.RegionSize < size || !VirtualProtect(p, size, PAGE_READWRITE, &protection))
	{
		fprintf(stderr, "ExeMem: the native PE image did not reserve the game data addresses.\n");
		return 0;
	}
#else
	void* p = VirtualAlloc((void*)EXEMEM_START, size, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
	if (!p)
	{
		printf("ExeMem: VirtualAlloc at %#x failed (%lu)\n", EXEMEM_START, GetLastError());
		return 0;
	}
#endif
#else
	void* p = mmap((void*)EXEMEM_START, size, PROT_READ | PROT_WRITE,
			MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED_NOREPLACE, -1, 0);
	if (p == MAP_FAILED || p != (void*)EXEMEM_START)
	{
		perror("ExeMem: mmap at 0x53B000 failed (is the binary built with -no-pie and -m32?)");
		return 0;
	}
#endif

	if (!exePath)
		return 1;

	FILE* f = fopen(exePath, "rb");
	if (!f)
	{
		printf("ExeMem: cannot open %s, data block stays zero\n", exePath);
		return 1;
	}

	SExeSection secs[8];
	u32 imageBase = 0;
	i32 n = readSections(f, &imageBase, secs, 8);
	if (!n)
	{
		printf("ExeMem: %s is not a PE file\n", exePath);
		fclose(f);
		return 1;
	}

	for (i32 i = 0; i < n; i++)
	{
		u32 va = imageBase + secs[i].virtualAddress;
		u32 end = va + secs[i].rawSize;
		if (va < EXEMEM_START || end > EXEMEM_END || !secs[i].rawSize)
			continue;   // .text, .rsrc, or nothing initialized

		if (fseek(f, secs[i].rawOffset, SEEK_SET) != 0
				|| fread((void*)va, 1, secs[i].rawSize, f) != secs[i].rawSize)
		{
			printf("ExeMem: short read on section at %#x\n", va);
			fclose(f);
			return 1;
		}
		printf("ExeMem: seeded %#x..%#x from %s\n", va, end, exePath);
	}

	fclose(f);
	gExeMemSeeded = 1;

	// The exe's 1677 C++ static initializers (.CRT$XCU table 0x546004..
	// 0x547A34) ran before WinMain and filled the bss part of this block
	// (every CVector/CQuat/matrix global with a constructor, e.g. the float
	// identity matrix at 0x64E518 that M3d_Render gives every unrotated
	// item). None of that code runs here, so replay the stores from the
	// generated table, then the four initializers that call constructors:
	// gSkaterModels[2] (0x5F6698) and gGlobalSkaterModel (0x5F6808) via
	// DCSkaterModel::DCSkaterModel (0x4325F0), gMessFont (0x60D238) via
	// Font::Font (0x4585E0), and gFrontCardExists / gFrontCardExistsThisFrame
	// (0x5FAD98 / 0x5FAE8C) from DCCard_Exists(0) (0x43F990 / 0x43F9A0).
	ExeMem_ApplyStaticInits();
	::new (reinterpret_cast<void*>(0x005F6698)) DCSkaterModel();
	::new (reinterpret_cast<void*>(0x005F6698 + sizeof(DCSkaterModel))) DCSkaterModel();
	::new (reinterpret_cast<void*>(0x005F6808)) DCSkaterModel();
	::new (reinterpret_cast<void*>(0x0060D238)) Font();
	*reinterpret_cast<u8*>(0x005FAD98) = DCCard_Exists(0);
	*reinterpret_cast<u8*>(0x005FAE8C) = DCCard_Exists(0);

	// Repo variables that live in the exe's bss (zero in the exe) but carry a
	// non-zero initializer in the repo. Their definitions are compiled out in
	// the standalone build (see exemem_syms.ld), so the value is set here.
	*reinterpret_cast<u8*>(0x006B2F08) = 1;   // gSpoolLogFailedTextureAccess (spool.cpp)

	return 1;
}

i32 ExeMem_IsSeeded(void)
{
	return gExeMemSeeded;
}
