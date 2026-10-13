#include "engine/world/actor_motion_integrator.h"
#include "game/cinematics/actor_script_controller.h"
#include "game/cinematics/script_command_list.h"
#include "game/actors/blackcat.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "engine/rendering/m3dutils.h"
#include "core/utils.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/world/collision.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/audio/ps2redbook.h"
#include "game/player/spidey.h"

// The "player died in the submarine level" byte, written only by the exe
// side (CPlayer::AI at 0x4C66A6, CPlayer_CutSceneSkipCleanup at 0x4BC17E),
// so it has to be read out of game memory. Same fixed pointer baddy.cpp,
// venom.cpp and spidey.cpp already use for it.
static u8 * const gSubmarinerDieRelated = (u8*)0x0060CFC4;

EXPORT SLight M3d_BlackCatLight =
{
  { { -2047, -2896, -2047 }, { 0, 0, 4096 }, { 0, 0, -4096 } },
  0,
  { { 1520, 2000, 1840 }, { 1440, 1920, 1760 }, { 1440, 1920, 1760 } },
  0,
  { 1760, 1600, 1600 }
};


// @Ok
// @Matching
void BlackCat_RelocatableModuleInit(reloc_mod* pMod)
{
	pMod->mClearFunc = BlackCat_RelocatableModuleClear;
	pMod->field_C[0] = BlackCat_CreateBlackCat;
}

// @Ok
void BlackCat_RelocatableModuleClear(void)
{
	for (CBody* cur = G_BADDY_LIST; cur; )
	{
		CBody* next = reinterpret_cast<CBody*>(cur->mNextItem);
		if (cur->mType == 319)
		{
			delete cur;
		}

		cur = next;
	}
}

// @Ok
// 0x413B60. The head hook is local; Black Cat has no hook table.
void CBlackCat::AI(void)
{
	if (*gSubmarinerDieRelated)
	{
		if (Trig_GetLevelID() != 0x803)
		{
			this->Die(0);
			return;
		}
	}

	if (this->mAnim == 8)
	{
		if (!(this->field_218 & 1) && this->mFrame >= 0x12)
		{
			SFX_PlayPos(0x819B, &this->mPos, 0);
			this->field_218 |= 1;
		}
	}
	else
	{
		this->field_218 &= ~1;
	}

	this->DoPhysics();

	if (this->pMessage)
	{
		this->CleanUpMessages(1, 0);
	}

	switch (this->field_31C.bothFlags)
	{
		case 1:
			switch (this->dumbAssPad)
			{
				case 0:
				{
					CVector aimTarget;
					aimTarget.vx = G_MECHLIST_PLAYER->mPos.vx;
					aimTarget.vy = this->mPos.vy;
					aimTarget.vz = G_MECHLIST_PLAYER->mPos.vz;
					Utils_CalcAim(&this->mAngles, &this->mPos, &aimTarget);

					i32 groundHeight = Utils_GetGroundHeight(&this->mPos, 0, 0x800, 0);
					if (groundHeight != -1)
					{
						this->mGravityAcceleration = 0x2000;
						this->realRegisterArr[0] = groundHeight >> 12;
						this->dumbAssPad++;
					}
					break;
				}
				case 1:
				{
					print_if_false(1, "CBlackCat::AI climb down");
					i32 landingY = (this->realRegisterArr[0] - this->field_21E) << 12;
					if (this->mPos.vy > landingY)
					{
						SFX_PlayPos(0x819A, &this->mPos, 0);
						this->RunAnim(7, 0, -1);
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
					print_if_false(0, "CBlackCat::AI bad sub state (1)");
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
						if (Trig_GetLevelID() == 0x803)
						{
							this->field_31C.bothFlags = 4;
							this->dumbAssPad = 0;
						}
						else
						{
							this->Die(0);
						}
					}
					break;

				default:
					print_if_false(0, "CBlackCat::AI bad sub state (2)");
					break;
			}
			break;

		case 4:
			if (this->mAnim != 0 || this->mAnimFinished != 0)
			{
				this->RunAnim(0, 0, -1);
			}
			break;

		default:
			print_if_false(0, "CBlackCat::AI bad state");
			break;
	}

	CSVector lookAngle;

	if (this->mLookTargetNode)
	{
		CVector hookPos;
		SHook hook;
		hook.Part.vx = 0;
		hook.Part.vy = 0;
		hook.Part.vz = 0;
		hook.Offset = 16;
		M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&hookPos), this, &hook);

		CVector trigPos;
		Trig_GetPosition(&trigPos, this->mLookTargetNode);

		MATRIX localMat;
		M3dMaths_TransposeMatrix1(&this->mTransform, &localMat);
		gte_SetRotMatrix(&localMat);

		trigPos.vx = (trigPos.vx - hookPos.vx) >> 12;
		trigPos.vz = (trigPos.vz - hookPos.vz) >> 12;
		trigPos.vy = (trigPos.vy - hookPos.vy) >> 12;
		gte_ldlvl(reinterpret_cast<VECTOR*>(&trigPos));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&trigPos));

		CVector zero;
		trigPos <<= 12;
		Utils_CalcAim(&lookAngle, &zero, &trigPos);
	}

	if (this->mpJoints)
	{
		// @FIXME guess: mpJoints points at a turret-style head/eye object
		// with a pan/tilt angle pair at offset 0x24
		i16 *panTilt = reinterpret_cast<i16*>(this->mpJoints) + 0x12;

		i32 dx = lookAngle.vx - panTilt[0];
		if (dx > 0x800) dx -= 0x1000;
		else if (dx < -0x800) dx += 0x1000;

		if (dx > 0x30)
		{
			dx = 0x30;
		}
		else if (dx < -0x30)
		{
			dx = -0x30;
		}
		panTilt[0] = static_cast<i16>(panTilt[0] + dx);

		i32 dy = lookAngle.vy - panTilt[1];
		if (dy > 0x800) dy -= 0x1000;
		else if (dy < -0x800) dy += 0x1000;

		if (dy > 0x30)
		{
			dy = 0x30;
		}
		else if (dy < -0x30)
		{
			dy = -0x30;
		}
		panTilt[1] = static_cast<i16>(panTilt[1] + dy);

		if ((this->mFlags & 4) && this->mScriptRunning)
		{
			if ((panTilt[0] | panTilt[1]) == 0)
			{
				this->mFlags &= ~4;
			}
		}
	}

	if (this->mFlags & 4)
	{
		this->ApplyPose(reinterpret_cast<i16*>(0x00548660));
	}
	else
	{
		M3d_BuildTransform(this);
	}

	this->DoMGSShadow();

}

// @Ok
// 4 leg/paw hook positions rotated into local (body) space via the
// transposed body matrix give an X/Z footprint box (floor -0x20/0x20 on X,
// -0x40/0x40 on Z), then the box corners are rotated back to world space and
// offset by mPos to give the shadow quad. heightOffset is computed and
// GTE-rotated but never read again, dead in the original too, kept for
// fidelity. verified against IDA decompile/disasm of 0x414c50; matches the
// already-fixed CCarnage::DoMGSShadow (carnage.cpp) and CSpClone's version
// (spclone.cpp) closely, same family of function. Found and fixed 3 real
// bugs versus the previous draft: (1) the box min/max init used box[0]
// instead of the fixed floor constants, (2) the corners were a degenerate
// single point (mPos+heightOffset repeated 4 times, no rotate) instead of
// the real rotated bounding-box quad, (3) mFrigDeltaZ was set on every call
// instead of only at CQuadBit creation time. cmpsum shows ~180 mnemonic
// diffs, but the fixed box constants (0x20/-0x20/0x40/-0x40), the 4 hook
// offsets (3/6/13/9) and the min/max compare shape all show up byte for
// byte at the right spots on both sides; the diffs are register/stack
// layout residue from the original's SEH frame (this function does a
// `new CQuadBit()`, see the `new T()` SEH note in CLAUDE.md) which our
// build does not reproduce, not a logic difference. Per this session's
// functional-only bar this is left @Ok without chasing a byte match.
void CBlackCat::DoMGSShadow(void)
{
	SHook hook;
	VECTOR pos0, pos1, pos2, pos3;

	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;
	hook.Offset = 3;
	M3dUtils_GetDynamicHookPosition(&pos0, this, &hook);

	hook.Offset = 6;
	M3dUtils_GetDynamicHookPosition(&pos1, this, &hook);

	hook.Offset = 13;
	M3dUtils_GetDynamicHookPosition(&pos2, this, &hook);

	hook.Offset = 9;
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

	MATRIX localMat;
	M3dMaths_TransposeMatrix1(&this->mTransform, &localMat);
	gte_SetRotMatrix(&localMat);

	CVector box[4] = { v0, v1, v2, v3 };

	i32 maxX = 0x20;
	i32 minX = -0x20;
	i32 maxZ = 0x40;
	i32 minZ = -0x40;
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

	CVector heightOffset;
	heightOffset.vx = 0;
	heightOffset.vy = height;
	heightOffset.vz = 0;

	heightOffset >>= 12;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&heightOffset));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&heightOffset));

	print_if_false(1, "MGS shadow");

	// footprint corners in local space, from the min/max X/Z box extents.
	CVector corners[4];
	corners[0].vx = minX; corners[0].vy = 0; corners[0].vz = maxZ;
	corners[1].vx = minX; corners[1].vy = 0; corners[1].vz = minZ;
	corners[2].vx = maxX; corners[2].vy = 0; corners[2].vz = maxZ;
	corners[3].vx = maxX; corners[3].vy = 0; corners[3].vz = minZ;

	gte_SetRotMatrix(&this->mTransform);

	i32 ry = this->realRegisterArr[0] << 12;

	for (i = 0; i < 4; i++)
	{
		gte_ldlvl(reinterpret_cast<VECTOR*>(&corners[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&corners[i]));

		corners[i] <<= 12;
		corners[i].vx += this->mPos.vx;
		corners[i].vy = ry;
		corners[i].vz += this->mPos.vz;
	}

	if (!this->field_33C)
	{
		G_TOTALBITUSAGE = 0;
		this->field_33C = new CQuadBit();
		G_TOTALBITUSAGE = -1;

		reinterpret_cast<CQuadBit*>(this->field_33C)->SetTexture(0, 0);
		reinterpret_cast<CQuadBit*>(this->field_33C)->mFrigDeltaZ = 32;
	}

	reinterpret_cast<CQuadBit*>(this->field_33C)->SetTransparency(0x40);
	reinterpret_cast<CQuadBit*>(this->field_33C)->SetSubtractiveTransparency();
	reinterpret_cast<CQuadBit*>(this->field_33C)->SetCorners(corners[0], corners[1], corners[2], corners[3]);
}

// @Ok

i32* CBlackCat::GetNewCommandBlock(u32 wordCount) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    return commands.allocate(wordCount);
}

// @Ok
// no xrefs to this function found in IDA (it is not called from AI(),
// DoMGSShadow(), the constructor/destructor or SynthesizeAnalogueInput()),
// same as CSpClone::KillCommandBlockByID (spclone.cpp), so there is no
// original address to compare against; likely dead/unused code, kept for
// interface completeness. Written to match the mScriptCommandHead command block list
// walk used everywhere else in this file (block[0] is the command id).

void CBlackCat::KillCommandBlockByID(i32 type) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    commands.removeByType(type);
}

// Original actor entry point; shared bytecode behavior lives in ActorScriptController.
void CBlackCat::SynthesizeAnalogueInput(void) {
    spidey::cinematics::ActorScriptState state = {mScriptElapsedTicks,
        mScriptBytecodeActive, mScriptCursorAddress, mScriptCommandHead,
        mScriptRunning, mLookTargetNode, &mScriptCheckpointNode};
    spidey::cinematics::ActorScriptController controller(*this, state,
        spidey::cinematics::ActorScriptProfile::blackCat(),
        spidey::cinematics::scriptCommandAllocator());
    controller.update();
}

// @Ok
// verified against IDA disasm of 0x413aa0: DeleteFrom, then the polymorphic
// delete on field_33C (call through vtbl[0] with flag 1, matches `delete`
// on a pointer with a virtual destructor, so field_33C's real type has one),
// then the KillAllCommandBlocks loop inlined here, in that exact order.
CBlackCat::~CBlackCat(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	delete reinterpret_cast<CClass*>(this->field_33C);

	this->KillAllCommandBlocks();
}

// @Ok
// verified line by line against IDA decompile/disasm of 0x4139a0. "bc2"
// string confirmed at 0x54875C, mpLight target confirmed as M3d_BlackCatLight,
// AttachTo target confirmed as BaddyList.
CBlackCat::CBlackCat(i16* a2, i32 a3)
{
	if (Trig_GetLevelID() != 2051)
	{
		this->InitItem("blackcat");
	}
	else
	{
		this->InitItem("bc2");
	}

	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));

	this->field_21E = 100;
	this->RunAnim(0xC, 0, -1);
	this->mFlags |= 0x480;

	this->mpLight = &M3d_BlackCatLight;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mType = 319;
	this->field_31C.bothFlags = 1;

	this->mNode = a3;
	this->mRMinor = 0;
	this->mScriptCursorAddress = reinterpret_cast<i32>(v5);

	if (*gSubmarinerDieRelated && Trig_GetLevelID() != 2051)
		this->Die(0);
}

// @Ok
void BlackCat_CreateBlackCat(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CBlackCat(v2, v3));
}


// @Ok
void CBlackCat::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// Original physics entry point; integration and integer rules live in ActorMotionIntegrator.
void CBlackCat::DoPhysics(void) {
    spidey::world::ActorMotionState state = {mPos, mVel, mAcc, mAngles,
        mAngVel, mAngAcc, mAngFric};
    spidey::world::ActorMotionIntegrator(state).advanceTwoTickSteps(mElapsedTicks, mGravityAcceleration);
}

// @Ok
// @Matching

i32* CBlackCat::KillCommandBlock(i32* command) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    return commands.remove(command);
}

// @Ok
// verified: this exact loop shape (walk mScriptCommandHead, KillCommandBlock each
// node, then mScriptCommandHead = 0 at the end) is inlined into the destructor at
// 0x413aa0, in that order, confirming this is the original body.

void CBlackCat::KillAllCommandBlocks(void) {
    spidey::cinematics::ScriptCommandList commands(mScriptCommandHead,
        spidey::cinematics::scriptCommandAllocator());
    commands.clear();
}

void validate_CBlackCat(void){
	VALIDATE_SIZE(CBlackCat, 0x354);


	VALIDATE(CBlackCat, mLookTargetNode, 0x324);
	VALIDATE(CBlackCat, mScriptCheckpointNode, 0x328);
	VALIDATE(CBlackCat, mGravityAcceleration, 0x32C);

	VALIDATE(CBlackCat, field_33C, 0x33C);


	VALIDATE(CBlackCat, mScriptRunning, 0x340);
	VALIDATE(CBlackCat, mScriptElapsedTicks, 0x344);
	VALIDATE(CBlackCat, mScriptBytecodeActive, 0x348);

	VALIDATE(CBlackCat, mScriptCursorAddress, 0x34C);
	VALIDATE(CBlackCat, mScriptCommandHead, 0x350);
}

// @Bogus
// The constructor stays in the exe. Our CBlackCat has no Hit, but the original
// vtable at 0x53B448 puts a "return 0" stub (0x4B0D60) in slot 3, so stamping
// our vtable would fall back to CBody::Hit and Black Cat would start taking
// damage. BlackCat_CreateBlackCat and BlackCat_RelocatableModuleInit go with
// it, they build (or hand out the pointer that builds) that same object.
void patch_blackcat(void)
{
	PATCH_PUSH_RET(0x004138D0, BlackCat_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x00413AA0, CBlackCat::~CBlackCat, "??1CBlackCat@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00413B60, CBlackCat::AI, "?AI@CBlackCat@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x00413FE0, CBlackCat::KillCommandBlock, "?KillCommandBlock@CBlackCat@@QAEPAHPAH@Z");
	PATCH_PUSH_RET_POLY(0x00414050, CBlackCat::SynthesizeAnalogueInput, "?SynthesizeAnalogueInput@CBlackCat@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00414B00, CBlackCat::Shouldnt_DoPhysics_Be_Virtual, "?Shouldnt_DoPhysics_Be_Virtual@CBlackCat@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x00414B10, CBlackCat::DoPhysics, "?DoPhysics@CBlackCat@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00414C50, CBlackCat::DoMGSShadow, "?DoMGSShadow@CBlackCat@@QAEXXZ");
}
