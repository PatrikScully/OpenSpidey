#include "engine/rendering/render_buffers.h"
#include "engine/rendering/render_buffer_pool.h"
#include <new>
#include "core/game_heap.h"
#include "game/application/main.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/rendering/PCGfx.h"

#include "compatibility/validate.h"

#ifndef SPIDEY_STANDALONE
SDoubleBuffer* pDoubleBuffer;
#else
extern SDoubleBuffer* pDoubleBuffer;
#endif
SDoubleBuffer* pOtherBuffer;
#ifndef SPIDEY_STANDALONE
SDoubleBuffer DoubleBuffer[2];
#else
extern SDoubleBuffer DoubleBuffer[2];
#endif
#ifndef SPIDEY_STANDALONE
u32* pPoly;
#else
extern u32* pPoly;
#endif
#ifndef SPIDEY_STANDALONE
u8* PolyBufferEnd;
#else
extern u8* PolyBufferEnd;
#endif

#ifndef SPIDEY_STANDALONE
u32 Db_SkyColor;
#else
extern u32 Db_SkyColor;
#endif
#ifndef SPIDEY_STANDALONE
u32 Db_SkyColorTarget;
#else
extern u32 Db_SkyColorTarget;
#endif

namespace {
class PersistentRenderAllocator : public spidey::core::MemoryAllocator {
public:
    void* allocate(std::size_t byteCount) override {
        return DCMem_New(byteCount, 1, BIGHEAP, 0, true);
    }
    void release(void* allocation) override { Mem_Delete(allocation); }
};
} // namespace

// Compatibility entry point; ownership lives in RenderBufferPool.
void Db_CreateOTsAndPolyBuffers(void)
{
	PersistentRenderAllocator allocator;
	spidey::rendering::RenderBufferPool pool(G_DOUBLE_BUFFER, allocator);
	if (!pool.allocate()) {
		throw std::bad_alloc();
	}

	Db_FlipClear();

	CalcPolyBufferEnd();
}

// @Ok
// @Matching
 void Db_DefaultScreenOffsets(void)
{
	G_DOUBLE_BUFFER[0].Disp.screen.x = 0;
	G_DOUBLE_BUFFER[1].Disp.screen.x = 0;
	G_DOUBLE_BUFFER[0].Disp.screen.y = 0;
	G_DOUBLE_BUFFER[1].Disp.screen.y = 0;
}

// Compatibility entry point; release is safe after partial initialization.
void Db_DeleteOTsAndPolyBuffers(void)
{
	PersistentRenderAllocator allocator;
	spidey::rendering::RenderBufferPool pool(G_DOUBLE_BUFFER, allocator);
	pool.release();

	G_PPOLY = reinterpret_cast<u32*>(1);
}

// @Ok
// @Matching
 void Db_FlipClear(void)
{
	G_PDOUBLE_BUFFER = G_PDOUBLE_BUFFER == &G_DOUBLE_BUFFER[0] ?
		&G_DOUBLE_BUFFER[1] :
		&G_DOUBLE_BUFFER[0];

	ClearOTagR();
	G_PPOLY = reinterpret_cast<u32*>(reinterpret_cast<u32>(G_PDOUBLE_BUFFER->Polys) & 0x7FFFFFFF);
}

// @Ok
// @Matching
void Db_Init(void)
{
	SetDefDrawEnv();
	SetDefDrawEnv();
	SetDefDispEnv();
	SetDefDispEnv();

	Db_DefaultScreenOffsets();
	G_DOUBLE_BUFFER[0].Draw.isbg = 1;

	setRGB0();
	G_DOUBLE_BUFFER[1].Draw.isbg = 1;
	setRGB0();

	G_DOUBLE_BUFFER[0].OrderingTable = (u32*)0xFFFFFFFF;
	G_DOUBLE_BUFFER[1].OrderingTable = (u32*)0xFFFFFFFF;
	G_DOUBLE_BUFFER[0].Polys = (u8*)0xFFFFFFFF;
	G_DOUBLE_BUFFER[1].Polys = (u8*)0xFFFFFFFF;

	Db_CreateOTsAndPolyBuffers();
	Db_FlipClear();

	PutDispEnv();
	PutDrawEnv();
	DrawOTag();
}

// @Ok
// @Matching
void Db_UpdateSky(void)
{
	if (G_DB_SKY_COLOR == 0x466973)
	{
		G_DB_SKY_COLOR = 0x154070;
	}

	u32 v1 = 0;

	if (G_DB_SKY_COLOR == -1)
	{
		G_DOUBLE_BUFFER[0].Draw.isbg = 0;
		G_DOUBLE_BUFFER[1].Draw.isbg = 0;
	}
	else
	{
		G_DOUBLE_BUFFER[0].Draw.isbg = 1;
		setRGB0();
		G_DOUBLE_BUFFER[1].Draw.isbg = 1;
		setRGB0();

		v1 = G_DB_SKY_COLOR;
	}

	if (v1 != G_PCGFX_SKY_COLOR)
	{
		G_PCGFX_SKY_COLOR = v1;
		G_BFOGGING_RELATED = 1;
	}
}

void validate_SDoubleBuffer(void)
{
	VALIDATE_SIZE(SDoubleBuffer, 0x7C);

	VALIDATE(SDoubleBuffer, Draw.isbg, 0x18);

	VALIDATE(SDoubleBuffer, Disp.screen.x, 0x64);
	VALIDATE(SDoubleBuffer, Disp.screen.y, 0x66);

	VALIDATE(SDoubleBuffer, OrderingTable, 0x70);
	VALIDATE(SDoubleBuffer, Polys, 0x78);
}

void validate_DB_RECT(void)
{
	VALIDATE_SIZE(DB_RECT, 0x8);

	VALIDATE(DB_RECT, x, 0x0);
	VALIDATE(DB_RECT, y, 0x2);
	VALIDATE(DB_RECT, w, 0x4);
	VALIDATE(DB_RECT, h, 0x6);
}

void validate_DR_ENV(void)
{
	VALIDATE_SIZE(DR_ENV, 0x40);

	VALIDATE(DR_ENV, tag, 0x0);

	VALIDATE(DR_ENV, code, 0x4);
}

void validate_DRAWENV(void)
{
	VALIDATE_SIZE(DRAWENV, 0x5C);

	VALIDATE(DRAWENV, clip, 0x0);

	VALIDATE(DRAWENV, ofs, 0x8);
	VALIDATE(DRAWENV, tw, 0xC);
	VALIDATE(DRAWENV, tpage, 0x14);
	VALIDATE(DRAWENV, dtd, 0x16);
	VALIDATE(DRAWENV, dfe, 0x17);
	VALIDATE(DRAWENV, isbg, 0x18);

	VALIDATE(DRAWENV, r0, 0x19);
	VALIDATE(DRAWENV, g0, 0x1A);
	VALIDATE(DRAWENV, b0, 0x1B);

	VALIDATE(DRAWENV, dr_env, 0x1C);
}

void validate_DISPENV(void)
{
	VALIDATE_SIZE(DISPENV, 0x14);

	VALIDATE(DISPENV, disp, 0x0);
	VALIDATE(DISPENV, screen, 0x8);

	VALIDATE(DISPENV, isinter, 0x10);
	VALIDATE(DISPENV, isrgb24, 0x11);
}
