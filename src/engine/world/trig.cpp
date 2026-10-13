#include "engine/world/trig.h"
#include "compatibility/trigger_runtime_bridge.h"
#include "compatibility/validate.h"
#include "core/game_heap.h"
#include "core/utils.h"
#include "game/player/spidey.h"
#include "game/ui/front.h"
#include "game/actors/baddy.h"
#include "engine/assets/spool.h"
#include "game/effects/exp.h"
#include "compatibility/my_assert.h"
#include "engine/assets/dcfileio.h"
#include "game/cinematics/camera.h"
#include "engine/world/bit.h"
#include "game/cinematics/cinema.h"
#include "engine/assets/reloc.h"
#include "game/ui/mess.h"
#include "engine/rendering/backgrnd.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/m3dinit.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/effects/effects.h"
#include "engine/audio/ps2redbook.h"
#include "engine/world/ob.h"
#include "game/application/init.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/input/PCInput.h"
#include "engine/input/input_devices.h"

// Object classes Trig_CreateObject below can spawn. Only headers, no
// other .cpp in these files is touched by this change.
#include "game/actors/chopper.h"
#include "game/actors/mj.h"
#include "game/actors/hostage.h"
#include "game/actors/cop.h"
#include "game/actors/thug.h"
#include "game/actors/rhino.h"
#include "game/actors/docock.h"
#include "game/actors/superock.h"
#include "game/actors/scorpion.h"
#include "game/actors/mysterio.h"
#include "game/actors/venom.h"
#include "game/actors/carnage.h"
#include "game/actors/jonah.h"
#include "game/actors/lizman.h"
#include "game/actors/blackcat.h"
#include "game/actors/simby.h"
#include "game/objects/turret.h"
#include "game/actors/lizard.h"
#include "game/actors/spclone.h"
#include "game/actors/torch.h"
#include "game/actors/submarin.h"
#include "game/objects/manipob.h"
#include "game/objects/level_one_bomb.h"
#include "game/objects/platform.h"
#include "game/objects/wire.h"
#include "game/objects/switch.h"
#include "game/objects/powerup.h"

#include <cstdarg>
#include <cstdio>
#include <string.h>

#ifndef SPIDEY_STANDALONE
i32 gRunCinemaRelated;
#else
extern i32 gRunCinemaRelated;
#endif
#ifndef SPIDEY_STANDALONE
i32 gLevelStatus;
#else
extern i32 gLevelStatus;
#endif

// @Ok
// Parses the level code ("lXaX_t") stored in the save: level char at
// field_4[1] (offset 0x5), area char at field_4[3] (offset 0x7).
// Returns (level << 8) | area. Demo levels (field_4[0]=='d'/'D') use 0x99.
i32 Trig_GetLevelId(void)
{
	i32 level = G_SAVE_GAME.mLevelName[1];
	if (G_SAVE_GAME.mLevelName[0] == 'd' || G_SAVE_GAME.mLevelName[0] == 'D')
	{
		level = 0x99;
	}
	else
	{
		if ((u32)level >= 0x30 && (u32)level <= 57)
			return ((level - 48) << 8) | ((char)G_SAVE_GAME.mLevelName[3] - 48);
		if ((u32)level >= 0x41 && (u32)level <= 90)
			return ((level - 49) << 8) | ((char)G_SAVE_GAME.mLevelName[3] - 48);
		if ((u32)level >= 97 && (u32)level <= 122)
			return ((level - 81) << 8) | ((char)G_SAVE_GAME.mLevelName[3] - 48);
	}
	return (level << 8) | ((char)G_SAVE_GAME.mLevelName[3] - 48);
}

#ifndef SPIDEY_STANDALONE
EXPORT u16* TrigFile;
#else
extern u16* TrigFile;
#endif
//#define G_TRIGFILE (TrigFile)
#define G_TRIGFILE (*reinterpret_cast<u16**>(0x006B4668))

#ifndef SPIDEY_STANDALONE
EXPORT i32 NumCheatRestarts;
#else
extern i32 NumCheatRestarts;
#endif
//#define G_NUMCHEATRESTARTS (NumCheatRestarts)
#define G_NUMCHEATRESTARTS (*reinterpret_cast<i32*>(0x006B4664))

// @Ok
#ifndef SPIDEY_STANDALONE
i16 **OffsetList;
#else
extern i16 ** OffsetList;
#endif


// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 NumNodes;
#else
extern i32 NumNodes;
#endif

// @Ok
const i32 MAXPENDING = 16;

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT PendingListEntry PendingListArray[MAXPENDING];
#else
extern PendingListEntry PendingListArray[MAXPENDING];
#endif
//#define G_PENDINGLISTARRAY (PendingListArray)
#define G_PENDINGLISTARRAY (reinterpret_cast<PendingListEntry*>(0x006B4688))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SCommandPoint* CommandPoints;
#else
extern SCommandPoint* CommandPoints;
#endif
//#define G_COMMANDPOINTS (CommandPoints)
#define G_COMMANDPOINTS (*reinterpret_cast<SCommandPoint**>(0x006B4708))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SCommandPoint* HashTable[256];
#else
extern SCommandPoint* HashTable[256];
#endif
//#define G_HASHTABLE (HashTable)
#define G_HASHTABLE (reinterpret_cast<SCommandPoint**>(0x006B4214))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 RestartNode = 0xFFFF;
#else
extern i32 RestartNode;
#endif
// G_RESTARTNODE moved to trig.h (needed by front.cpp too now, one
// definition in the owning header per the G_* placement rule).

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 IsRestartDeath;
#else
extern i32 IsRestartDeath;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 EndLevelNode;
#else
extern i32 EndLevelNode;
#endif
extern CSpecialDisplay *SpecialDisplayList;

extern i32 JoelJewCheatCode;

extern CBody* ControlBaddyList;
extern CBaddy* BaddyList;
extern CBody* PowerUpList;


#ifndef SPIDEY_STANDALONE
EXPORT char *MenuFileNamePointers[40];
#else
extern char * MenuFileNamePointers[40];
#endif

//#define G_MENUFILENAMEPOINTERS (MenuFileNamePointers)
#define G_MENUFILENAMEPOINTERS (reinterpret_cast<char**>(0x006B3844))

#ifndef SPIDEY_STANDALONE
EXPORT i32 NumTrigMenuEntries;
#else
extern i32 NumTrigMenuEntries;
#endif
//#define G_NUMTRIGMENUENTRIES (NumTrigMenuEntries)
#define G_NUMTRIGMENUENTRIES (*reinterpret_cast<i32*>(0x006B467C))

// #define ENABLE_TRIG_LOG

// @Bogus
void trigLog(const char* fmt, ...)
{
#ifdef ENABLE_TRIG_LOG
	static char buffer[512];

	va_list lst;
    va_start(lst, fmt);

#ifdef _WIN32
	_vsnprintf(buffer, sizeof(buffer), fmt, lst);
#else
	vsnprintf(buffer, sizeof(buffer), fmt, lst);
#endif

	va_end(lst);
	printf("trigLog! %s\n", buffer);

#endif
}

// @Ok
// Verified against original disasm at 0x4E3190 (IDA sub_4E3190, string
// "Bad node sent to SendKillFromNode" pins the address). Two bugs found
// and fixed versus the previous draft: (1) case 2/9 must call
// Spool_FindEnviroItem with the checksum VALUE at the computed pointer
// (*(u32*)v20), not the pointer itself; the original does
// sub_4C9230(*(DWORD*)v20). (2) the PowerUpList case is node type 5 or
// 20, not 4 or 20 (original switch has case 5: case 0x14:). Trig_GetLinksPointer
// is inlined into this function in the original (its own print_if_false
// string appears back to back with this function's), consistent with it
// being INLINE in this TU; kept as a real call here since it stays
// functionally identical either way.
void SendKillFromNode(i32 Node, i32 How)
{
	print_if_false(Node >= 0 && Node < NumNodes, "Bad node sent to SendKillFromNode");

	u16 *pLinkInfo = Trig_GetLinksPointer(Node);

	u16 NumLinks = *pLinkInfo;
	u16* nodeIndexPtr = pLinkInfo + 1;

	for (i32 i = 0; i < NumLinks; i++)
	{
		u16 nodeIndex = nodeIndexPtr[i];

		i16 *node = reinterpret_cast<i16*>(G_OFFSETLIST[nodeIndex]);
		switch (*node)
		{
			case 1:
				if (node[1] == 409)
				{
					for (
							CSpecialDisplay *cur = SpecialDisplayList;
							cur;
							cur = reinterpret_cast<CSpecialDisplay*>(cur->mNext))
					{
						if (cur->mType == 9)
						{
							if (*reinterpret_cast<i16*>(reinterpret_cast<u8*>(cur)+0x6A) == nodeIndex)
							{
								cur->Die();
							}
						}
					}
				}
				else
				{
					KillInList(nodeIndex, BaddyList, How);
					KillInList(nodeIndex, ControlBaddyList, How);
					KillInList(nodeIndex, G_ENVIRONMENTAL_OBJECT_LIST, How);
				}
				break;
			case 2:
			case 9:

				u32 v20;
				CItem* EnviroItem;

				v20 = reinterpret_cast<u32>(&node[node[1] + 2]);
				if (v20 & 2)
					v20 += 2;

				EnviroItem = Spool_FindEnviroItem(*reinterpret_cast<u32*>(v20));
				if (EnviroItem)
				{
					if (How == 1)
					{
						Exp_HitEnvItem(EnviroItem, 0, 0xFFFF);
					}
					else
					{
						EnviroItem->mFlags |= 1;
					}
				}
				break;
			case 5:
			case 20:
				KillInList(nodeIndex, PowerUpList, How);
				break;
			default:
				break;
		}
	}


}

// @Ok
void SendSuspendOrActivate(u16* pLinkInfo, i32 signalType)
{
	switch(signalType)
	{
		case 4:
		case 5:
			break;
		default:
			print_if_false(0, "Bad signalType");
			break;
	}

	print_if_false(*pLinkInfo !=0, "Node sending an activate or \n suspen is not lined\n to anything");

	u16 numIters = *pLinkInfo;

	u16* nodeIndexPtr = pLinkInfo + 1;

	for (i32 i = 0; i < numIters; i++)
	{
		u16 *node = reinterpret_cast<u16*>(G_OFFSETLIST[nodeIndexPtr[i]]);

		switch(*node)
		{
			case 1:
			case 7:
				if (signalType == 5)
				{
					SendSuspend(reinterpret_cast<CBody**>(&BaddyList), nodeIndexPtr[i]);
					SendSuspend(reinterpret_cast<CBody**>(&ControlBaddyList), nodeIndexPtr[i]);
					SendSuspend(reinterpret_cast<CBody**>(&G_ENVIRONMENTAL_OBJECT_LIST), nodeIndexPtr[i]);
				}
				else
				{
					SendUnSuspend(BaddyList, nodeIndexPtr[i]);
					SendUnSuspend(ControlBaddyList, nodeIndexPtr[i]);
					SendUnSuspend(G_ENVIRONMENTAL_OBJECT_LIST, nodeIndexPtr[i]);
				}
				break;
		}
	}
}

// @Ok
void SendUnSuspend(CBody* pList, i32 NodeIndex)
{
	for (CBody* cur = pList; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mNode == NodeIndex)
			cur->UnSuspend();
	}
}

// @Ok
// @Matching
void SendSignalToNode(CBody* pBody, i32 NodeIndex)
{
	for (CBody* cur = pBody; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mNode == NodeIndex)
			cur->mInputFlags |= 1;
	}
}

// @Ok
 void SendSuspend(CBody** ppList, i32 NodeIndex)
{
	for (CBody* cur = *ppList; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mNode == NodeIndex)
			cur->Suspend(ppList);
	}
}

// @Ok
void KillInList(i32 Node, CBody* pList, i32 How)
{
	for (CBody *cur = pList; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mNode == Node)
		{
			switch (How)
			{
				case 0:
					cur->Die();
					break;
				case 1:
					SHitInfo hitInfo;
					hitInfo.field_8 = cur->mHealth;
					hitInfo.field_C.vx = 0;
					hitInfo.field_C.vy = 0;
					hitInfo.field_C.vz = 0;
					hitInfo.field_0 = 4;
					cur->Hit(&hitInfo);
					break;
			}
		}
	}

}

// dword_60D9D0: read-only BSS constant referenced by ~50 unrelated
// functions across the binary as a CVector* input (the powerup
// constructor's "velocity" argument here). Never observed written
// anywhere; our own guess (not from an IDB) is that it is a shared,
// permanently-zero vector constant. Flagged as a guess, not a fact.
static CVector* const gZeroVectorConst = reinterpret_cast<CVector*>(0x0060D9D0);

// Guards a spawn-in-progress window around CLaserFence/CTripWire
// construction from a trigger node (see the case 404/405/408 block in
// Trig_CreateObject below): set to 0 right before the constructor call,
// back to 1 right after. No idb_globals.txt entry nearby; tentative
// name, evidence is only this exact toggle-around-ctor shape at 0x4DF7A2
// etc.
static i32 gWireBeingCreated = 1;

// Mirrors sub_46BD80, the powerup/light "resolve difficulty-remapped
// type, then allocate+construct" helper Trig_CreateObject calls for
// trigger meta-types 5/20. names.json's guess "PowerUp_Create" for this
// address is really this wrapper, not the constructor itself (that is
// the already-decompiled CPowerUp::CPowerUp in powerup.cpp). The
// DifficultyLevel-based type substitution table below was read directly
// off the disasm at 0x46BD9E (not guessed): on DifficultyLevel==3, type
// 14 is dropped entirely (no powerup spawns), 15 downgrades to 14, 16
// downgrades to 15; on DifficultyLevel<=1, type 14 upgrades to 15;
// DifficultyLevel==2 never remaps.
// @Ok
static CBody* Trig_CreatePowerUp(i32 type, CVector* pos, i32 flags, i32 param1, i32 param2)
{
	i32 remappedType = type;

	if (DifficultyLevel == 3)
	{
		if (type == 14)
			return 0;
		if (type == 15)
			remappedType = 14;
		else if (type == 16)
			remappedType = 15;
	}
	else if (DifficultyLevel <= 1)
	{
		if (type == 14)
			remappedType = 15;
	}

	return new CPowerUp(static_cast<u16>(remappedType), pos, gZeroVectorConst, flags, param1, param2);
}

#ifdef SPIDEY_STANDALONE
// @Bogus
// Standalone-only external name for Trig_CreatePowerUp above (0x46BD80,
// PowerUp_Create in names.json), see powerup.h.
CBody* PowerUp_Create(i32 type, CVector* pos, i32 flags, i32 param1, i32 param2)
{
	return Trig_CreatePowerUp(type, pos, flags, param1, param2);
}
#endif

// A handful of the classes Trig_CreateObject below can spawn (CThug,
// CCop, CHostage) each add ONE new virtual beyond CBaddy's 17 declared
// vtable slots (0..16) purely so this function can stamp a
// subtype/variant code onto the freshly built object (the original does
// a raw "call [vtable+0x44]" with that code as the one argument, e.g.
// CThug's is named SetThugType in thug.h, but CCop/CHostage have no
// equivalent declared anywhere and adding one means editing
// cop.h/hostage.h, out of scope for this change). This struct reproduces
// the exact "read vtable, call slot 17 with one i32 arg" shape without
// touching those headers, the same way spidey.cpp's
// SVTableSlot0Deletable reproduces a slot-0 call -- __thiscall itself is
// rejected by this build (error C4234), so a same-shaped virtual class
// is the only portable way to get the compiler to emit a thiscall here.
// Slot 0 of every CItem-derived class is the virtual destructor, and this
// fake class must declare one too, not a plain dummy virtual: MSVC gives
// a virtual destructor ONE vtable entry (the scalar deleting destructor),
// GCC's Itanium ABI gives it TWO (complete + deleting), so only a real
// destructor here keeps SetSubType on the same slot as the real classes
// under both compilers. With a dummy virtual instead, the standalone
// (GCC) build called slot 17 = CBaddy::GetVariable, the thug never got
// its type/region and RenderSuperItem crashed on region 0's NULL
// pAnimFile (found 2026-09-03).
struct SVTableSlot17Setter
{
	virtual ~SVTableSlot17Setter() {}
	virtual void Unused01() {}
	virtual void Unused02() {}
	virtual void Unused03() {}
	virtual void Unused04() {}
	virtual void Unused05() {}
	virtual void Unused06() {}
	virtual void Unused07() {}
	virtual void Unused08() {}
	virtual void Unused09() {}
	virtual void Unused10() {}
	virtual void Unused11() {}
	virtual void Unused12() {}
	virtual void Unused13() {}
	virtual void Unused14() {}
	virtual void Unused15() {}
	virtual void Unused16() {}
	virtual void SetSubType(i32) {}
};

// @Ok
static void Trig_SetCreatedObjectSubType(CBody* obj, i32 subType)
{
	reinterpret_cast<SVTableSlot17Setter*>(obj)->SetSubType(subType);
}

// @Ok
// Rewritten 2026-08-31 from a fresh IDA decompile + disassembly
// cross-check of 0x4DEE70 (3504 bytes, the trigger-node object factory).
// Session bar is functional parity, not byte match. Every field offset
// and byte-scan below was verified against the raw disasm, not just
// Hex-Rays: the marker-byte walk at 0x4DEEF2..0x4DEFD2 and the
// post-creation mNode/mCBodyFlags/Suspend-list epilogue at
// 0x4DF120..0x4DF19B were both read instruction-for-instruction. The
// per-node "marker byte" list (values 1/2/4, terminated by 0xFF, right
// after an embedded name string) is the same walk-until-terminator idiom
// documented in CLAUDE.md for other trigger/PSX record formats; here it
// gates two booleans (hasMarker2 disables the Suspend-list registration
// below, hasMarker4 clears CBODY flag 2) and locates where the spawned
// object's own embedded command list starts (pData). Debug-only
// print/log calls the original makes at nearly every step (nullsub_1 in
// this build, same as trigLog elsewhere in this file) are omitted, they
// have no observable effect.
//
// Every "normal" case below calls the game's own already-decompiled
// Xxx_CreateYyy(const u32*, u32*) factory wrapper (or, for the few
// classes with no such wrapper -- CManipOb, CPlatform, CLaserFence,
// CTripWire, CSwitch, CL1A3Bomb -- their public (i16*, i32)-style
// constructor directly; MSVC6 wraps that in the same operator-new + SEH
// cleanup-frame shape as the original since operator new is inherited
// from CBody/CItem, see CLAUDE.md's SEH-frame note).
//
// Two node subtypes are intentionally left calling into the ORIGINAL
// game code for exactly that one subtype, not guessed:
//   - subtype 203 (CScriptOnlyBaddy, meta-type 1/7 branch): its
//     constructor at 0x4075B0 is a real ~0xF0-byte body (base CBaddy
//     ctor, vtable swap, then an inlined copy of CBaddy::ParseScript)
//     and CScriptOnlyBaddy has no public constructor declared anywhere
//     in this repo; adding one means editing baddy.h, out of scope here
//     (trig.cpp/trig.h only).
//   - subtype 409 ("electro lines" chain, sub_439CC0): builds a chain of
//     unnamed ~108-byte link objects across the node's linked list using
//     three texture handles ("Bolt"/"Bolt2"/"Bolt3") and a helper
//     (sub_4398B0) with no repo class, struct or declared signature at
//     all -- a whole new subsystem, not a one-function fix.
CBody* Trig_CreateObject(i32 NodeIndex)
{
	print_if_false(NodeIndex >= 0 && NodeIndex < G_NUMNODES, "Bad node sent to Trig_CreateObject");

	u16* v1 = reinterpret_cast<u16*>(G_OFFSETLIST[NodeIndex]);
	i16 metaType = static_cast<i16>(*v1);
	u16* v3 = v1 + 1;

	CBody* result = 0;
	i32 hasMarker2 = 0; // if set, skip the Suspend-list registration below
	i32 hasMarker4 = 0; // if set, clear CBODY flag 2 below

	if (metaType == 5 || metaType == 20)
	{
		// Powerup / light node: {type:u16} right at v3, then
		// {extraFlag:u16, param1:u16, param2:u16 (metaType 20 only)}
		// packed right after whatever Trig_GetPosition itself consumes.
		i32 type = static_cast<i16>(*v3);

		CVector pos;
		u8* posEnd = reinterpret_cast<u8*>(Trig_GetPosition(&pos, NodeIndex));

		i16 extraFlag = *reinterpret_cast<i16*>(posEnd + 2);
		u16* pExtra = reinterpret_cast<u16*>(posEnd + 4);

		i32 flags = (extraFlag != 0) ? 0 : 4;
		i32 param1 = *pExtra;
		i32 param2 = (metaType == 20) ? pExtra[1] : -1;

		result = Trig_CreatePowerUp(type, &pos, flags, param1, param2);

		if (result != 0)
			reinterpret_cast<CPowerUp*>(result)->SetNode(NodeIndex);

		hasMarker2 = 1;
	}
	else
	{
		switch (metaType)
		{
			case 1:
			case 7:
				break;
			default:
				return 0;
		}

		i32 objType = static_cast<i16>(v3[0]);
		i32 count = static_cast<i16>(v3[2]);
		u16* v6 = v3 + 2;

		u8* markerStart = reinterpret_cast<u8*>(v6) + count * 2 + 2;

		{
			u8* scan = markerStart;
			while (*scan != 0xFF)
			{
				if (*scan == 2)
				{
					hasMarker2 = 1;
					break;
				}
				scan++;
			}
		}
		{
			u8* scan = markerStart;
			while (*scan != 0xFF)
			{
				if (*scan == 4)
				{
					hasMarker4 = 1;
					break;
				}
				scan++;
			}
		}

		u8* terminator = markerStart;
		while (*terminator != 0xFF)
			terminator++;

		i16* pData = reinterpret_cast<i16*>((reinterpret_cast<u32>(terminator) + 4) & ~3u);

		u32 stack2[2];
		stack2[0] = reinterpret_cast<u32>(pData);
		stack2[1] = static_cast<u32>(NodeIndex);

		u32 stack1[1];
		stack1[0] = static_cast<u32>(NodeIndex);

		u32 outPtr;

		switch (objType)
		{
			case 322:
				Chopper_CreateSearchlight(stack1, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 203:
				// 0x4DF005: new(0x330) + CScriptOnlyBaddy::CScriptOnlyBaddy(pData,
				// NodeIndex). (The old forward jumped to 0x4DEE70, which is this
				// very function.)
				result = new CScriptOnlyBaddy(pData, NodeIndex);
				break;

			case 303:
				MJ_CreateMJ(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 304:
				Thug_CreateThug(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				if (result)
					Trig_SetCreatedObjectSubType(result, 304);
				break;

			case 305:
			case 315:
				Hostage_CreateHostage(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				if (result)
					Trig_SetCreatedObjectSubType(result, objType);
				break;

			case 306:
				Cop_CreateCop(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				if (result)
					Trig_SetCreatedObjectSubType(result, 306);
				break;

			case 307:
				Rhino_CreateRhino(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 308:
				DocOck_CreateDocOck(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 309:
				SuperDocOck_CreateSuperDocOck(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 310:
				Scorpion_CreateScorpion(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 311:
				Mysterio_CreateMysterio(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 312:
				Thug_CreateThug(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				if (result)
					Trig_SetCreatedObjectSubType(result, 312);
				break;

			case 313:
				Venom_CreateVenom(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 314:
				Carnage_CreateCarnage(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 316:
				Jonah_CreateJonah(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 317:
				LizMan_CreateLizMan(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 318:
				Chopper_CreateChopper(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 319:
				BlackCat_CreateBlackCat(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 320:
				Cop_CreateCop(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				if (result)
					Trig_SetCreatedObjectSubType(result, 320);
				break;

			case 323:
				Chopper_CreateSniper(stack1, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 324:
				Simby_CreateSimby(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 325:
				Turret_CreateTurret(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 326:
				Lizard_CreateLizard(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 327:
				SpClone_CreateSpClone(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 328:
				Torch_CreateTorch(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 401:
				result = new CManipOb(pData, NodeIndex);
				break;

			case 402:
				result = new CPlatform(pData, NodeIndex);
				break;

			case 404:
				gWireBeingCreated = 0;
				result = new CLaserFence(pData, NodeIndex, true);
				gWireBeingCreated = 1;
				break;

			case 405:
				gWireBeingCreated = 0;
				result = new CTripWire(pData, static_cast<u16>(NodeIndex));
				gWireBeingCreated = 1;
				break;

			case 407:
				result = new CSwitch(pData, NodeIndex);
				break;

			case 408:
				gWireBeingCreated = 0;
				result = new CLaserFence(pData, NodeIndex, false);
				gWireBeingCreated = 1;
				break;

			case 409:
				Effects_CreateElectroLines(NodeIndex);
				return 0;

			case 411:
				Simby_CreateSimbyDroplet(stack2, &outPtr);
				return 0;

			case 412:
				Simby_CreatePunchOb(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			case 600:
				result = new CL1A3Bomb(pData, NodeIndex);
				break;

			case 719:
				Submariner_CreateSubmariner(stack2, &outPtr);
				result = reinterpret_cast<CBody*>(outPtr);
				break;

			default:
				return 0;
		}
	}

	if (result == 0)
		return 0;

	result->mNode = static_cast<u16>(NodeIndex);

	if (hasMarker4)
		result->mCBodyFlags &= 0xFFFD;

	if (!hasMarker2)
	{
		CBody** listHead = 0;
		if (result == BaddyList) listHead = reinterpret_cast<CBody**>(&BaddyList);
		if (result == ControlBaddyList) listHead = &ControlBaddyList;
		if (result == G_ENVIRONMENTAL_OBJECT_LIST) listHead = &G_ENVIRONMENTAL_OBJECT_LIST;
		if (result == PowerUpList) listHead = &PowerUpList;

		print_if_false(listHead != 0, "NewObject not in baddy,env obj or powerup list");

		result->Suspend(listHead);
	}

	return result;
}

// @Ok
void Trig_ExecuteAutoexec(void)
{
	print_if_false(G_TRIGFILE != 0, "No trigger file");
	EndLevelNode = 0xFFFF;

	if (JoelJewCheatCode)
	{
		for (i32 curNode = 0; curNode < NumNodes; curNode++)
		{
			u16 *v5 = reinterpret_cast<u16*>(G_OFFSETLIST[curNode]);
			if (*v5 == 15)
			{
				trigLog("*** Executing AUTOEXEC2 Node %i ***", curNode);
				ExecuteCommandList(v5 + 1, curNode, 1);
				return;
			}
		}
	}

	for (i32 curNode = 0; curNode < NumNodes; curNode++)
	{
		u16 *v5 = reinterpret_cast<u16*>(G_OFFSETLIST[curNode]);
		if (*v5 == 4)
		{
			trigLog("*** Executing AUTOEXEC Node %i ***", curNode);
			ExecuteCommandList(v5 + 1, curNode, 1);
		}
	}
}

// @Ok
// @AlmostMatching: different reg alloc
void Trig_ExecuteRestart(void)
{
	ASSERT(G_RESTARTNODE != 0xFFFF, "Tried to execute a restart with no restart node set");
	ASSERT(*G_OFFSETLIST[G_RESTARTNODE] == 8, "Eh? Restart node isn't a restart node!");
	ASSERT(G_MECHLIST != 0, "Tried to execute a restart with a NULL G_MECHLIST_PLAYER");

	CVector v7;

	CSVector *Position = reinterpret_cast<CSVector*>(Trig_GetPosition(&v7, G_RESTARTNODE));

	CPlayer *pPlayer = (CPlayer*)G_MECHLIST;

	pPlayer->mPos = v7;
	pPlayer->SetStartOrientation(Position);

	char *v3 = reinterpret_cast<char*>(&Position[1]);
	trigLog("*** Executing Restart Node: %s ***", v3);

	u16* v4 = SkipString(v3);

	Trig_ZeroPendingList();
	ExecuteCommandList(v4, G_RESTARTNODE, 1);
}

// Adjacent to gKillNotifyCallCount (spidey.cpp, CPlayer::NotifyKill); both
// get reset here when a new trigger file loads. No idb_globals.txt entry
// for this one, tentative name only, not confirmed what it tracks.
static i32 * const gKillNotifyRelated = (i32*)0x0060CFB8;
static i32 * const gKillNotifyCallCount = (i32*)0x0060CFBC;

// Set to 1 when the lowres/%s.trg variant of the trigger file was found
// and used, instead of the plain %s.trg. No idb_globals.txt entry nearby.
static u8 * const gTrigLoadedLowRes = (u8*)0x006B4680;

// Restart point name table, indexed by G_NUMCHEATRESTARTS (checked
// against 20 below). No idb_globals.txt entry nearby, tentative name.
static char ** const gCheatRestartNames = reinterpret_cast<char**>(0x006B4614);

//#define G_LOWGRAPHICS (gLowGraphics)
#define G_LOWGRAPHICS (*reinterpret_cast<i32*>(0x006B78F8))

// Mac symbol not confirmed, address 0x4E35C0. Called from Trig_LoadTRG
// after the node offset table is relocated. Not decompiled here; forward
// @Ok
void Trig_ParseTRGFile(void)
{
	print_if_false(G_TRIGFILE != 0, "No trigger file to parse");

	for (i32 i = 0; i < G_NUMNODES; i++)
	{
		// 0x4E35F1: "mov ecx,[6B466Ch]; mov eax,[ecx+edi*4]", the node table
		// pointer (G_OFFSETLIST), not a table at 0x6B466C
		u16 *v2 = reinterpret_cast<u16*>(G_OFFSETLIST[i]);
		i32 v3 = *v2;
		if (v3 > 20)
		{
			if (v3 == 500)
				print_if_false(0, "No more lights");
		}
		else if (v3 == 20 || v3 == 5)
		{
			CVector pos;
			memset(&pos, 0, sizeof(pos));
			Trig_GetPosition(&pos, i);
		}
		else
		{
			switch (v3)
			{
			case 1:
			{
				u8 *v4 = (u8*)&v2[v2[3] + 4];
				u8 v5 = *v4;
				while (v5 != 1 && v5 != 0xFF)
				{
					v4++;
					v5 = *v4;
				}
				if (v5 == 1)
					Trig_CreateObject(i);
				break;
			}
			case 2:
			case 9:
			{
				i32 v9 = (i32)&v2[v2[1] + 2];
				if (v9 & 2)
					v9 += 2;
				u32 v10 = *(u32*)v9;
				CreateCommandPoint(v10, static_cast<u16>(i), reinterpret_cast<u16*>(0x005580C0));
				break;
			}
			case 6:
			{
				u32 *v6 = (u32*)&v2[v2[1] + 2];
				u32 *v16 = v6;
				if ((u8)(u32)v6 & 2)
				{
					v6 = (u32*)((char*)v6 + 2);
					v16 = v6;
				}
				u32 v7 = *v6;
				CreateCommandPoint(v7, static_cast<u16>(i), reinterpret_cast<u16*>(v16 + 1));
				break;
			}
			default:
				break;
			}
		}
	}

	G_NUMTRIGMENUENTRIES = 0;
	memset(G_MENUFILENAMEPOINTERS, 0, sizeof(char*) * 40);

	for (i32 j = 0; j < G_NUMNODES; j++)
	{
		if (*reinterpret_cast<u16*>(G_OFFSETLIST[j]) == 8)
		{
			CVector pos;
			memset(&pos, 0, sizeof(pos));
			char *v14 = (char*)Trig_GetPosition(&pos, j) + 6;
			print_if_false(G_NUMTRIGMENUENTRIES < 40, "Too many restart points");
			G_MENUFILENAMEPOINTERS[G_NUMTRIGMENUENTRIES] = v14;
			G_NUMTRIGMENUENTRIES++;
		}
	}
}

// Mac symbol not confirmed, address 0x4DEB50. Called from Front_LoadGame
// (front.cpp) with the restart-point TRG name; loads the TRG file for
// that name and parses it into TrigFile/NumNodes/OffsetList.
// @Ok
// Functionally correct and verified against the original logic (see
// residue note below from earlier byte-matching work; this session's
// bar is functional parity, not zero-diff, so kept as @Ok).
// residue: 125 mnemonic diffs, all downstream of one root cause. Same
// instruction count as the original (181) and the same operations in the
// same order, so this is register scheduling, not a missing/extra store.
// The original reloads the pName parameter from the stack fresh at each
// of its 3 uses (sprintf call sites for "lowres\%s.trg" / "%s.trg"),
// never keeping it live in a register across the G_LOWGRAPHICS branch.
// Our build always hoists a single load of pName before the branch and
// shares it (a shared push before the je, then reused by whichever side
// runs), which is functionally fine but shifts the whole rest of the
// function's register allocation. Confirmed the epilogue (last 6
// instructions) matches exactly, so this really is one localized cause.
// 8 source hypotheses tried, all rebuilt and diffed against the DLL: (1)
// natural nested if/else, no goto: 119 diffs, 183 instr (2 extra). (2)
// caching G_LOWGRAPHICS into a named local read before the resets: 126
// diffs, register allocation shifted from the very first instruction. (3)
// goto to merge the two FileIO_Open("%s.trg", pName) call sites into one
// (matches the original's own tail-merge): fixed the instruction count to
// exactly 181, diffs down to 125. (4) swapping declaration order of
// buf/fileSize vs the resets: no change (125). (5) "if (!fileSize)" as an
// independent check instead of goto/nesting: worse (139, 183 instr). (6)
// flat "if (!G_LOWGRAPHICS) goto plainOpen" early-exit shape instead of
// if/else: worse (139). (7) forcing a fresh volatile reload of pName at
// each of the 3 use sites (defeats CSE): diffs dropped to 118, but
// instruction count grew to 183, i.e it manufactures extra loads instead
// of reproducing the original's shape, so rejected per the "verify byte
// length" rule in CLAUDE.md. (8) duplicating the 3 global resets inside
// each branch instead of once before the if (letting the optimizer choose
// where to hoist them): no change (125), confirms the compiler already
// hoists them to the same place either way. Best kept version is (3).
// See trig.attempts.md for the same list with per-attempt cmpsum output.
void Trig_LoadTRG(char *pName)
{
	print_if_false(G_TRIGFILE == 0, "Old Trig file not deleted?");
	print_if_false(G_PENDINGLISTARRAY[0].pCommands == 0, "Pending list not empty?");

	char buf[32];
	i32 fileSize;

	*gKillNotifyRelated = 0;
	*gKillNotifyCallCount = 0;
	*gTrigLoadedLowRes = 0;

	if (G_LOWGRAPHICS)
	{
		sprintf(buf, "lowres\\%s.trg", pName);
		fileSize = FileIO_Open(buf);

		if (fileSize)
		{
			print_if_false((i32)"Loaded lowres trigger file: %s\r\n", buf);
			*gTrigLoadedLowRes = 1;
			goto haveFile;
		}

		sprintf(buf, "%s.trg", pName);
		goto openFile;
	}

	sprintf(buf, "%s.trg", pName);
openFile:
	fileSize = FileIO_Open(buf);
haveFile:
	;

	G_TRIGFILE = static_cast<u16*>(DCMem_New(fileSize, 0, 1, 0, 1));
	FileIO_Load(G_TRIGFILE);
	FileIO_Sync();

	u8 *pFile = reinterpret_cast<u8*>(G_TRIGFILE);

	print_if_false(*reinterpret_cast<u32*>(pFile) == 0x4752545F, "Not a _TRG file");
	print_if_false((*reinterpret_cast<u32*>(pFile + 4) & 0xFFFF) == 2, "Wrong trigger file version.");
	print_if_false((*reinterpret_cast<u32*>(pFile + 4) & 0xFFFF0000) == 0x10000, "Not a Spidey trigger file");

	G_NUMNODES = *reinterpret_cast<u16*>(pFile + 8);

	print_if_false((i32)"Loading Trigger File: %s [%i Nodes]", pName, G_NUMNODES);

	u32 *pOffsets = reinterpret_cast<u32*>(pFile + 0xC);
	G_OFFSETLIST = reinterpret_cast<i16**>(pOffsets);

	i32 i;

	for (i = 0; i < G_NUMNODES; i++)
	{
		pOffsets[i] += reinterpret_cast<u32>(G_TRIGFILE);
	}

	u8 *pLast = reinterpret_cast<u8*>(G_OFFSETLIST[G_NUMNODES - 1]);

	if (*reinterpret_cast<u16*>(pLast) == 0xFF)
	{
		Mem_Shrink(G_TRIGFILE, (pLast - reinterpret_cast<u8*>(G_TRIGFILE) + 5) & ~3);
	}
	else
	{
		print_if_false(0, "Trig file is missing a terminator node");
	}

	Spool_ClearAllPSXs();

	Spidey_LoadAlternativeHealthIcon((G_SAVE_GAME.field_7C & 0xFF) + 1);

	G_NUMCHEATRESTARTS = 0;

	for (i = 0; i < G_NUMNODES; i++)
	{
		if (*G_OFFSETLIST[i] == 8)
		{
			CVector v;
			CSVector *pPos = reinterpret_cast<CSVector*>(Trig_GetPosition(&v, i));
			char *pRestartName = reinterpret_cast<char*>(&pPos[1]);

			print_if_false(G_NUMCHEATRESTARTS < 20, "Too many restart points for restarts menu");

			gCheatRestartNames[G_NUMCHEATRESTARTS] = pRestartName;
			G_NUMCHEATRESTARTS++;
		}
	}

	G_RESTARTNODE = 0xFFFF;

	Trig_ExecuteAutoexec();
	Trig_ParseTRGFile();
}

// @Ok
// @Matching
void Trig_SetRestart(char *pName)
{
	G_RESTARTNODE = 0xFFFF;
	for (i32 curNode = 0; curNode < G_NUMNODES; curNode++)
	{
		if (*G_OFFSETLIST[curNode] == 8)
		{
			CVector v3;

			u16* Position = Trig_GetPosition(&v3, curNode);
			
			if (Utils_CompareStrings(reinterpret_cast<char*>(&Position[3]), pName))
			{
				G_RESTARTNODE = curNode;
				trigLog("Set RestartNode = %i", curNode);
				if (!Utils_CompareStrings(pName, "re_start_death"))
					G_ISRESTARTDEATH = 1;
				return;
			}
		}
	}

	ASSERT(0, "Restart point ");
}

// @Ok
 u16 *SkipString(char *pText)
{
	while(*pText)
		pText++;

	pText++;

	u32 res = reinterpret_cast<u32>(pText);

	return reinterpret_cast<u16*>(res + (res & 1));
}

// @Ok
// @Matching
void Trig_DeleteTrigFile(void)
{
	if (G_TRIGFILE)
	{
		Mem_Delete(reinterpret_cast<void*>(G_TRIGFILE));
		G_TRIGFILE = 0;
	}

	G_NUMCHEATRESTARTS = 0;
	Trig_ZeroPendingList();
}

// ---------------------------------------------------------------------------
// ExecuteCommandList support: globals, tables and helpers
// ---------------------------------------------------------------------------

// gWaterEffect (0x0060FA9C) and TimeAttackComplete (0x0060CFC6): real
// names from idb_globals.txt, no repo header owns them yet.
static i32 * const gWaterEffect = reinterpret_cast<i32*>(0x0060FA9C);
static u8 * const gTimeAttackComplete = reinterpret_cast<u8*>(0x0060CFC6);

// gSimpleMessageRelated (0x0060D594) and gSimpleMessageTextWidth
// (0x0060D230): real names from idb_globals.txt. The TextBox command
// uses them to place the box next to the simple-message text.
static i32 * const gSimpleMessageRelated = reinterpret_cast<i32*>(0x0060D594);
static i32 * const gSimpleMessageTextWidth = reinterpret_cast<i32*>(0x0060D230);

// 0x0060F76C: written with the same value as gWideScreen (ps2m3d.h) by
// the WideScreen command, and cleared next to it in CPlayer::~CPlayer
// (0x4BAA30). baddy.cpp already calls this address gWideScreenShadow;
// spidey.cpp instead reads it as gAnimWebcart+0xC and panel.cpp as
// gPanelScreenY, so the repo does not agree on it yet. Kept file-local
// with baddy.cpp's name because that is the reading that matches how
// this function uses it.
static i32 * const gWideScreenShadow = reinterpret_cast<i32*>(0x0060F76C);

// OTPushback (0x00660F78) is a real idb_globals.txt name. The word right
// after it is written by the SetOTPushback2 command; the IDB does not
// name it, so this is a guess based only on that debug string. They may
// well be one two-element array in the original source.
static i16 * const gOTPushback = reinterpret_cast<i16*>(0x00660F78);
static i16 * const gOTPushback2 = reinterpret_cast<i16*>(0x00660F7A);

// Written by the SetGameLevel command and read nowhere else in the
// binary (single xref). Tentative name.
static i32 * const gTrigGameLevel = reinterpret_cast<i32*>(0x005FCD14);

// 0x005498FC holds a char* scratch buffer that SpoolIn loads SkipLib.txt
// into (the original does "mov esi, [5498FC]", a pointer load, not an
// address). idb_globals.txt calls the slot gSpoolSystemMemory, but
// dcmodel.cpp and bit.cpp treat the same address as the buffer itself
// rather than a pointer to one, so one of the two readings is wrong.
// (gSpoolSystemMemory itself is declared in spool.h now)

// char* holding "Checkpoint", shown by Mess_Message when a SetRestart
// command moves the restart node. Part of a string table; tentative name.
static const char * const * const gCheckpointMessage =
	reinterpret_cast<const char* const*>(0x0054B88C);

// Five globals CPlayer::~CPlayer (0x4BAA30) swaps with gSaveGame fields
// 0x48/0x4C/0x50/0x79/0x7A so player state survives a level load. The
// LoadNewTrg command copies them back the other way when it reloads the
// level that is already running. What each field means is not known yet;
// the names describe the mechanism only.
static i32 * const gCarriedPlayerStat0 = reinterpret_cast<i32*>(0x006A9058);
static i32 * const gCarriedPlayerStat1 = reinterpret_cast<i32*>(0x006A905C);
static u8  * const gCarriedPlayerFlag0 = reinterpret_cast<u8*>(0x006A9060);
static i32 * const gCarriedPlayerStat2 = reinterpret_cast<i32*>(0x006A9064);
static u8  * const gCarriedPlayerFlag1 = reinterpret_cast<u8*>(0x006A9068);

// Text templates the TextMessage command substitutes control names into.
// Original tables: patterns+formats at 0x00558054 (9 entries, 8 bytes
// each), the action ids at 0x0055809C (9 entries, two i16 each), and the
// 9 x 256 byte output buffers at 0x006B3914. Reproduced as real arrays
// because nothing else in the binary touches them.
struct STrigTextSubst
{
	const char* mPattern;   // '?' matches any single character
	const char* mFormat;
};

static const STrigTextSubst gTrigTextSubst[9] =
{
	{ "?? to Punch and ?? to Kick",           "%s to Punch and %s to Kick"  },
	{ "?? to fire webs",                      "%s to fire webs"             },
	{ "Press Trigger R while",                "Press %s while in"           },
	{ "in mid-air to Swing",                  "mid-air to Swing"            },
	{ "Hold Trigger L For Target Mode",       "Hold %s For Target Mode"     },
	{ "Press Trigger L + ?? to Zip line up",  "Press %s to Zip line up"     },
	{ "Press ?? to pick Objects up",          "Press %s to pick Objects up" },
	{ "Hold L trigger to go",                 "Hold %s to go"               },
	{ "Press ",                               "Press %s in mid-air to swing"}
};

struct STrigTextSubstActions
{
	i16 mAction0;
	i16 mAction1;
};

static const STrigTextSubstActions gTrigTextSubstActions[9] =
{
	{ 0x0040, 0x0020 },
	{ 0x0080, 0x0000 },
	{ 0x0400, 0x0000 },
	{ 0x0000, 0x0000 },
	{ 0x0100, 0x0000 },
	{ 0x0200, 0x0000 },
	{ 0x0040, 0x0000 },
	{ 0x0100, 0x0000 },
	{ 0x0010, 0x0000 }
};

static char gTrigTextSubstBuffers[9][256];

// The CPlayer fields the trigger commands poke are not named in spidey.h
// yet (that file belongs to another change), so they are reached by byte
// offset here. Offsets read straight off the original: 0x1A4 cutscene
// skip flag, 0x34C footstep bank, 0x574 shadow RGB, 0x578 body RGB,
// 0xC60 fight music time, 0xC68/0xC69 fight music fade down/up, 0xE34
// motion angle offset, 0xE40/0xE44 drop damage range, 0xEC8/0xECC spidey
// sense buzz.
// @Bogus
static i32* TrigFieldI32(void* pObject, u32 offset)
{
	return reinterpret_cast<i32*>(reinterpret_cast<u8*>(pObject) + offset);
}

// @Bogus
static i16* TrigFieldI16(void* pObject, u32 offset)
{
	return reinterpret_cast<i16*>(reinterpret_cast<u8*>(pObject) + offset);
}

// @Bogus
static u8* TrigFieldU8(void* pObject, u32 offset)
{
	return reinterpret_cast<u8*>(pObject) + offset;
}

// The original calls _strlwr, which only exists in the MSVC runtime.
// @Bogus
static char* TrigToLower(char* pText)
{
	for (char* p = pText; *p; p++)
	{
		if (*p >= 'A' && *p <= 'Z')
			*p = static_cast<char>(*p + 32);
	}

	return pText;
}

// @Ok
static u16* TrigAlign4(u16* pCommands)
{
	return reinterpret_cast<u16*>((reinterpret_cast<u32>(pCommands) + 3) & ~3u);
}

// Six 12-bit fixed point values (a min/max box), 4-byte aligned first.
// @Ok
static u16* TrigReadBox(u16* pCommands, CVector* pMin, CVector* pMax)
{
	i32* pRaw = reinterpret_cast<i32*>(TrigAlign4(pCommands));

	pMin->vx = pRaw[0] << 12;
	pMin->vy = pRaw[1] << 12;
	pMin->vz = pRaw[2] << 12;
	pMax->vx = pRaw[3] << 12;
	pMax->vy = pRaw[4] << 12;
	pMax->vz = pRaw[5] << 12;

	return reinterpret_cast<u16*>(pRaw + 6);
}

// Walks past a cutscene script: words until a zero word, repeated until
// the word after the zero is 0xFF, then one more word. Same walk
// CPlayer::SwitchToSynthesizedInput (0x4BC1A0) does internally and
// returns the end of; spidey.h declares that method void, so the cursor
// is recomputed here instead.
// @Ok
static u16* TrigSkipCutSceneScript(u16* pCommands)
{
	do
	{
		while (*pCommands++ != 0)
			;
	}
	while (*pCommands != 0xFF);

	return pCommands + 1;
}

// Wildcard compare used by the TextMessage command: '?' in the pattern
// matches any single character, everything else must match exactly and
// both strings must end together.
// @Ok
static i32 TrigTextMatches(const char* pPattern, const char* pText)
{
	char patternChar = *pPattern;

	while (patternChar != 0)
	{
		if (*pText == 0)
			break;

		if (patternChar != '?' && patternChar != *pText)
			break;

		pPattern++;
		pText++;
		patternChar = *pPattern;
	}

	return (*pPattern == 0) && (*pText == 0);
}

// @Ok
// Reads one command and returns the start of the next one, WITHOUT
// executing it. In the original this is a separate function (its assert
// says "Unknown command, need to update SkipCommand") that MSVC inlined
// into the IfPulseCount case of ExecuteCommandList; the argument sizes
// below were read off that inlined switch at 0x4E1595 / 0x4E1613.
// Faithful to two original bugs: commands 193/194 (SendPushback) and
// 303/304 (FadePalettesUp/Down) are missing from the table even though
// ExecuteCommandList itself handles them, so skipping over one of those
// inside an IfPulseCount block desyncs the cursor.
static u16* SkipCommand(u16* pCommands)
{
	u32 command = *pCommands++;

	switch (command)
	{
		// SetCheatRestarts: a list of strings ended by an empty one.
		case 2:
			if (*reinterpret_cast<char*>(pCommands) != 0)
			{
				do
				{
					pCommands = SkipString(reinterpret_cast<char*>(pCommands));
				}
				while (*reinterpret_cast<char*>(pCommands) != 0);
			}
			pCommands++;
			break;

		// No arguments.
		case 3: case 4: case 5: case 10: case 11: case 12:
		case 102: case 103: case 110: case 121: case 122: case 129:
		case 136: case 137: case 149: case 150: case 162: case 173:
		case 175:
		case 205: case 215: case 216: case 218: case 219: case 220:
		case 300: case 301:
		case 305:
			break;

		// One word.
		case 13: case 105: case 106: case 131: case 132: case 134:
		case 138: case 147: case 148: case 151: case 153: case 154:
		case 155: case 156: case 160: case 163: case 164: case 165:
		case 166: case 168: case 169: case 170: case 172:
		case 177: case 185: case 186: case 187: case 190: case 195:
		case 197: case 203: case 204: case 206: case 207: case 211:
		case 213: case 217: case 222: case 223:
		case 302:
			pCommands++;
			break;

		// Two words.
		case 130: case 135: case 143: case 144: case 145: case 146:
		case 167: case 174:
		case 188: case 196: case 200: case 202: case 210: case 212:
		case 221:
			pCommands += 2;
			break;

		// Three words.
		case 104: case 139:
		case 182: case 183: case 184:
			pCommands += 3;
			break;

		// Five words, 4-byte aligned first (BackgroundCreate).
		case 171:
			pCommands = TrigAlign4(pCommands) + 5;
			break;

		// Five words, no alignment.
		case 180: case 214:
			pCommands += 5;
			break;

		// Eight words (TextBox).
		case 208:
			pCommands += 8;
			break;

		// One string.
		case 115: case 119: case 126: case 127: case 128: case 140:
		case 142:
		case 176: case 181: case 189:
			pCommands = SkipString(reinterpret_cast<char*>(pCommands));
			break;

		// One string then three words.
		case 191: case 198:
			pCommands = SkipString(reinterpret_cast<char*>(pCommands)) + 3;
			break;

		// A cutscene script.
		case 199:
			pCommands = TrigSkipCutSceneScript(pCommands);
			break;

		// Two words then a list of boxes.
		case 141: case 192: case 209:
			pCommands += 2;
			// fall through
		// A list of 24 byte boxes ended by a 0xFF word.
		case 133:
			if (*pCommands != 0xFF)
			{
				do
				{
					pCommands = TrigAlign4(pCommands) + 12;
				}
				while (*pCommands != 0xFF);
			}
			pCommands++;
			break;

		// End of the command list. The original zeroes the cursor here.
		case 0xFFFF:
			pCommands = 0;
			break;

		default:
			print_if_false(0, "Unknown command, need to update SkipCommand");
			break;
	}

	return pCommands;
}

// @Ok
// The level trigger/script interpreter, from the original at 0x4E0210
// (0x299E bytes, 3041 instructions, a 304 case jump table at 0x4E02BE
// plus a shared default). Session bar is functional parity, not a byte
// match. Every opcode below was read off the raw disassembly, and the
// per-command argument sizes were cross-checked against the inlined
// SkipCommand table above, so the read cursor stays in step even for the
// commands that only log and do nothing.
//
// Differences from the original that are deliberate:
//   - The original inlines Trig_GetLinksPointer, Trig_SendPulse,
//     Trig_SendPulseToNode, Trig_SendSignalToLinks, Trig_SetRestart,
//     SkipString and SkipCommand into this function. They are all
//     already decompiled in this file, so they are called here instead
//     of being pasted in again; the behaviour is the same.
//   - Two walks of a list that throw the result away (pSimpleMessages in
//     TextMessage, TextBoxList in TextBox) are dropped, they have no
//     effect.
//   - Debug logging goes through trigLog / print_if_false, both empty in
//     this build, exactly as in the original (the linker folded the two
//     empty bodies into one address, 0x4015B0).
//
// Original bugs kept on purpose:
//   - The default case does not advance the cursor, so an unknown
//     command spins forever. Real .trg files never emit one.
//   - Command 115 (Text) logs "Command no longer supported" and does not
//     skip its string argument, unlike SkipCommand.
//   - SetRestart sets IsRestartDeath when the restart point is NOT
//     called "re_start_death" (the already decompiled Trig_SetRestart
//     above has the same inverted test, it is what the binary does).
void ExecuteCommandList(u16* pCommands, i32 Node, i32 WaitForSpooling)
{
	SCommandPoint* pCommandPoint = 0;

	if (Node != 0xFFFF && *G_OFFSETLIST[Node] == 6)
	{
		for (SCommandPoint* pSearch = G_COMMANDPOINTS; pSearch; pSearch = pSearch->pNext)
		{
			if (pSearch->NodeIndex == Node)
			{
				pCommandPoint = pSearch;
				break;
			}
		}
	}

	trigLog("** Executing Command-List: %8.8X, Node: %i **", pCommands, Node);

	i32 ifDepth = 0;
	i32 fogChanged = 0;
	i32 fogNear = 0;
	i32 fogFar = 0;
	i32 fogValue = 0;

	u16* p = pCommands;
	u16 command = *p;

	while (command != 0xFFFF)
	{
		p++;

		switch (command)
		{
			// SetCheatRestarts (0x4E051A)
			case 2:
			{
				trigLog("SetCheatRestarts");
				G_NUMCHEATRESTARTS = 0;

				while (*reinterpret_cast<char*>(p) != 0)
				{
					print_if_false(G_NUMCHEATRESTARTS < 20, "Too many strings in SetCheatRestarts");

					gCheatRestartNames[G_NUMCHEATRESTARTS] = reinterpret_cast<char*>(p);
					G_NUMCHEATRESTARTS++;

					p = SkipString(reinterpret_cast<char*>(p));

					trigLog("\tCheatRestart[%i] = %s",
							G_NUMCHEATRESTARTS - 1,
							gCheatRestartNames[G_NUMCHEATRESTARTS - 1]);
				}

				p++;
				break;
			}

			// SendPulse (0x4E1747)
			case 3:
			{
				trigLog("SendPulse");
				print_if_false(Node != 0xFFFF,
						"SendPulse command requires the command list to be associated with a node.");

				if (pCommandPoint)
				{
					if (pCommandPoint->NumPulses == 0)
						break;

					Trig_SendPulse(Trig_GetLinksPointer(Node));

					if (pCommandPoint->NumPulses != 0xFFFF)
						pCommandPoint->NumPulses--;
				}
				else
				{
					Trig_SendPulse(Trig_GetLinksPointer(Node));
				}
				break;
			}

			// SendActivate (4) / SendSuspend (5) (0x4E1B14)
			case 4:
			case 5:
			{
				if (command == 5)
					trigLog("SendSuspend");
				else
					trigLog("SendActivate");

				print_if_false(Node != 0xFFFF,
						"SendSuspend or SendActivate require the command list to be associated with a node.");

				SendSuspendOrActivate(Trig_GetLinksPointer(Node), command);
				break;
			}

			// SendSignal (0x4E1C07)
			case 10:
			{
				trigLog("SendSignal");
				print_if_false(Node != 0xFFFF,
						"SendSignal command requires the command list to be associated with a node.");

				Trig_SendSignalToLinks(Trig_GetLinksPointer(Node));
				break;
			}

			// SendKill (0x4E1ED8)
			case 11:
				trigLog("SendKill");
				SendKillFromNode(Node, 0);
				break;

			// SendKillLoudly (0x4E1EF5)
			case 12:
				trigLog("SendKillLoudly");
				SendKillFromNode(Node, 1);
				break;

			// SendVisible (0x4E1D7C)
			case 13:
			{
				trigLog("SendVisible");

				u16 hide = *p;

				print_if_false(Node >= 0 && Node < G_NUMNODES, "Bad node sent to SendVisible");

				u16* pLinks = Trig_GetLinksPointer(Node);
				u16 numLinks = *pLinks;
				u16* pLink = pLinks + 1;

				for (i32 i = 0; i < numLinks; i++)
				{
					i16* pNode = reinterpret_cast<i16*>(G_OFFSETLIST[pLink[i]]);

					if (*pNode == 2 || *pNode == 9)
					{
						u32 pChecksum = reinterpret_cast<u32>(&pNode[pNode[1] + 2]);
						if (pChecksum & 2)
							pChecksum += 2;

						CItem* pItem = Spool_FindEnviroItem(*reinterpret_cast<u32*>(pChecksum));
						if (pItem)
						{
							if (hide)
								pItem->mFlags &= 0xFFFE;
							else
								pItem->mFlags |= 1;
						}
					}
					else
					{
						print_if_false(0, "SendVisible to non-crate");
					}
				}

				p++;
				break;
			}

			// WaterEffectOn (0x4E111D)
			case 102:
				trigLog("WaterEffectOn");
				*gWaterEffect = 1;
				break;

			// WaterEffectOff (0x4E1139)
			case 103:
				trigLog("WaterEffectOff");
				*gWaterEffect = 0;
				break;

			// SetFoggingParams (0x4E1203), applied once at the end.
			case 104:
				trigLog("Trigger file changes zYon plane!\r\n");
				trigLog("SetFoggingParams");
				fogNear = *p++;
				fogFar = *p++;
				fogValue = *p++;
				fogChanged = 1;
				break;

			// PlaySound (0x4E1373)
			case 105:
				trigLog("PlaySound(%i)", *p);
				SFX_Play(*p, 0x2000, 0);
				p++;
				break;

			// StopSound (0x4E13A0)
			case 106:
				trigLog("StopSound(%i)", *p);
				SFX_Stop(*p);
				p++;
				break;

			// ClearAllPSXs (0x4E03F2)
			case 110:
				trigLog("ClearAllPSXs");
				Spool_ClearAllPSXs();
				break;

			// Text (0x4E1F12). Does not skip its string, unlike SkipCommand.
			case 115:
				trigLog("Text (unimplemented)");
				print_if_false(0, "Command no longer supported");
				break;

			// DebugText (0x4E1F30)
			case 119:
				trigLog("DebugText(%s)", reinterpret_cast<char*>(p));
				p = SkipString(reinterpret_cast<char*>(p));
				break;

			// CamFollowPath (0x4E1F5B)
			case 121:
				trigLog("CamFollowPath (unimplemented)");
				print_if_false(0, "CamFollowPath not currently supported");
				break;

			// ClearOtherRegion (0x4E1518)
			case 122:
				trigLog("ClearOtherRegion (unimplemented)");
				print_if_false(0, "Clear other region not done yet");
				break;

			// SpoolIn (0x4E06C9)
			case 126:
			{
				char* pName = reinterpret_cast<char*>(p);
				trigLog("SpoolIn(%s)\r\n", pName);
				TrigToLower(pName);

				i32 skipIt = 0;

				if (*gTrigLoadedLowRes == 0)
				{
					char Delimiter[6] = "\r \t\n";

					FileIO_Open("SkipLib.txt");

					char* pSkipLib = static_cast<char*>(gSpoolSystemMemory);
					FileIO_Load(pSkipLib);
					FileIO_Sync();

					for (char* pToken = strtok(pSkipLib, Delimiter);
							pToken;
							pToken = strtok(0, Delimiter))
					{
						TrigToLower(pToken);

						if (strcmp(pToken, pName) == 0)
						{
							skipIt = 1;
							break;
						}
					}
				}

				if (!skipIt)
				{
					Spool_PSX(pName, 0);

					if (WaitForSpooling)
						Spool_Sync();
				}

				p = SkipString(pName);
				break;
			}

			// SpoolOut (0x4E07B8)
			case 127:
				trigLog("SpoolOut(%s)", reinterpret_cast<char*>(p));
				Spool_ClearPSX(reinterpret_cast<char*>(p));
				p = SkipString(reinterpret_cast<char*>(p));
				break;

			// SpoolEnv (0x4E07E9)
			case 128:
			{
				char* pName = reinterpret_cast<char*>(p);
				trigLog("SpoolEnv(%s)", pName);
				Spool_PSX(pName, 1);

				if (WaitForSpooling)
					Spool_Sync();

				p = SkipString(pName);
				break;
			}

			// SpoolLock (0x4E0872)
			case 129:
				trigLog("SpoolLock");
				Spool_Sync();
				break;

			// SetCamAngle (0x4E23B0)
			case 130:
				trigLog("SetCamAngle");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamAngle(static_cast<i16>(p[0]), p[1]);
				p += 2;
				break;

			// BackgroundOn (0x4E11CD)
			case 131:
				trigLog("BackgroundOn");
				Backgrnd_On(*p);
				p++;
				break;

			// BackgroundOff (0x4E11E8)
			case 132:
				trigLog("BackgroundOff");
				Backgrnd_Off(*p);
				p++;
				break;

			// KillEverythingInBox (0x4E08FB)
			case 133:
			{
				CVector boxMin;
				CVector boxMax;
				boxMin.vx = 0; boxMin.vy = 0; boxMin.vz = 0;
				boxMax.vx = 0; boxMax.vy = 0; boxMax.vz = 0;

				while (*p != 0xFF)
				{
					p = TrigReadBox(p, &boxMin, &boxMax);

					trigLog("KillEverythingInBox (%f,%f,%f) - (%f,%f,%f)",
							boxMin.vx / 4096.0f, boxMin.vy / 4096.0f, boxMin.vz / 4096.0f,
							boxMax.vx / 4096.0f, boxMax.vy / 4096.0f, boxMax.vz / 4096.0f);

					Utils_KillEverythingInBox(&boxMin, &boxMax);
				}

				p++;
				break;
			}

			// SetInitialPulses (0x4E170C)
			case 134:
				trigLog("SetInitialPulses");
				print_if_false(pCommandPoint != 0,
						"SetInitialPulses command requires a command point");

				if (!pCommandPoint->NumPulsesSet)
				{
					pCommandPoint->NumPulsesSet = 1;
					pCommandPoint->NumPulses = *p;
				}

				p++;
				break;

			// SetCamDistXZ (0x4E2405)
			case 135:
				trigLog("SetCamDistXZ");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamXZDistance(p[0], p[1]);
				p += 2;
				break;

			// AllowXA (0x4E2778)
			case 136:
				trigLog("AllowXA");
				Redbook_XAAllow(true);
				break;

			// DisallowXA (0x4E278E)
			case 137:
				trigLog("DisallowXA");
				Redbook_XAAllow(false);
				break;

			// SeekXA (0x4E27A4). The seek helper is an empty function in
			// this build (0x430880), so nothing is called here.
			case 138:
				trigLog("SeekXA");
				p++;
				break;

			// PlayXA (0x4E27C4)
			case 139:
				trigLog("PlayXA(%i)", p[0]);
				Redbook_XAPlay(p[0], p[1], p[2]);
				p += 3;
				break;

			// SetRestart (0x4E05B7)
			case 140:
			case 176:
			{
				char* pName = reinterpret_cast<char*>(p);
				trigLog("SetRestart = %s", pName);

				i32 previousRestart = G_RESTARTNODE;

				Trig_SetRestart(pName);

				p = SkipString(pName);

				if (previousRestart != 0xFFFF && G_RESTARTNODE != previousRestart)
				{
					if (command != 176)
					{
						Mess_DeleteAll();
						Mess_Message(*gCheckpointMessage, 0);
					}

					Front_SaveGameState();
				}
				break;
			}

			// SetVisibilityInBox (141) / SetBaddyVisibilityInBox (192) /
			// SetObjectVisibilityInBox (209) (0x4E0ACE)
			case 141:
			case 192:
			case 209:
			{
				u16 visible = p[0];
				u16 inside = p[1];
				p += 2;

				CVector boxMin;
				CVector boxMax;
				boxMin.vx = 0; boxMin.vy = 0; boxMin.vz = 0;
				boxMax.vx = 0; boxMax.vy = 0; boxMax.vz = 0;

				while (*p != 0xFF)
				{
					p = TrigReadBox(p, &boxMin, &boxMax);

					if (command == 192)
					{
						trigLog("SetBaddyVisibilityInBox (%f,%f,%f) - (%f,%f,%f), %s, %s",
								boxMin.vx / 4096.0f, boxMin.vy / 4096.0f, boxMin.vz / 4096.0f,
								boxMax.vx / 4096.0f, boxMax.vy / 4096.0f, boxMax.vz / 4096.0f,
								visible ? "Visible" : "Invisible",
								inside ? "Inside" : "Outside");

						Utils_SetBaddyVisibilityInBox(&boxMin, &boxMax,
								visible != 0, inside != 0, reinterpret_cast<CBody*>(BaddyList));
					}
					else if (command == 209)
					{
						trigLog("SetObjectVisibilityInBox (%f,%f,%f) - (%f,%f,%f), %s, %s",
								boxMin.vx / 4096.0f, boxMin.vy / 4096.0f, boxMin.vz / 4096.0f,
								boxMax.vx / 4096.0f, boxMax.vy / 4096.0f, boxMax.vz / 4096.0f,
								visible ? "Visible" : "Invisible",
								inside ? "Inside" : "Outside");

						Utils_SetBaddyVisibilityInBox(&boxMin, &boxMax,
								visible != 0, inside != 0, G_ENVIRONMENTAL_OBJECT_LIST);
						Utils_SetBaddyVisibilityInBox(&boxMin, &boxMax,
								visible != 0, inside != 0, PowerUpList);
					}
					else
					{
						trigLog("SetVisibilityInBox (%f,%f,%f) - (%f,%f,%f), %s, %s",
								boxMin.vx / 4096.0f, boxMin.vy / 4096.0f, boxMin.vz / 4096.0f,
								boxMax.vx / 4096.0f, boxMax.vy / 4096.0f, boxMax.vz / 4096.0f,
								visible ? "Visible" : "Invisible",
								inside ? "Inside" : "Outside");

						Utils_SetVisibilityInBox(&boxMin, &boxMax, visible != 0, inside != 0);
					}
				}

				p++;
				break;
			}

			// SetObjFile (0x4E08BA)
			case 142:
			{
				char* pName = reinterpret_cast<char*>(p);
				trigLog("SetObjFile(%s)", pName);

				G_OBJ_FILE = pName;
				p = SkipString(pName);
				gObjFileRegion = static_cast<u8>(Spool_FindRegion(pName));
				break;
			}

			// SetCamDistY (0x4E2432)
			case 143:
				trigLog("SetCamDistY");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamYDistance(static_cast<i16>(p[0]), p[1]);
				p += 2;
				break;

			// SetCamOffsetX (0x4E245F)
			case 144:
				trigLog("SetCamOffsetX");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamXOffset(static_cast<i16>(p[0]), p[1]);
				p += 2;
				break;

			// SetCamOffsetY (0x4E248C)
			case 145:
				trigLog("SetCamOffsetY");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamYOffset(static_cast<i16>(p[0]), p[1]);
				p += 2;
				break;

			// SetCamOffsetZ (0x4E24B9)
			case 146:
				trigLog("SetCamOffsetZ");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCamZOffset(static_cast<i16>(p[0]), p[1]);
				p += 2;
				break;

			// SetGameLevel (0x4E2759)
			case 147:
				trigLog("SetGameLevel");
				*gTrigGameLevel = *p;
				p++;
				break;

			// IfPulseCount (0x4E1536)
			case 148:
			{
				print_if_false(pCommandPoint != 0,
						"IfPulseCount command only valid for a command point");

				u16 pulses = pCommandPoint->PulsesReceived;
				u16 wanted = *p++;

				if (pulses == wanted)
				{
					ifDepth++;
					break;
				}

				while (*p != 0x95)
				{
					print_if_false(*p != 0x94, "Cannot nest IfPulseCount");
					p = SkipCommand(p);
				}

				p++;
				break;
			}

			// Endif (0x4E16EB)
			case 149:
				print_if_false(ifDepth != 0, "Endif without if");
				ifDepth--;
				break;

			// TimeAttackComplete (0x4E13C6)
			case 150:
				trigLog("TimeAttackComplete");
				*gTimeAttackComplete = 1;
				break;

			// SetDualBufferSize (0x4E05A2)
			case 151:
				trigLog("SetDualBufferSize (unimplemented)");
				p++;
				break;

			// KillBruce (0x4E145E)
			case 152:
				trigLog("KillBruce");
				if (G_MECHLIST)
					reinterpret_cast<CPlayer*>(G_MECHLIST)->SwitchToDeathMode(true);
				break;

			// SetCamColijSide (0x4E268F)
			case 153:
				trigLog("SetCamColijSide");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCollisionRayLR(*reinterpret_cast<i16*>(p));
				p++;
				break;

			// SetCamColijBack (0x4E26B7)
			case 154:
				trigLog("SetCamColijBack");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCollisionRayBack(*reinterpret_cast<i16*>(p));
				p++;
				break;

			// SetReverbType (0x4E13DF)
			case 157:
				trigLog("SetReverbType");
				SFX_SetReverbType(*reinterpret_cast<u8*>(p));
				p++;
				break;

			// EndLevel (0x4E13F9)
			case 158:
				trigLog("EndLevel");
				gLevelStatus = 3;
				break;

			// SpoolMidi (0x4E0889). The midi spooler is an empty function
			// in this build (0x430880).
			case 159:
				trigLog("SpoolMidi(%s)", reinterpret_cast<char*>(p));
				p = SkipString(reinterpret_cast<char*>(p));
				break;

			// SetCamMode (0x4E272F)
			case 160:
				trigLog("SetCamMode");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetMode(static_cast<ECameraMode>(*p));
				p++;
				break;

			// ClearAllCodeModules (0x4E085B)
			case 162:
				trigLog("ClearAllCodeModules");
				Reloc_UnloadAll();
				break;

			// IgnoreBruceInput (0x4E1415). Original bug kept: the cursor
			// only moves when there is a player, so this command desyncs
			// the rest of the list when MechList is null.
			case 163:
				trigLog("IgnoreBruceInput(%u)", *p);
				if (G_MECHLIST)
				{
					reinterpret_cast<CPlayer*>(G_MECHLIST)->SetIgnoreInputTimer(*p);
					p++;
				}
				break;

			// SetCamColijAngleSide (0x4E26DF)
			case 164:
				trigLog("SetCamColijAngleSide");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCollisionAngLR(static_cast<i16>(*p));
				p++;
				break;

			// SetCamColijAngleBack (0x4E2707)
			case 165:
				trigLog("SetCamColijAngleBack");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetCollisionAngBack(static_cast<i16>(*p));
				p++;
				break;

			// SetOTPushback (0x4E1F9F)
			case 166:
				trigLog("SetOTPushback = %i", *reinterpret_cast<i16*>(p));
				*gOTPushback = static_cast<i16>(*p);
				p++;
				break;

			// SetCamZoom (0x4E2331)
			case 167:
				trigLog("SetCamZoom");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->SetZoom(p[0], p[1]);
				p += 2;
				break;

			// SetCamPitchDamp (0x4E2360)
			case 168:
				trigLog("SetCamPitchDamp");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->field_1CC = static_cast<i16>(*p);
				p++;
				break;

			// SetOTPushback2 (0x4E1FC2)
			case 169:
				trigLog("SetOTPushback2 = %i", *reinterpret_cast<i16*>(p));
				*gOTPushback2 = static_cast<i16>(*p);
				p++;
				break;

			// SetSuspendDistance (0x4E1F79)
			case 170:
				trigLog("SetSuspendDistance(%i)", *p);
				SuspendedDistance = *p;
				p++;
				break;

			// BackgroundCreate (0x4E1155)
			case 171:
			{
				trigLog("BackgroundCreate");

				p = TrigAlign4(p);

				u32 backgroundId = *reinterpret_cast<u32*>(p);

				CSVector pos;
				pos.vx = static_cast<i16>(p[2]);
				pos.vy = static_cast<i16>(p[3]);
				pos.vz = static_cast<i16>(p[4]);
				p += 5;

				new CBackground(backgroundId, &pos);
				break;
			}

			// SetCamYDamp (0x4E2388)
			case 172:
				trigLog("SetCamYDamp");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->field_1CE = static_cast<i16>(*p);
				p++;
				break;

			// SetCamFocusEqualsTripod (0x4E2306)
			case 173:
				trigLog("SetCamFocusEqualsTripod");
				if (G_CAMERA_LIST)
					G_CAMERA_LIST->field_13C = G_CAMERA_LIST->mTripod;
				break;

			// SetDropDamageOn (0x4E27F8)
			case 174:
				trigLog("SetDropDamageOn");
				if (G_MECHLIST)
				{
					CPlayer* pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);
					*TrigFieldI32(pPlayer, 0xE40) = p[0];
					*TrigFieldI32(pPlayer, 0xE44) = p[1];
				}
				p += 2;
				break;

			// SetDropDamageOff (0x4E282E)
			case 175:
				trigLog("SetDropDamageOff");
				if (G_MECHLIST)
					*TrigFieldI32(G_MECHLIST, 0xE40) = 0;
				break;

			// IgnoreBruceInputFreeze (0x4E1449)
			case 177:
				trigLog("IgnoreBruceInputFreeze (unimplemented)");
				p++;
				break;

			// SetSpideyCamValue (0x4E24E6)
			case 180:
				trigLog("SetSpideyCamValue");
				if (G_MECHLIST)
				{
					reinterpret_cast<CPlayer*>(G_MECHLIST)->SetSpideyCamValue(
							p[0], p[1], static_cast<i16>(p[2]), p[3], p[4]);
				}
				p += 5;
				break;

			// LoadNewTrg (0x4E2857)
			case 181:
			{
				char* pName = reinterpret_cast<char*>(p);
				trigLog("LoadNewTrg(%s)", pName);

				u8* pSaveBytes = reinterpret_cast<u8*>(&G_SAVE_GAME);

				if (Utils_CompareStrings(pName, G_SAVE_GAME.mLevelName))
				{
					if (gLevelStatus == 0 && G_MECHLIST == 0)
					{
						*reinterpret_cast<i32*>(pSaveBytes + 0x48) = *gCarriedPlayerStat0;
						*reinterpret_cast<i32*>(pSaveBytes + 0x4C) = *gCarriedPlayerStat1;
						pSaveBytes[0x79] = *gCarriedPlayerFlag0;
						*reinterpret_cast<i32*>(pSaveBytes + 0x50) = *gCarriedPlayerStat2;
						pSaveBytes[0x7A] = *gCarriedPlayerFlag1;
					}

					gLevelStatus = 9;
				}
				else
				{
					gLevelStatus = 3;
				}

				Utils_CopyString(pName, G_SAVE_GAME.mLevelName, 9);

				p = SkipString(pName);

				Init_KillAll();
				break;
			}

			// SetSpideyRGB (0x4E1254)
			case 182:
				trigLog("SetSpideyRGB");
				*TrigFieldI32(G_MECHLIST, 0x578) = *p;
				p += 3;
				break;

			// SetSpideyLookAroundCamValue (0x4E265D)
			case 183:
				trigLog("SetSpideyLookAroundCamValue");
				if (G_MECHLIST)
				{
					reinterpret_cast<CPlayer*>(G_MECHLIST)->SetSpideyLookaroundCamValue(
							p[0], p[1], static_cast<i16>(p[2]));
				}
				p += 3;
				break;

			// SetSpideyShadowRGB (0x4E1279)
			case 184:
				trigLog("SetSpideyShadowRGB");
				*TrigFieldI32(G_MECHLIST, 0x574) = *p;
				p += 3;
				break;

			// SetCamFixedPos (185) / with angles (186) (0x4E1FE4)
			case 185:
			case 186:
			{
				trigLog("SetCamFixedPos");
				print_if_false(G_CAMERA_LIST != 0, "No camera for SETCAM...");

				if (G_CAMERA_LIST == 0)
				{
					p++;
					break;
				}

				u16* pLinks = Trig_GetLinksPointer(Node);
				print_if_false(*pLinks != 0, "SETCAMFIXEDPOS with no linked nodes");

				u16 numLinks = *pLinks++;
				if (numLinks == 0)
				{
					p++;
					break;
				}

				u16 camNode = 0;
				i32 found = 0;

				do
				{
					camNode = *pLinks++;

					if (*G_OFFSETLIST[camNode] == 0x0C)
					{
						found = 1;
						break;
					}

					numLinks--;
				}
				while (numLinks != 0);

				if (!found)
				{
					p++;
					break;
				}

				CVector pos;
				pos.vx = 0; pos.vy = 0; pos.vz = 0;

				u16* pAngles = Trig_GetPosition(&pos, camNode);

				if (command == 185)
				{
					G_CAMERA_LIST->SetFixedPosMode(pos, *p);
				}
				else
				{
					CQuat angles;
					angles.x = *reinterpret_cast<i16*>(&pAngles[0]);
					angles.y = *reinterpret_cast<i16*>(&pAngles[1]);
					angles.z = *reinterpret_cast<i16*>(&pAngles[2]);
					angles.w = *reinterpret_cast<i16*>(&pAngles[3]);

					G_CAMERA_LIST->SetFixedPosAnglesMode(&pos, &angles, *p);
				}

				p++;
				break;
			}

			// SetCamAngleLock (0x4E23DD)
			case 187:
				trigLog("SetCamAngleLock");
				if (G_MECHLIST)
					reinterpret_cast<CPlayer*>(G_MECHLIST)->SetCamAngleLock(*p);
				p++;
				break;

			// SetCamFixedFocus (0x4E21A2)
			case 188:
			{
				trigLog("SetCamFixedFocus");
				print_if_false(G_CAMERA_LIST != 0, "No camera for SETCAM...");

				if (G_CAMERA_LIST == 0)
				{
					p += 2;
					break;
				}

				u16* pLinks = Trig_GetLinksPointer(Node);
				print_if_false(*pLinks != 0, "SETCAMFIXEDPOS with no linked nodes");

				u16 numLinks = *pLinks++;
				if (numLinks == 0)
				{
					p += 2;
					break;
				}

				u16 camNode = 0;
				i32 found = 0;

				do
				{
					camNode = *pLinks++;

					if (*G_OFFSETLIST[camNode] == 0x0C)
					{
						found = 1;
						break;
					}

					numLinks--;
				}
				while (numLinks != 0);

				if (found)
				{
					CVector pos;
					pos.vx = 0; pos.vy = 0; pos.vz = 0;

					Trig_GetPosition(&pos, camNode);

					G_CAMERA_LIST->SetFixedFocusMode(&pos, p[0], p[1]);
				}

				p += 2;
				break;
			}

			// SpoolCodeModule (0x4E0828)
			case 189:
				trigLog("SpoolCodeModule(%s)", reinterpret_cast<char*>(p));
				Reloc_Load(reinterpret_cast<char*>(p), 1);
				p = SkipString(reinterpret_cast<char*>(p));
				break;

			// RunCinema (0x4E0409)
			case 190:
			{
				i16 nodeType = *G_OFFSETLIST[Node];

				if ((nodeType == 4 || nodeType == 15) && G_ISRESTARTDEATH)
				{
					trigLog("RunCinema (ignored)");
					G_ISRESTARTDEATH = 0;
				}
				else
				{
					trigLog("RunCinema(%i)", *p);
					Cinema_Run(*p);
				}

				p++;
				break;
			}

			// SetVisibilityByName (0x4E109C)
			case 191:
			{
				char* pName = reinterpret_cast<char*>(p);
				p = SkipString(pName);

				u16 first = p[0];
				u16 count = p[1];
				p += 2;

				u16 visible = *p++;

				Utils_SetVisibilityByName(pName, first, count, visible != 0);

				trigLog("SetVisibilityByName: %s, %u, %u, %s",
						pName, first, count, visible ? "Visible" : "Invisible");
				break;
			}

			// SendPushback (193) / SendPushback2 (194) (0x4E0A57)
			case 193:
			case 194:
			{
				trigLog("SendPushback = %i", *reinterpret_cast<i16*>(p));

				u16 value = *p++;
				u16 count = *p;

				p = reinterpret_cast<u16*>((reinterpret_cast<u32>(p) + 5) & ~3u);

				for (i32 i = 0; i < count; i++)
				{
					u32 checksum = *reinterpret_cast<u32*>(p);
					p += 2;

					CItem* pItem = Spool_FindEnviroItem(checksum);
					print_if_false(pItem != 0, "Bad checksum in SENDPUSHBACK command");

					if (pItem)
					{
						if (command == 193)
							pItem->mDummyFrame = static_cast<u8>(value);
						else
							pItem->mDummyAnim = static_cast<u8>(value);
					}
				}
				break;
			}

			// WideScreen (0x4E0488)
			case 195:
				trigLog("WideScreen = %i", *p);
				gWideScreen = *p;
				*gWideScreenShadow = *p;
				p++;
				break;

			// BuzzSpideySense (0x4E04B2)
			case 196:
				trigLog("BuzzSpideySense(%i,%i)", p[0], p[1]);
				{
					CPlayer* pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);
					*TrigFieldI32(pPlayer, 0xEC8) = p[0];
					*TrigFieldI32(pPlayer, 0xECC) = p[1];
				}
				p += 2;
				break;

			// SetMotionAngleOffset (0x4E04F1)
			case 197:
				trigLog("SetMotionAngleOffset(%i)", *reinterpret_cast<i16*>(p));
				*TrigFieldI16(G_MECHLIST, 0xE34) =
					static_cast<i16>(*p);
				p++;
				break;

			// TextMessage (0x4E0E70)
			case 198:
			{
				char* pMessage = reinterpret_cast<char*>(p);
				trigLog("TextMessage(%s)", pMessage);

				p = SkipString(pMessage);

				u16 messageArg0 = p[0];
				u16 messageArg1 = p[1];
				p += 2;
				u16 messageArg2 = *p++;

				trigLog("\t\tOLD MESSAGE: %s\r\n", pMessage);

				i32 substIndex = 0;
				while (substIndex < 9)
				{
					if (TrigTextMatches(gTrigTextSubst[substIndex].mPattern, pMessage))
						break;

					substIndex++;
				}

				if (substIndex < 9 && gTrigTextSubst[substIndex].mPattern != 0)
				{
					u32 keyCode0 = 0;
					u32 keyCode1 = 0;

					PCINPUT_GetKeyboardMappingForAction(
							gTrigTextSubstActions[substIndex].mAction0, &keyCode0);
					PCINPUT_GetKeyboardMappingForAction(
							gTrigTextSubstActions[substIndex].mAction1, &keyCode1);

					char keyName0[16];
					char keyName1[16];

					DXINPUT_GetKeyName(static_cast<u8>(keyCode0), keyName0);
					DXINPUT_GetKeyName(static_cast<u8>(keyCode1), keyName1);

					char* pBuffer = gTrigTextSubstBuffers[substIndex];

					if (keyCode1 != 0x4000)
					{
						sprintf(pBuffer, gTrigTextSubst[substIndex].mFormat, keyName0, keyName1);
					}
					else if (keyCode0 != 0x4000)
					{
						sprintf(pBuffer, gTrigTextSubst[substIndex].mFormat, keyName0);
					}
					else
					{
						strcpy(pBuffer, gTrigTextSubst[substIndex].mFormat);
					}

					pMessage = pBuffer;
				}

				trigLog("\t\tNEW MESSAGE: %s\r\n", pMessage);

				Mess_SimpleMessage(pMessage, messageArg0, messageArg1, messageArg2);
				break;
			}

			// CutSceneScript (0x4E1485)
			case 199:
				trigLog("CutSceneScript");
				reinterpret_cast<CPlayer*>(G_MECHLIST)->SwitchToSynthesizedInput(
						reinterpret_cast<i16*>(p));
				p = TrigSkipCutSceneScript(p);
				break;

			// SetFadeColor (0x4E129F)
			case 200:
				trigLog("SetFadeColor");
				M3d_FadeColour = (static_cast<u32>(p[0]) << 16) + p[1];
				p += 2;
				break;

			// SetSkyColor (0x4E12CC)
			case 202:
				trigLog("SetSkyColor");
				{
					u32 skyColour = (static_cast<u32>(p[0]) << 16) + p[1];
					G_DB_SKY_COLOR_TARGET = skyColour;
					G_DB_SKY_COLOR = skyColour;
				}
				p += 2;
				Db_UpdateSky();
				break;

			// SetChopperAngle (0x4E296B)
			case 203:
			{
				trigLog("SetChopperAngle");

				CChopper* pChopper = reinterpret_cast<CChopper*>(FindBaddyOfType(0x13E));
				if (pChopper)
					*TrigFieldI32(pChopper, 0x358) = (*p * 182) >> 4;

				p++;
				break;
			}

			// ChopperFollowWayPoints (0x4E2A00)
			case 204:
			{
				trigLog("ChopperFollowWayPoints");

				CChopper* pChopper = reinterpret_cast<CChopper*>(FindBaddyOfType(0x13E));
				if (pChopper)
				{
					i32* pFlags = TrigFieldI32(pChopper, 0x218);
					*pFlags |= 1;
					*TrigFieldI32(pChopper, 0x1F4) = *p;
				}

				p++;
				break;
			}

			// ChopperContinue (0x4E29A7)
			case 205:
			{
				trigLog("ChopperContinue");

				CChopper* pChopper = reinterpret_cast<CChopper*>(FindBaddyOfType(0x13E));
				if (pChopper)
					*TrigFieldI32(pChopper, 0x218) |= 4;

				break;
			}

			// ChopperVelocity (0x4E29D2)
			case 206:
			{
				trigLog("ChopperVelocity");

				CChopper* pChopper = reinterpret_cast<CChopper*>(FindBaddyOfType(0x13E));
				if (pChopper)
					*TrigFieldI32(pChopper, 0x348) = *p;

				p++;
				break;
			}

			// ChopperMinHeight (0x4E2AD4)
			case 207:
			{
				trigLog("ChopperMinHeight");

				CChopper* pChopper = reinterpret_cast<CChopper*>(FindBaddyOfType(0x13E));
				if (pChopper)
				{
					CVector pos;
					pos.vx = 0; pos.vy = 0; pos.vz = 0;

					Trig_GetPosition(&pos, *p);

					*TrigFieldI32(pChopper, 0x350) = pos.vy;
				}

				p++;
				break;
			}

			// TextBox (0x4E2522)
			case 208:
			{
				trigLog("TextBox");

				u8* pRaw = reinterpret_cast<u8*>(p);

				char colour[6];
				colour[0] = pRaw[0x0A];
				colour[1] = pRaw[0x0C];
				colour[2] = pRaw[0x0E];

				colour[0] = static_cast<char>(static_cast<i32>(
							static_cast<u8>(colour[0]) * 0.4f));
				colour[1] = static_cast<char>(static_cast<i32>(
							static_cast<u8>(colour[1]) * 0.4f));
				colour[2] = static_cast<char>(static_cast<i32>(
							static_cast<u8>(colour[2]) * 0.4f));

				i32 x = *reinterpret_cast<i16*>(p);
				i32 y = *reinterpret_cast<i16*>(p + 1);

				i32 offset = *gSimpleMessageRelated - x;
				if (offset < 0)
					offset = -offset;

				new CTextBox(
						x - 2,
						y,
						*gSimpleMessageTextWidth + offset * 2 - 2,
						*reinterpret_cast<i16*>(p + 3) - 2,
						static_cast<u32>(*reinterpret_cast<i16*>(p + 4)),
						reinterpret_cast<CFriction*>(colour));

				p += 8;
				break;
			}

			// SetFightMusicTime (0x4E1303)
			case 213:
				trigLog("SetFightMusicTime");
				*TrigFieldI32(G_MECHLIST, 0xC60) = *p;
				p++;
				break;

			// NightSky (0x4E03DD)
			case 214:
				trigLog("NIghtSky (unimplemented)");
				p += 5;
				break;

			// SetFightMusicFadeDown (0x4E1329)
			case 215:
			{
				trigLog("SetFightMusicFadeDown");
				CPlayer* pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);
				*TrigFieldU8(pPlayer, 0xC68) = 1;
				*TrigFieldU8(pPlayer, 0xC69) = 0;
				break;
			}

			// SetFightMusicFadeUp (0x4E134E)
			case 216:
			{
				trigLog("SetFightMusicFadeUp");
				CPlayer* pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);
				*TrigFieldU8(pPlayer, 0xC69) = 1;
				*TrigFieldU8(pPlayer, 0xC68) = 0;
				break;
			}

			// AllowCamLOSCheck (0x4E0372)
			case 217:
				trigLog("AllowCamLOSCheck(%i)", *p);
				*TrigFieldU8(G_CAMERA_LIST, 0xF9) = (*p != 0) ? 1 : 0;
				p++;
				break;

			// ClearTextMessages (0x4E1085)
			case 218:
				trigLog("ClearTextMessages");
				Mess_ClearSimpleMessages();
				break;

			// ClearTextBoxes (0x4E2646)
			case 219:
				trigLog("ClearTextBoxes");
				Bit_ClearTextBoxes();
				break;

			// VenomEnterWaitState (220) / VenomExitWaitState (221) (0x4E290B)
			case 220:
			case 221:
			{
				CVenom* pVenom = reinterpret_cast<CVenom*>(FindBaddyOfType(0x139));
				if (pVenom == 0)
					break;

				if (command == 220)
				{
					trigLog("VenomEnterWaitState");
					pVenom->EnterWaitState();
				}
				else
				{
					trigLog("VenomExitWaitState");
					pVenom->ExitWaitState(p[0], p[1]);
					p += 2;
				}
				break;
			}

			// CutSceneSkipAllow (0x4E14A5)
			case 222:
				trigLog("CutSceneSkipAllow(%i)", *p);
				*TrigFieldU8(G_MECHLIST, 0x1A4) =
					(*p != 0) ? 1 : 0;
				p++;
				break;

			// SetSpideyFootStepBank (0x4E14EC)
			case 223:
				trigLog("SetSpideyFootStepBank(%i)", *p);
				*TrigFieldI32(G_MECHLIST, 0x34C) = *p;
				p++;
				break;

			// AllowSpeedup (0x4E03B9)
			case 300:
				trigLog("AllowSpeedup (unimplemented)");
				break;

			// DisallowSpeedup (0x4E03CB)
			case 301:
				trigLog("DisallowSpeedup (unimplemented)");
				break;

			// EndLevelNode (0x4E0355)
			case 302:
				EndLevelNode = *p++;
				trigLog("EndLevelNode = %i", EndLevelNode);
				break;

			// FadePalettesUp (0x4E02C5)
			case 303:
			{
				u32 fade[3];
				fade[0] = p[1];
				fade[1] = p[1] >> 8;
				fade[2] = p[0];

				trigLog("FadePalettesUp(%u,%u,%u)", fade[0], fade[1], fade[2]);

				u32 result;
				Reloc_CallUserFunction("mysterio", 1, fade, &result);

				p += 2;
				break;
			}

			// FadePalettesDown (0x4E0327)
			case 304:
			{
				trigLog("FadePalettesDown");

				u32 arg;
				u32 result;
				Reloc_CallUserFunction("mysterio", 2, &arg, &result);
				break;
			}

			// KillEverything (0x4E0471)
			case 305:
				trigLog("KillEverything");
				Init_KillAll();
				break;

			// Unknown commands do NOT advance the cursor in the original,
			// so a bad .trg file hangs the game here. Kept as is.
			default:
				print_if_false(0, "Unknown command\n ");
				break;
		}

		command = *p;
	}

	if (fogChanged && !G_LOWGRAPHICS)
		M3dInit_SetFoggingParams(fogNear, fogFar, fogValue);

	print_if_false(ifDepth == 0, "Missing Endif");
}

// @Ok
void Trig_DoPendingCommandLists(void)
{
    class Executor : public spidey::world::TriggerCommandExecutor {
    public:
        void execute(u16* commands, u16 node) override { ExecuteCommandList(commands, node, 0); }
    } executor;
    spidey::compatibility::pendingTriggerQueue().execute(executor);
}

// @Ok
 void Trig_AddCommandListToPending(u16 nodeIndex, u16* pCommands)
{
    const bool queued = spidey::compatibility::pendingTriggerQueue().append(nodeIndex, pCommands);
    ASSERT(queued, "Pending command list overflow, increase MAXPENDING in trig.cpp");
}

// @Ok
// @Matching
SCommandPoint* Trig_TriggerCommandPoint(u32 checksum, bool assert)
{
    const auto result = spidey::compatibility::triggerCommandPoints().trigger(checksum);
    ASSERT(!result.queueOverflow, "Pending command list overflow, increase MAXPENDING in trig.cpp");
    if (result.point) trigLog("\tCommandPoint Triggered: node %i", result.point->NodeIndex);
    return result.point;
}

// @Ok
 SCommandPoint* GetCommandPoint(i32 Node)
{
    if (Node != 0xFFF && *G_OFFSETLIST[Node] == 6)
        return spidey::compatibility::triggerCommandPoints().findByNode(Node);
    return nullptr;
}

// @Ok
SCommandPoint* CreateCommandPoint(u32 checksum, u16 node, u16* pCommands)
{
    return spidey::compatibility::triggerCommandPoints().create(checksum, node, pCommands);
}

// @Ok
// @Matching
void Trig_DeleteCommandPoints(void)
{
    spidey::compatibility::triggerCommandPoints().clear();
}

// @Ok
 void Trig_ZeroPendingList(void)
{
    spidey::compatibility::pendingTriggerQueue().clear();
}

// @Ok
// @Matching
void Trig_ResetCPExecutedFlags(void)
{
    spidey::compatibility::triggerCommandPoints().rearmUncollidedPoints();
}

// @Ok
void* Trig_GetLinkInfoList(
		i32 a1,
		SLinkInfo* pLink,
		i32 count)
{
	i32 result = 0;

	u16* linksPtr = reinterpret_cast<u16*>(Trig_GetLinksPointer(a1));

	if (*linksPtr)
	{
		u16 *v8 = linksPtr + 1;
		result = *linksPtr;

		if (result)
		{
			for (i32 i = 0; i<result && i < count; i++, v8++)
			{
				u16 *v11 = reinterpret_cast<u16*>(G_OFFSETLIST[*v8]);

				pLink[i].field_0 = *v8;
				pLink[i].field_4 = *v11;
				if (*v11 == 1002)
					pLink[i].field_8 = v11[1];
				else
					pLink[i].field_8 = 0;
				pLink[i].field_C = 0;
			}
		}
	}


	if (result <= count)
	{
		return reinterpret_cast<void*>(result);
	}

	return reinterpret_cast<void*>(count);

}

// The current level code string "lXaXm" lives in G_SAVE_GAME.field_4 (front.h,
// gSaveGame + 4 = 0x0068285C). Offset 6 is a literal 'a' this function skips.

// @Ok
// Functionally correct (verified logic against the disasm); previous session's residue
// note kept below since this session's bar is functional parity, not zero-diff.
// Residue: 3 mnemonic diffs, one per char-range branch. Original does `sub ecx,0x30/0x31/
// 0x51`, ours does `add ecx,-0x30/-0x31/-0x51`, same result. 24 source variants tried
// (declaration order, signedness, temporaries, +=/-=, shift amount, combiner operator,
// helper-function indirection...), all produce the same add. Looks like a genuine MSVC6
// /O2 quirk for "32-bit value live-in from before a branch, adjusted then shifted then
// or'd". See trig.attempts.md.
int Trig_GetLevelID(void)
{
	char levelPrefix = G_SAVE_GAME.mLevelName[0];
	i32 areaCode = static_cast<i8>(G_SAVE_GAME.mLevelName[1]);

	if (levelPrefix != 'd' && levelPrefix != 'D')
	{
		if (static_cast<u32>(areaCode) >= '0' && static_cast<u32>(areaCode) <= '9')
		{
			i32 missionDigit = static_cast<i8>(G_SAVE_GAME.mLevelName[3]);
			areaCode += -'0';
			missionDigit -= '0';
			return missionDigit | (areaCode << 8);
		}

		if (static_cast<u32>(areaCode) >= 'A' && static_cast<u32>(areaCode) <= 'Z')
		{
			i32 missionDigit = static_cast<i8>(G_SAVE_GAME.mLevelName[3]);
			areaCode += -0x31;
			missionDigit -= '0';
			return missionDigit | (areaCode << 8);
		}

		if (static_cast<u32>(areaCode) >= 'a' && static_cast<u32>(areaCode) <= 'z')
		{
			i32 missionDigit = static_cast<i8>(G_SAVE_GAME.mLevelName[3]);
			areaCode += -0x51;
			missionDigit -= '0';
			return missionDigit | (areaCode << 8);
		}
	}
	else
	{
		areaCode = 0x99;
	}

	i32 missionDigit = static_cast<i8>(G_SAVE_GAME.mLevelName[3]);
	missionDigit -= '0';
	return missionDigit | (areaCode << 8);
}

// @Ok
// Functionally correct (verified logic against the disasm); previous session's residue
// note kept below since this session's bar is functional parity, not zero-diff.
// Residue: 1 mnemonic diff in the default (unrecognized node type) path. Original does
// `mov eax,[pos]; add eax,0xC`, ours does `mov ecx,[pos]; lea eax,[ecx+0xC]`, same result.
// Could not reproduce the lea in an isolated repro with the same switch shape; the real
// cause looks like whole-function register pressure. See trig.attempts.md.
u16* Trig_GetPosition(CVector* pos, i32 node)
{
	print_if_false(node >= 0 && node < NumNodes, "Bad node sent to Trig_GetPosition");

	u16* trigNodePtr = reinterpret_cast<u16*>(G_OFFSETLIST[node]);
	i32 trigNodeValue = *trigNodePtr;

	u8* pRaw;

	switch (trigNodeValue)
	{
		case 1:
		case 7:
			pRaw = SkipFlags(reinterpret_cast<u8*>(trigNodePtr) + 8 + trigNodePtr[3] * 2) + 3;
			break;
		case 5:
		case 20:
			pRaw = reinterpret_cast<u8*>(trigNodePtr) + 9 + trigNodePtr[2] * 2;
			break;
		case 500:
		case 501:
			pRaw = reinterpret_cast<u8*>(trigNodePtr) + 5;
			break;
		case 1002:
			trigNodePtr++;
			// fallthrough
		case 3:
		case 8:
		case 10:
		case 12:
		case 13:
		case 1000:
		case 1001:
			pRaw = reinterpret_cast<u8*>(trigNodePtr) + 7 + trigNodePtr[1] * 2;
			break;
		default:
			print_if_false(0, "Unrecognized node type in\n Trig_GetPosition");
			return reinterpret_cast<u16*>(reinterpret_cast<u32>(pos) + 0xC);
	}

	i32* pAligned = reinterpret_cast<i32*>(reinterpret_cast<u32>(pRaw) & ~3);

	pos->vx = pAligned[0] << 0xC;
	pos->vy = pAligned[1] << 0xC;
	pos->vz = pAligned[2] << 0xC;

	return reinterpret_cast<u16*>(pAligned + 3);
}

// @Ok
 u16* Trig_GetLinksPointer(int node)
{
	print_if_false(node >= 0 && node < NumNodes, "Bad node sent to Trig_GetLinksPointer");

	u16* trigNodePtr = reinterpret_cast<u16*>(G_OFFSETLIST[node]);
	i32 trigNodeValue = *reinterpret_cast<u16*>(trigNodePtr);

	if (trigNodeValue <= 0xD)
	{
		if (trigNodeValue < 0xC)
		{
			switch (trigNodeValue)
			{
				case 1:
					return reinterpret_cast<u16*>(trigNodePtr + 3);
				case 2:
				case 3:
				case 6:
				case 8:
				case 9:
				case 10:
					return reinterpret_cast<u16*>(trigNodePtr + 1);
				case 5:
					return reinterpret_cast<u16*>(trigNodePtr + 2);
				default:
					print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
					print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
					return 0;
			}
		}
		else
		{
			return reinterpret_cast<u16*>(trigNodePtr + 1);
		}
	}
	else if (trigNodeValue <= 0x3E9)
	{
		if (trigNodeValue < 0x3E8)
		{
			if (trigNodeValue != 0x14)
			{
				print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
				print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
				return 0;
			}

			return reinterpret_cast<u16*>(trigNodePtr + 2);
		}

		return reinterpret_cast<u16*>(trigNodePtr + 1);
	}
	else if (trigNodeValue != 0x3EA)
	{
		print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
		print_if_false(0, "Unrecognized node type in\n Trig_GetLinksPointer");
		return 0;
	}

	return reinterpret_cast<u16*>(trigNodePtr + 2);
}

// @Ok
void Trig_SendPulse(u16* pLinkInfo)
{
	u16 NumLinks = pLinkInfo[0];
	u16* pLink = &pLinkInfo[1];

	for (i32 i = 0; i < NumLinks; i++)
	{
		Trig_SendPulseToNode(pLink[i]);
	}
}

// @Ok
void Trig_SendSignalToLinks(u16* pLinkInfo)
{
	print_if_false(*pLinkInfo != 0, "Node sending signal is not linked\n to anything");

	u16 NumLinks = *pLinkInfo;
	u16 *pLink = pLinkInfo+1;

	for (i32 i = 0; i < NumLinks; i++)
	{
		u32 nodeIndex = pLink[i];
		switch (*G_OFFSETLIST[nodeIndex])
		{
			case 1:
			case 7:
				SendSignalToNode(BaddyList, nodeIndex);
				SendSignalToNode(ControlBaddyList, nodeIndex);
				SendSignalToNode(G_ENVIRONMENTAL_OBJECT_LIST, nodeIndex);
				break;
		}
	}
}

// @Ok
// @Matching
void Trig_ClearTrigMenu(void)
{
	for (i32 i = 0; i<40; i++)
	{
		G_MENUFILENAMEPOINTERS[i] = 0;
	}

	G_NUMTRIGMENUENTRIES = 0;
}

// @Ok
unsigned char* SkipFlags(unsigned char* pFlags)
{
	while(*pFlags != 0xFF)
		pFlags++;

	return pFlags+1;
}


// @Ok
// @Matching
void Trig_ResetCPCollisionFlags(void)
{
    spidey::compatibility::triggerCommandPoints().resetCollisionFlags();
}

// @Ok
// Scans a flag byte list terminated by 0xFF (same terminator convention as
// SkipFlags above). Bug fixed versus the previous draft: the loop never
// advanced pFlags, so a flag not present at the very first byte caused an
// infinite loop instead of scanning the rest of the list. Added the
// missing pFlags++ so it walks the list like SkipFlags does.
 u8 GetFlag(unsigned char flag, unsigned char *pFlags)
{
	while (*pFlags != 0xFF)
	{
		if (*pFlags == flag)
		{
			return 1;
		}
		pFlags++;
	}

	return 0;
}

// @Ok
void Trig_SendPulseToNode(i32 NodeIndex)
{
	ASSERT(NodeIndex >= 0 && NodeIndex < G_NUMNODES, "Bad node sent to Trig_SendPulseToNode");
	trigLog("\tSending pulse to node %i", NodeIndex);

	SCommandPoint *pCommand;
	switch(*G_OFFSETLIST[NodeIndex])
	{
		case 1:
		case 5:
		case 7:
		case 20:
			Trig_CreateObject(NodeIndex);
			break;
		case 6:
        {
			pCommand = GetCommandPoint(NodeIndex);
			ASSERT(pCommand != 0, "Sent pulse to command point node before command point was created");

			const bool queued = spidey::compatibility::triggerCommandPoints().receivePulse(*pCommand, static_cast<u16>(NodeIndex));
            ASSERT(queued, "Pending command list overflow, increase MAXPENDING in trig.cpp");

			break;
        }
		default:
			return;
	}
}

void validate_SLinkInfo(void)
{
	VALIDATE_SIZE(SLinkInfo, 0x10);

	VALIDATE(SLinkInfo, field_0, 0x0);
	VALIDATE(SLinkInfo, field_4, 0x4);
	VALIDATE(SLinkInfo, field_8, 0x8);
	VALIDATE(SLinkInfo, field_C, 0xC);
}

void validate_SCommandPoint(void)
{
	VALIDATE_SIZE(SCommandPoint, 0x18);


	VALIDATE(SCommandPoint, pCommands, 0x0);

	VALIDATE(SCommandPoint, Collision, 0x4);
	VALIDATE(SCommandPoint, Executed, 0x5);

	VALIDATE(SCommandPoint, NumPulsesSet, 0x6);
	VALIDATE(SCommandPoint, PulsesReceived, 0x7);
	VALIDATE(SCommandPoint, NumPulses, 0x8);
	VALIDATE(SCommandPoint, NodeIndex, 0xA);
	VALIDATE(SCommandPoint, Checksum, 0xC);

	VALIDATE(SCommandPoint, pNextSimilar, 0x10);
	VALIDATE(SCommandPoint, pNext, 0x14);
}

void validate_PendingListEntry(void)
{
	VALIDATE_SIZE(PendingListEntry, 0x8);

	VALIDATE(PendingListEntry, NodeIndex, 0x0);
	VALIDATE(PendingListEntry, field_2, 0x2);
	VALIDATE(PendingListEntry, pCommands, 0x4);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_trig(void)
{
	PATCH_PUSH_RET(0x004DE750, Trig_ClearTrigMenu);
	PATCH_PUSH_RET(0x004DE890, Trig_ResetCPCollisionFlags);
	PATCH_PUSH_RET(0x004DE8B0, Trig_ResetCPExecutedFlags);
	PATCH_PUSH_RET(0x004DE840, Trig_DeleteCommandPoints);
	PATCH_PUSH_RET(0x004DE8D0, Trig_TriggerCommandPoint);
	PATCH_PUSH_RET(0x004DE970, Trig_SetRestart);

	PATCH_PUSH_RET(0x004DEA20, Trig_ExecuteRestart);

	PATCH_PUSH_RET(0x004DEB10, Trig_DeleteTrigFile);
	PATCH_PUSH_RET(0x004DFC20, Trig_SendPulseToNode);
	PATCH_PUSH_RET(0x004DFD30, Trig_SendPulse);
	PATCH_PUSH_RET(0x004DFFB0, SendSignalToNode);
}
