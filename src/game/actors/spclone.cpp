#include "engine/world/actor_motion_integrator.h"
#include "game/cinematics/actor_script_controller.h"
#include "game/cinematics/script_command_list.h"
#include "game/actors/spclone.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/trig.h"
#include "core/utils.h"
#include "engine/audio/ps2redbook.h"
#include "engine/world/collision.h"
#include "engine/rendering/scene_renderer.h"

EXPORT SLight M3d_SpCloneLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
  0,

  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,
  { 1200, 1200, 960 }
};


// @Ok
// @Matching
void SpClone_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = SpClone_RelocatableModuleClear;
	pMod->field_C[0] = SpClone_CreateSpClone;
}

// @Ok
// Same overall shape as CBlackCat::AI (blackcat.cpp), reverse engineered
// from IDA decompile+disasm of 0x4AFF30 (1056 bytes). field_31C.bothFlags
// picks the outer phase (1 = fall/land on a ledge below, 2 = walk the
// SynthesizeAnalogueInput script), dumbAssPad is the sub state inside each
// phase. mLookTargetNode is a trig link id to look at (same role as CBlackCat's
// own mLookTargetNode), mScriptRunning is the "script driving me" outer flag
// SynthesizeAnalogueInput clears when it runs out of work.
//
// The look-at-trig block (M3dUtils_GetDynamicHookPosition with
// SHook{Part=0,Offset=7} + M3dMaths_TransposeMatrix1 + gte_ldlvl/rtir/stlvnl
// + CVector::operator<<=) was verified instruction by instruction against
// the raw disasm, not just the Hex-Rays pseudocode: Hex-Rays mislabels the
// gte_ldlvl/gte_rtir/gte_stlvnl triplet as
// qt_register_signal_spy_callbacks/sub_46DA40/sub_46D790 (same decompiler
// symbol-matching artifact already documented in CBlackCat::AI's comment),
// and mislabels CVector::operator<<=(const int&) (0x4E7680) as a plain
// function taking &local instead of a thiscall on &delta; the disasm shows
// ecx = &delta is still live at the call, matching the CVector member call.
//
// Known original defect kept for fidelity (per CLAUDE.md: reproduce the
// source-level bug, don't fix it): the pan/tilt block that steers
// mpJoints+0x24/+0x26 runs unconditionally even when mpJoints is null (the
// disasm only guards the ApplyPose(gUnkPose) call with the mpJoints==0
// check, not the pan/tilt math after it), unlike CBlackCat::AI which wraps
// the whole block in "if (this->mpJoints)".
//
// Functional-only per this session's bar: field offsets, opcode/constant
// values (RunAnim ids, magnitude 8, CycleAnim(21,1), landing anim 203) and
// call targets all checked against the disasm; not expected to byte-match.
void CSpClone::AI(void)
{
	if (this->pMessage)
	{
		this->CleanUpMessages(1, 0);
	}

	if (G_SUBMARINER_DIE_RELATED)
	{
		this->Die(0);
		return;
	}

	this->mPhysicsStepScale = 1365 * (this->mElapsedTicks + this->mOlderElapsedTicks + this->mPreviousElapsedTicks);
	this->DoPhysics();

	switch (this->field_31C.bothFlags)
	{
		case 1:
			switch (this->dumbAssPad)
			{
				case 0:
				{
					CVector aimTarget;
					aimTarget.vx = G_MECHLIST->mPos.vx;
					aimTarget.vy = this->mPos.vy;
					aimTarget.vz = G_MECHLIST->mPos.vz;
					Utils_CalcAim(&this->mAngles, &this->mPos, &aimTarget);

					i32 groundHeight = Utils_GetGroundHeight(&this->mPos, 0, 0x800, 0);
					if (groundHeight != -1)
					{
						this->mGravityAcceleration = 0x2000;
						print_if_false(1, "Bad register index");
						this->realRegisterArr[0] = groundHeight >> 12;
						this->dumbAssPad++;
					}
					break;
				}

				case 1:
				{
					print_if_false(1, "Bad register index");
					i32 landingY = (this->realRegisterArr[0] - this->field_21E) << 12;
					if (this->mPos.vy > landingY)
					{
						this->RunAnim(203, 0, -1);
						this->mPos.vy = landingY;
						this->mGravityAcceleration = 0;
						this->mVel.vx = 0;
						this->mVel.vy = 0;
						this->mVel.vz = 0;
						this->dumbAssPad++;
					}
					break;
				}

				case 2:
					if (this->mAnimFinished)
					{
						this->field_31C.bothFlags = 2;
						this->dumbAssPad = 0;
					}
					break;

				default:
					print_if_false(0, "Unknown substate");
					break;
			}
			break;

		case 2:
			switch (this->dumbAssPad)
			{
				case 0:
					this->mScriptRunning = 1;
					this->mScriptBytecodeActive = 1;
					this->dumbAssPad = 1;
					// fall through

				case 1:
					if (this->mScriptRunning)
					{
						this->SynthesizeAnalogueInput();
					}
					else
					{
						this->Die(0);
					}
					break;

				default:
					print_if_false(0, "Unknown substate");
					break;
			}
			break;

		default:
			print_if_false(0, "Unknown state");
			break;
	}

	this->mOlderElapsedTicks = this->mPreviousElapsedTicks;
	this->mPreviousElapsedTicks = this->mElapsedTicks;

	CSVector lookAngle;
	lookAngle.vx = 0;
	lookAngle.vy = 0;
	lookAngle.vz = 0;

	if (this->mLookTargetNode)
	{
		SHook hook;
		hook.Part.vx = 0;
		hook.Part.vy = 0;
		hook.Part.vz = 0;
		hook.Offset = 7;
		VECTOR hookPos;
		M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

		CVector trigPos;
		trigPos.vx = 0;
		trigPos.vy = 0;
		trigPos.vz = 0;
		Trig_GetPosition(&trigPos, this->mLookTargetNode);

		MATRIX localMat;
		M3dMaths_TransposeMatrix1(&this->mTransform, &localMat);
		gte_SetRotMatrix(&localMat);

		CVector delta;
		delta.vx = (trigPos.vx - hookPos.vx) >> 12;
		delta.vy = (trigPos.vy - hookPos.vy) >> 12;
		delta.vz = (trigPos.vz - hookPos.vz) >> 12;

		gte_ldlvl(reinterpret_cast<VECTOR*>(&delta));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&delta));

		delta <<= 12;

		CVector zero;
		zero.vx = 0;
		zero.vy = 0;
		zero.vz = 0;
		Utils_CalcAim(&lookAngle, &zero, &delta);
	}

	if (!this->mpJoints)
	{
		this->ApplyPose(G_UNK_POSE);
	}

	i16* panTilt = reinterpret_cast<i16*>(this->mpJoints) + 0x12;

	i32 dx = lookAngle.vx - panTilt[0];
	if (dx > 0x800) dx -= 0x1000;
	else if (dx < -0x800) dx += 0x1000;

	if (dx != 0)
	{
		if (dx > 0x30) dx = 0x30;
		else if (dx < -0x30) dx = -0x30;
		panTilt[0] = static_cast<i16>(panTilt[0] + dx);
	}

	i32 dy = lookAngle.vy - panTilt[1];
	if (dy > 0x800) dy -= 0x1000;
	else if (dy < -0x800) dy += 0x1000;

	if (dy != 0)
	{
		if (dy > 0x30) dy = 0x30;
		else if (dy < -0x30) dy = -0x30;
		panTilt[1] = static_cast<i16>(panTilt[1] + dy);
	}

	if ((this->mFlags & 4) && this->mScriptRunning && (panTilt[0] | panTilt[1]) == 0)
	{
		this->mFlags &= ~4;
	}

	if (this->mFlags & 4)
	{
		this->ApplyPose(G_UNK_POSE);
	}
	else
	{
		M3d_BuildTransform(this);
	}

	this->DoMGSShadow();
}

// @Ok
// @Matching
CSpClone::CSpClone(i16 * a2,i32 a3)
{
	this->InitItem("spidey");
	this->field_194 &= 0xFFFFFFDF;
	this->field_194 |= 0x40u;
	this->field_194 &= 0xFFFFFBFF;
	this->field_194 |= 0x800u;

	i16 *v5 = this->SquirtAngles(this->SquirtPos(a2));

	this->ShadowOn();
	this->mShadowScale = 48;
	this->field_21E = 100;
	this->mPreviousElapsedTicks = 2;
	this->mOlderElapsedTicks = 2;

	this->RunAnim(0xCAu, 0, -1);
	this->mFlags |= 0x480u;

	this->mpLight = &M3d_SpCloneLight;

	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mType = 327;
	this->field_31C.bothFlags = 1;
	this->mNode = a3;
	this->mRMinor = 0;
	this->mScriptCursorAddress = reinterpret_cast<i32>(v5);

	if ( G_SUBMARINER_DIE_RELATED )
		this->Die(0);
}

// @Ok
// Same MGS-shadow idiom as CBlackCat::DoMGSShadow (blackcat.cpp) and CCarnage::DoMGSShadow
// (carnage.cpp): 4 hook positions rotated into local (body) space give an X/Z footprint box,
// then a vertical offset gives the world space shadow center, applied to a lazily-created
// CQuadBit (field_338). Hook offsets here are 0xE, 0x11, 0xB, 6. Unlike the other two, the
// height offset is rotated TWICE (once by the transposed body matrix, once by the body
// matrix directly) then shifted left 12, and the Y of each corner comes from
// realRegisterArr[0] << 12, not a plain field. Residue (not chased, per this session's
// functional-only bar): the original has an SEH frame at entry (mov eax,fs:[0]; push -1;
// push handler; ...) that this source does not produce, the same unresolved issue documented
// in CBlackCat::DoMGSShadow's comment. See ~/Documents/spidey-work/wt/spclone.attempts.md for
// the attempt log (177 diffs, first divergence at the entry SEH frame only, semantic body
// verified against the disasm beyond that point).
void CSpClone::DoMGSShadow(void)
{
	SHook hook;
	VECTOR pos0, pos1, pos2, pos3;

	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;
	hook.Offset = 0xE;
	M3dUtils_GetDynamicHookPosition(&pos0, this, &hook);

	hook.Offset = 0x11;
	M3dUtils_GetDynamicHookPosition(&pos1, this, &hook);

	hook.Offset = 0xB;
	M3dUtils_GetDynamicHookPosition(&pos2, this, &hook);

	hook.Offset = 6;
	M3dUtils_GetDynamicHookPosition(&pos3, this, &hook);

	i32 height = this->field_21E << 12;

	CVector v0 = *reinterpret_cast<CVector*>(&pos0);
	v0 -= this->mPos;
	CVector v1 = *reinterpret_cast<CVector*>(&pos1);
	v1 -= this->mPos;
	CVector v2 = *reinterpret_cast<CVector*>(&pos2);
	v2 -= this->mPos;
	CVector v3 = *reinterpret_cast<CVector*>(&pos3);
	v3 -= this->mPos;

	CVector heightOffset;
	heightOffset.vx = 0;
	heightOffset.vy = height;
	heightOffset.vz = 0;

	MATRIX localMat;
	M3dMaths_TransposeMatrix1(&this->mTransform, &localMat);
	gte_SetRotMatrix(&localMat);

	CVector box[4] = { v0, v1, v2, v3 };

	i32 maxX = 0x20;
	i32 minX = box[0].vx;
	i32 maxZ = box[0].vz;
	i32 minZ = box[0].vz;
	i32 i;

	for (i = 0; i < 4; i++)
	{
		box[i] >>= 12;
		gte_ldlvl(reinterpret_cast<VECTOR*>(&box[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&box[i]));

		if (box[i].vx > maxX)
		{
			maxX = box[i].vx;
		}
		else if (box[i].vx < minX)
		{
			minX = box[i].vx;
		}

		if (box[i].vz > maxZ)
		{
			maxZ = box[i].vz;
		}
		else if (box[i].vz < minZ)
		{
			minZ = box[i].vz;
		}
	}

	heightOffset >>= 12;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&heightOffset));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&heightOffset));

	print_if_false(
		maxX - minX < 0x40 && maxZ - minZ < 0x40,
		"MGS shadow box too big");

	gte_SetRotMatrix(&this->mTransform);

	i32 ry = this->realRegisterArr[0] << 12;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&heightOffset));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&heightOffset));

	heightOffset <<= 12;

	CVector corners[4];
	for (i = 0; i < 4; i++)
	{
		corners[i].vx = this->mPos.vx + heightOffset.vx;
		corners[i].vy = ry;
		corners[i].vz = this->mPos.vz + heightOffset.vz;
	}

	if (!this->field_338)
	{
		G_TOTALBITUSAGE = 0;
		this->field_338 = new CQuadBit();
		G_TOTALBITUSAGE = -1;

		this->field_338->SetTexture(0, 0);
	}

	this->field_338->mFrigDeltaZ = 0x20;
	this->field_338->SetTransparency(0x40);
	this->field_338->SetSubtractiveTransparency();
	this->field_338->SetCorners(corners[0], corners[1], corners[2], corners[3]);
}

// @Ok

i32* CSpClone::GetNewCommandBlock(u32 wordCount) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    return commands.allocate(wordCount);
}

// @Ok
// @Matching

void CSpClone::KillCommandBlockByID(i32 type) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    commands.removeByType(type);
}

// Original actor entry point; shared bytecode behavior lives in ActorScriptController.
void CSpClone::SynthesizeAnalogueInput(void) {
    spidey::cinematics::ActorScriptState state = {mScriptElapsedTicks,
        mScriptBytecodeActive, mScriptCursorAddress, mScriptCommandHead,
        mScriptRunning, mLookTargetNode, nullptr};
    spidey::cinematics::ActorScriptController controller(*this, state,
        spidey::cinematics::ActorScriptProfile::spiderClone(),
        spidey::cinematics::scriptCommandAllocator());
    controller.update();
}

// @Ok
CSpClone::~CSpClone(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	delete this->field_338;

	this->KillAllCommandBlocks();
}

// @Ok
// @Matching
void SpClone_CreateSpClone(const u32 * a2,u32 * a3)
{
	i16* v2 = reinterpret_cast<i16*>(a2[0]);
	i32 v3 = static_cast<i32>(a2[1]);

	*a3 = reinterpret_cast<u32>(new CSpClone(v2, v3));
}

// @Ok
// @Matching
void SpClone_RelocatableModuleClear(void)
{
	for (CBody* cur = G_BADDY_LIST; cur; )
	{
		CBody* next = reinterpret_cast<CBody*>(cur->mNextItem);
		if (cur->mType == 327)
		{
			delete cur;
		}

		cur = next;
	}
}

// @Ok
void CSpClone::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// Original physics entry point; integration and integer rules live in ActorMotionIntegrator.
void CSpClone::DoPhysics(void) {
    spidey::world::ActorMotionState state = {mPos, mVel, mAcc, mAngles,
        mAngVel, mAngAcc, mAngFric};
    spidey::world::ActorMotionIntegrator(state).advanceFixedStep(mPhysicsStepScale, mGravityAcceleration);
}

// @Ok
// @Matching

i32* CSpClone::KillCommandBlock(i32* command) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    return commands.remove(command);
}

// @Ok
// No original address to compare against: this has no xrefs in IDA outside
// the destructor, and the original inlines this loop there (same situation
// as CBlackCat::KillCommandBlockByID / CBlackCat::~CBlackCat's inlined
// KillAllCommandBlocks, blackcat.cpp). KillCommandBlock always finds
// this->mScriptCommandHead == cur (cur starts at the head and KillCommandBlock keeps
// mScriptCommandHead pointed at the head after every delete), so this walks and
// frees every block exactly once in list order. The trailing
// mScriptCommandHead = 0 is a harmless no-op once the loop empties the list (the
// last KillCommandBlock call already leaves it 0), kept for clarity.

void CSpClone::KillAllCommandBlocks(void) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    commands.clear();
}

void validate_CSpClone(void){
	VALIDATE_SIZE(CSpClone, 0x350);

	VALIDATE(CSpClone, mLookTargetNode, 0x324);

	VALIDATE(CSpClone, mGravityAcceleration, 0x328);
	VALIDATE(CSpClone, mPreviousElapsedTicks, 0x32C);
	VALIDATE(CSpClone, mOlderElapsedTicks, 0x330);
	VALIDATE(CSpClone, mPhysicsStepScale, 0x334);
	VALIDATE(CSpClone, field_338, 0x338);

	VALIDATE(CSpClone, mScriptRunning, 0x33C);

	VALIDATE(CSpClone, mScriptElapsedTicks, 0x340);
	VALIDATE(CSpClone, mScriptBytecodeActive, 0x344);

	VALIDATE(CSpClone, mScriptCursorAddress, 0x348);

	VALIDATE(CSpClone, mScriptCommandHead, 0x34C);
}

// @Bogus
// hostageVtable-style check done first: CSpClone's vtable (0x53C158) has 17
// live entries (0-16, entry 17 decodes as float garbage, same cutoff shape
// as CBlackCat's 0x53B448). Slot 0 is the dtor, slot 2 is AI, slot 11 is
// Shouldnt_DoPhysics_Be_Virtual (compiled as a jmp-thunk straight into
// DoPhysics, named j_CSpClone_DoPhysics by IDA, same shape as CBlackCat's
// slot 11), everything else matches the shared CBaddy/CItem default stubs
// byte for byte against CBlackCat's own vtable. Our class declares every
// one of those overrides, so unlike hostage.cpp there is no gap: the
// constructor and destructor are safe to hook here.
// GetNewCommandBlock, KillCommandBlockByID and KillAllCommandBlocks have no
// standalone address, the original inlines them at their call sites same as
// CBlackCat's equivalents.
void patch_spclone(void)
{
	PATCH_PUSH_RET(0x004AFC70, SpClone_RelocatableModuleInit);
	PATCH_PUSH_RET(0x004AFC90, SpClone_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004AFCC0, SpClone_CreateSpClone);

	PATCH_PUSH_RET_POLY(0x004AFD60, CSpClone::CSpClone, "??0CSpClone@@QAE@PAFH@Z");
	PATCH_PUSH_RET_POLY(0x004AFE70, CSpClone::~CSpClone, "??1CSpClone@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004AFF30, CSpClone::AI, "?AI@CSpClone@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004B0350, CSpClone::KillCommandBlock, "?KillCommandBlock@CSpClone@@QAEPAHPAH@Z");
	PATCH_PUSH_RET_POLY(0x004B03C0, CSpClone::SynthesizeAnalogueInput, "?SynthesizeAnalogueInput@CSpClone@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004B0D70, CSpClone::Shouldnt_DoPhysics_Be_Virtual, "?Shouldnt_DoPhysics_Be_Virtual@CSpClone@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004B0D80, CSpClone::DoPhysics, "?DoPhysics@CSpClone@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004B0EF0, CSpClone::DoMGSShadow, "?DoMGSShadow@CSpClone@@QAEXXZ");
}
