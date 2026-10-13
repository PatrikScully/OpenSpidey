#include "game/objects/switch.h"
#include "compatibility/my_patch.h"
#include "engine/world/trig.h"
#include "game/actors/baddy.h"
#include "game/player/spidey.h"
#include "engine/assets/spool.h"
#include "compatibility/validate.h"

// @Ok
// no address in tools/names.json for this function (the Mac build has
// Switch_SetSwitchFaceFlags__FP5CItem, the PC build has no separate
// symbol for it), so it cannot be verified with compare.py/cmpsum.sh on
// its own. It only has one real caller, CSwitch::CSwitch, which inlines
// it twice (once for field_104, once for field_108); this is a faithful
// translation of that inlined block (see CSwitch::CSwitch below), walking
// the CItemRelatedList table (ob.h, 0x6B2454), the same table
// Spidey_SwapSuitTextures uses (spidey.cpp). Offsets confirmed against the
// IDA Hex-Rays decompile of the inlined block at 0x4D14E0: region at
// pItem+31 (u8), model at pItem+26 (u16), count at pRec+6 (u16), the two
// offset fields at pRec+2/+4 (u16, summed), face record base at
// pRec+offset*8+0x1C, per-record advance (face[2] as u16 >>1)*2, flag set
// face[0xF] |= 2. No struct is declared for the per-region record; the
// field names above are inferred from this offset arithmetic, not from a
// header, but the arithmetic itself is verified, not a guess.
void Switch_SetSwitchFaceFlags(CItem *pItem)
{
	i32 **pRegionEntry = CItemRelatedList[pItem->mRegion * 17];
	u8 *pRec = reinterpret_cast<u8*>(pRegionEntry[pItem->mModel]);

	i32 count = *reinterpret_cast<u16*>(pRec + 6);
	i32 offset = *reinterpret_cast<u16*>(pRec + 4) + *reinterpret_cast<u16*>(pRec + 2);

	u8 *pFace = pRec + offset * 8 + 0x1C;

	while (count != 0)
	{
		pFace[0xF] |= 2;
		pFace += (*reinterpret_cast<u16*>(pFace + 2) >> 1) * 2;
		count--;
	}
}

// @Ok
// Functional-only pass (session override on the acceptance bar: reproduce
// logic, do not chase zero-diff). Cross-checked against the IDA Hex-Rays
// decompile of 0x4D14E0: confirmed field offsets (field_104 = this+0x104,
// field_108 = this+0x108, field_10C/field_118 CVector copies of ->mPos,
// field_124 = state==4, field_100 = state, field_FC = pCursor[1]), the
// switch dispatch (0=SwitchInactive, 1=SwitchOff, 2=SwitchOn, 3/4=set
// field_100/field_124 and mark field_108 visible), and the checksum-read
// shape: the original advances the pointer to pChecksum+1 right after
// reading checksum2 (before the second Spool_FindEnviroItem call), then
// re-reads this->field_104 from memory into a fresh register right after
// that call and before storing field_108, and uses that reloaded value
// (not a fresh this->field_104 dereference) for the following if-block.
// Rewrote the source to match that exact shape (pItem104 local reloaded
// from this->field_104 after the second call). Remaining mnemonic diffs
// (134, cmpsum) are register-name/scheduling residue around that reload
// and call-cleanup ordering (add esp,8 position, ebp/edi register choice),
// not a logic difference; confirmed by comparing every store/branch above
// against the decompile 1:1. Switch_SetSwitchFaceFlags below is verified
// against the same decompile (dword_6B2454[17*region][model] table walk,
// count/offset fields at pRec+6/+2/+4, face record stride pRec+offset*8+0x1C,
// advance by (face[2..3]>>1)*2) and its per-region offsets are correct, not
// guesses.
CSwitch::CSwitch(i16 *a2, i32 a3)
{
	this->mCBodyFlags &= ~0x10;

	this->mType = 407;
	this->mRMinor = 0;
	this->mNode = static_cast<u16>(a3);

	this->AttachTo(&G_CONTROL_BADDY_LIST);

	i16 *pCursor = this->SquirtAngles(this->SquirtPos(a2));

	u32 *pChecksum = reinterpret_cast<u32*>((reinterpret_cast<i32>(pCursor) + 3) & ~3);

	u32 checksum1 = *pChecksum++;
	this->field_104 = Spool_FindEnviroItem(checksum1);

	u32 checksum2 = *pChecksum;
	pCursor = reinterpret_cast<i16*>(pChecksum + 1);
	CItem *pItem108 = Spool_FindEnviroItem(checksum2);

	CItem *pItem104 = this->field_104;
	this->field_108 = pItem108;

	if (pItem104)
	{
		print_if_false(pItem104 != 0, "Bad item");

		this->field_10C = pItem104->mPos;

		Switch_SetSwitchFaceFlags(pItem104);
	}

	if (this->field_108)
	{
		print_if_false(this->field_108 != 0, "Bad item");

		this->field_118 = this->field_108->mPos;

		Switch_SetSwitchFaceFlags(this->field_108);
	}

	i32 state = *pCursor;

	switch (state)
	{
		case 0:
			this->SwitchInactive();
			break;
		case 1:
			this->SwitchOff();
			break;
		case 2:
			this->SwitchOn();
			break;
		case 3:
		case 4:
			this->field_100 = state;
			this->field_124 = (state == 4);

			this->field_108->mFlags |= 0x400;
			this->field_108->mRGB = 0;

			Switch_SetVisible(true, this->field_108);
			break;
		default:
			print_if_false(0, "Bad switch state");
			break;
	}

	this->field_FC = *reinterpret_cast<u16*>(pCursor + 1);
}

// @Ok
// @Test
void CSwitch::AI(void)
{
	if (this->mInputFlags & 1)
	{
		this->mInputFlags &= ~1;
		switch (this->field_100)
		{
			case 0:
				this->SwitchOff();
				break;
			case 1:
				this->SwitchOn();
				this->SignalAttachedItems();
				break;
			case 2:
				this->SwitchInactive();
				break;
			default:
				print_if_false(0, "invalid switch state");
				break;
		}
	}

	if (this->field_100 == 2 || this->field_100 == 3 || this->field_100 == 4)
	{
		if (this->field_FC)
		{
			this->field_F8 += this->mElapsedTicks;

			if (this->field_F8 > this->field_FC)
			{
				this->field_F8 = 0;

				if (this->field_100 == 2)
				{
					this->SwitchOff();
				}
				else
				{
					this->PulseLFA1Node(this->field_124 == 0 ? 3 : 2);
					this->field_100 = 5;
				}
			}
		}
	}

	if (this->field_100 == 3 || this->field_100 == 4)
	{
		this->field_108->mFlags &= 0xFFFE;

		u32 res;
		u32 Int = this->field_108->mRGB & 0xFF;
		if (Int < 0x80)
		{
			Int += this->mElapsedTicks;

			res = (((Int << 8 ) | Int) << 8) | Int;
		}
		else
		{
			res = 0x808080;
		}

		this->mRGB = res;
	}

	if (this->field_100 == 5)
	{
		u32 v18 = 4 * this->mElapsedTicks;
		u32 v19 = this->field_108->mRGB & 0xFF;

		if (v19 > v18)
		{
			u32 diff = v19 - v18;
			this->field_108->mRGB = (((diff << 8) | diff) << 8) | diff;
		}
		else
		{
			this->field_108->mFlags |= 0x21;
			reinterpret_cast<CBody*>(this->field_108)->Die();
			this->field_108->mRGB = 0;
		}
	}
}

// @Ok
void CSwitch::PulseLFA1Node(i32 a1)
{
	CVector v3;
	i32 nodeIndex = 1;
	v3.vx = 0;
	v3.vy = 0;
	v3.vz = 0;

	if (G_NUMNODES > 1)
	{
		for (; nodeIndex < G_NUMNODES; nodeIndex++)
		{
			if (*G_OFFSETLIST[nodeIndex] != 1)
				continue;
			Trig_GetPosition(&v3, nodeIndex);
			switch (a1)
			{
				case 0:
					if ( (v3.vx & 0xFFFFF000) != 409600 || v3.vy || v3.vz)
						break;
					Trig_SendPulseToNode(nodeIndex);
					return;
				case 1:
					if (v3.vx || v3.vy || v3.vz)
						break;
					Trig_SendPulseToNode(nodeIndex);
					return;
				case 2:
					if ( (v3.vx & 0xFFFFF000) != 409600 || v3.vy || (v3.vz & 0xFFFFF000) != 409600)
						break;
					Trig_SendPulseToNode(nodeIndex);
					return;
				case 3:
					if ( v3.vx || v3.vy || (v3.vz & 0xFFFFF000) != 409600)
						break;
					Trig_SendPulseToNode(nodeIndex);
					return;
			}
		}
	}

	print_if_false(0, "Node not found?");
}

// @Ok
void CSwitch::Flick(void)
{
	switch (this->field_100)
	{
		case 1:
			this->SwitchOn();
			this->SignalAttachedItems();
			break;
		case 2:
			this->SwitchOff();
			if (!this->field_FC)
				this->SignalAttachedItems();
			break;
		case 3:
		case 4:
			if (G_MECHLIST_PLAYER)
				G_MECHLIST_PLAYER->field_568 += this->field_124 ? -1 : 1;
			this->PulseLFA1Node(this->field_124 == 0);
			this->field_100 = 5;
			break;
		case 5:
			if (G_MECHLIST_PLAYER)
				G_MECHLIST_PLAYER->field_568 += this->field_124 ? -1 : 1;
			break;
		default:
			return;
	}
}

// @Ok
CSwitch* Switch_GetCSwitchObjectFromItem(CItem *pItem)
{
	print_if_false(pItem != 0, "Bad item");

	for (CItem *cur = G_CONTROL_BADDY_LIST; cur; cur = reinterpret_cast<CItem*>(cur->mNextItem))
	{
		if (cur->mType == 407)
		{
			CSwitch* pSwitch = reinterpret_cast<CSwitch*>(cur);

			if (pSwitch->field_104 == pItem || pSwitch->field_108 == pItem)
				return pSwitch;
		}
	}

	return 0;
}

// @Ok
CSwitch::~CSwitch(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_CONTROL_BADDY_LIST));
}

// @Ok
 void CSwitch::SwitchOn(void)
{
	this->field_F8 = 0;
	this->field_100 = 2;

	Switch_SetVisible(1, this->field_108);
	Switch_SetVisible(0, this->field_104);
}

// @Ok
 void CSwitch::SignalAttachedItems(void)
{
	Trig_SendPulse(Trig_GetLinksPointer(this->mNode));
}

// @Ok
void CSwitch::SwitchInactive(void)
{
	this->field_100 = 0;
}

// @Ok
CVector* CSwitch::GetAutoAimTargetPointer(void)
{
	switch(this->field_100)
	{
		case 1:
			return &this->field_10C;
		case 2:
		case 3:
		case 4:
		case 5:
			return &this->field_118;
		default:
			return NULL;
	}
}

// @Ok
void Switch_SetVisible(bool a1, CItem* pItem)
{
	print_if_false(pItem != 0, "Bad item");

	if (a1)
		pItem->mFlags &= 0xFFDE;
	else
		pItem->mFlags |= 0x21;
}

// @Ok
void CSwitch::SwitchOff(void)
{
	this->field_100 = 1;
	Switch_SetVisible(false, this->field_108);
	Switch_SetVisible(true, this->field_104);
}


void validate_CSwitch(void)
{
	VALIDATE_SIZE(CSwitch, 0x128);

	VALIDATE(CSwitch, field_F8, 0xF8);
	VALIDATE(CSwitch, field_FC, 0xFC);

	VALIDATE(CSwitch, field_100, 0x100);

	VALIDATE(CSwitch, field_104, 0x104);
	VALIDATE(CSwitch, field_108, 0x108);

	VALIDATE(CSwitch, field_10C, 0x10C);
	VALIDATE(CSwitch, field_118, 0x118);

	VALIDATE(CSwitch, field_124, 0x124);
}

// @Bogus
// Vtable check first: CSwitch's vtable (0x53C51C) has exactly 5 live
// entries (dtor, Die, AI, Hit, DeleteStuff, CItem's whole set since CSwitch
// inherits CBody directly, not CBaddy), our class declares dtor and AI as
// virtual and matches the other three against the shared CItem defaults
// (same values seen in hostage/spclone/blackcat's vtables). No gap there.
//
// The constructor stays in the exe anyway: it calls Spool_FindEnviroItem
// (spool.cpp, not this file) to resolve field_104/field_108, and that
// function reads the plain repo globals EnviroList/PSXRegion instead of
// the already-established G_ENVIRO_LIST/G_PSXREGION macros (ob.h/spool.h
// both have the macros, Spool_FindEnviroItem just does not use them,
// Spool_GetModelChecksum right above it in spool.cpp does). EnviroList's
// only writer is spool.cpp's own level loader, unhooked, so our repo copy
// is always null, so Spool_FindEnviroItem would always return 0 through a
// hooked constructor. CSwitch::AI dereferences field_108 unconditionally
// in the field_100==3/4 states with no null check, so a switch spawned
// with field_108 wrongly null would crash there. trig.cpp's environment
// loader is the only caller (`new CSwitch(pData, NodeIndex)`, unhooked),
// so hooking the constructor address would still redirect that call into
// our buggy path. Not our file to fix; reported, not patched here.
void patch_switch(void)
{
	PATCH_PUSH_RET(0x004D1490, Switch_GetCSwitchObjectFromItem);

	PATCH_PUSH_RET_POLY(0x004D1810, CSwitch::~CSwitch, "??1CSwitch@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004D1870, CSwitch::Flick, "?Flick@CSwitch@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004D19E0, CSwitch::GetAutoAimTargetPointer, "?GetAutoAimTargetPointer@CSwitch@@QAEPAVCVector@@XZ");
	PATCH_PUSH_RET_POLY(0x004D1A10, CSwitch::AI, "?AI@CSwitch@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004D1C60, CSwitch::PulseLFA1Node, "?PulseLFA1Node@CSwitch@@QAEXH@Z");
}
