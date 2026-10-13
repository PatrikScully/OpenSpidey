#include "game/cinematics/camera.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "engine/rendering/ps2funcs.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"
#include "game/player/spidey.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/trig.h"
#include "game/objects/powerup.h"
#include "engine/input/ps2pad.h"
#include "game/application/SpideyDX.h"
#include "engine/input/PCInput.h"
#include "game/actors/baddy.h"

#ifndef SPIDEY_STANDALONE
SViewport gViewport;
#else
extern SViewport gViewport;
#endif
#ifndef SPIDEY_STANDALONE
CCamera *CameraList;
#else
extern CCamera * CameraList;
#endif

// ---------------------------------------------------------------------------
// The whole camera is in this file now: CCamera::AI (0x417CB0) is the
// per-frame driver that calls the mode handlers, MoveToDesiredPos (0x416B10)
// places the camera, CM_FixedFocus (0x418C40) and CM_Boss3 (0x4192F0) are the
// last two mode handlers and Camera_SelectOptimumViewingNode (0x419430) picks
// a viewing node for the fixed camera.  In the DLL build none of these five
// are hooked, so the exe's copies still run there and keep reading and
// writing the globals at their original addresses; ours have to be the same
// memory or the two halves get private copies and the camera stops
// responding.  The standalone build places every twinned global at its exe
// address (platform/exemem_syms.ld), so both spellings are one storage there.
//
// Every address below was read out of the original disassembly
// (tools/functions/*.bin), not taken from a name list.
//
// The shake amplitude tables (BigShakeAmp .. LandShakeSpeed) deliberately stay
// repo-local: the only writes to them in the whole binary are the C++ static
// initialisers at 0x415D20..0x415DC0, so both copies hold the same constants.
// ---------------------------------------------------------------------------

// @Ok
EXPORT CSVector BigShakeAmp(25, 0, 50);

// @Ok
EXPORT CSVector MediumShakeAmp(0, 0, 30);

// @Ok
EXPORT CSVector SmallShakeAmp(0, 0, 25);

// @Ok
EXPORT CSVector UnkShakeAmp(0, 0, 16);

// @Ok
EXPORT CFriction LandShakeDecay(3, 0, 3);

// @Ok
EXPORT CSVector LandShakeSpeed(600, 0, 600);

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCameraModeOne;
#else
extern i32 gCameraModeOne;
#endif
//#define G_CAMERA_MODE_ONE (gCameraModeOne)
#define G_CAMERA_MODE_ONE (*reinterpret_cast<i32*>(0x0056F254))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCameraModeTwo;
#else
extern i32 gCameraModeTwo;
#endif
//#define G_CAMERA_MODE_TWO (gCameraModeTwo)
#define G_CAMERA_MODE_TWO (*reinterpret_cast<i32*>(0x0056F38C))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCameraModeThree;
#else
extern i32 gCameraModeThree;
#endif
//#define G_CAMERA_MODE_THREE (gCameraModeThree)
#define G_CAMERA_MODE_THREE (*reinterpret_cast<i32*>(0x0056F28C))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 NumCameras;
#else
extern i32 NumCameras;
#endif
//#define G_NUM_CAMERAS (NumCameras)
#define G_NUM_CAMERAS (*reinterpret_cast<i32*>(0x0056F3B4))

#ifndef SPIDEY_STANDALONE
SCamera gMikeCamera[2];
#else
extern SCamera gMikeCamera[2];
#endif

// Same address as gCameraModeOne (0x0056F254), see CCamera::SetMode.
// EXPORT i32 gCameraModeRelated;

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 camXZDist = 0x120;
#else
extern i32 camXZDist;
#endif
//#define G_CAM_XZ_DIST (camXZDist)
#define G_CAM_XZ_DIST (*reinterpret_cast<i32*>(0x00548860))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 camYDist = 0xFFFFFFDC;
#else
extern i32 camYDist;
#endif
//#define G_CAM_Y_DIST (camYDist)
#define G_CAM_Y_DIST (*reinterpret_cast<i32*>(0x00548864))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gWtfCam[35];
#else
extern i32 gWtfCam[35];
#endif
//#define G_WTF_CAM (gWtfCam)
#define G_WTF_CAM (reinterpret_cast<i32*>(0x0056F124))


// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXZDistanceRelated;
#else
extern i32 gCamXZDistanceRelated;
#endif
//#define G_CAM_XZ_DISTANCE_RELATED (gCamXZDistanceRelated)
#define G_CAM_XZ_DISTANCE_RELATED (*reinterpret_cast<i32*>(0x0056F3BC))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXZRelatedTwo;
#else
extern i32 gCamXZRelatedTwo;
#endif
//#define G_CAM_XZ_RELATED_TWO (gCamXZRelatedTwo)
#define G_CAM_XZ_RELATED_TWO (*reinterpret_cast<i32*>(0x0056F0DC))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXZRelatedThree;
#else
extern i32 gCamXZRelatedThree;
#endif
//#define G_CAM_XZ_RELATED_THREE (gCamXZRelatedThree)
#define G_CAM_XZ_RELATED_THREE (*reinterpret_cast<i32*>(0x0056F0EC))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYDistanceRelated;
#else
extern i32 gCamYDistanceRelated;
#endif
//#define G_CAM_Y_DISTANCE_RELATED (gCamYDistanceRelated)
#define G_CAM_Y_DISTANCE_RELATED (*reinterpret_cast<i32*>(0x0056F3C0))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYDistanceRelatedTwo;
#else
extern i32 gCamYDistanceRelatedTwo;
#endif
//#define G_CAM_Y_DISTANCE_RELATED_TWO (gCamYDistanceRelatedTwo)
#define G_CAM_Y_DISTANCE_RELATED_TWO (*reinterpret_cast<i32*>(0x0056F258))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYDistanceRelatedThree;
#else
extern i32 gCamYDistanceRelatedThree;
#endif
//#define G_CAM_Y_DISTANCE_RELATED_THREE (gCamYDistanceRelatedThree)
#define G_CAM_Y_DISTANCE_RELATED_THREE (*reinterpret_cast<i32*>(0x0056F290))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXOffsetRelatedOne;
#else
extern i32 gCamXOffsetRelatedOne;
#endif
//#define G_CAM_X_OFFSET_ONE (gCamXOffsetRelatedOne)
#define G_CAM_X_OFFSET_ONE (*reinterpret_cast<i32*>(0x0056F3A4))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXOffsetRelatedTwo;
#else
extern i32 gCamXOffsetRelatedTwo;
#endif
//#define G_CAM_X_OFFSET_TWO (gCamXOffsetRelatedTwo)
#define G_CAM_X_OFFSET_TWO (*reinterpret_cast<i32*>(0x0056F3C8))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXOffsetRelatedThree;
#else
extern i32 gCamXOffsetRelatedThree;
#endif
//#define G_CAM_X_OFFSET_THREE (gCamXOffsetRelatedThree)
#define G_CAM_X_OFFSET_THREE (*reinterpret_cast<i32*>(0x0056EFF8))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamXOffsetRelatedFour;
#else
extern i32 gCamXOffsetRelatedFour;
#endif
//#define G_CAM_X_OFFSET_FOUR (gCamXOffsetRelatedFour)
#define G_CAM_X_OFFSET_FOUR (*reinterpret_cast<i32*>(0x0056F3C4))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYOffsetOne;
#else
extern i32 gCamYOffsetOne;
#endif
//#define G_CAM_Y_OFFSET_ONE (gCamYOffsetOne)
#define G_CAM_Y_OFFSET_ONE (*reinterpret_cast<i32*>(0x0056F11C))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYOffsetTwo;
#else
extern i32 gCamYOffsetTwo;
#endif
//#define G_CAM_Y_OFFSET_TWO (gCamYOffsetTwo)
#define G_CAM_Y_OFFSET_TWO (*reinterpret_cast<i32*>(0x0056F3CC))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYOffsetThree;
#else
extern i32 gCamYOffsetThree;
#endif
//#define G_CAM_Y_OFFSET_THREE (gCamYOffsetThree)
#define G_CAM_Y_OFFSET_THREE (*reinterpret_cast<i32*>(0x0056F120))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamYOffsetFour = 0xFFFFFFFA;
#else
extern i32 gCamYOffsetFour;
#endif
//#define G_CAM_Y_OFFSET_FOUR (gCamYOffsetFour)
#define G_CAM_Y_OFFSET_FOUR (*reinterpret_cast<i32*>(0x00548868))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamZOffsetOne;
#else
extern i32 gCamZOffsetOne;
#endif
//#define G_CAM_Z_OFFSET_ONE (gCamZOffsetOne)
#define G_CAM_Z_OFFSET_ONE (*reinterpret_cast<i32*>(0x0056F078))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamZOffsetTwo;
#else
extern i32 gCamZOffsetTwo;
#endif
//#define G_CAM_Z_OFFSET_TWO (gCamZOffsetTwo)
#define G_CAM_Z_OFFSET_TWO (*reinterpret_cast<i32*>(0x0056F3D4))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamZOffsetThree;
#else
extern i32 gCamZOffsetThree;
#endif
//#define G_CAM_Z_OFFSET_THREE (gCamZOffsetThree)
#define G_CAM_Z_OFFSET_THREE (*reinterpret_cast<i32*>(0x0056F394))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCamZOffsetFour;
#else
extern i32 gCamZOffsetFour;
#endif
//#define G_CAM_Z_OFFSET_FOUR (gCamZOffsetFour)
#define G_CAM_Z_OFFSET_FOUR (*reinterpret_cast<i32*>(0x0056F3D0))

// @Ok
// @AlmostMatching: lea instruction when assigning a2 happens couple places up for some reason
void CCamera::SetTripodMotion(const CVector &a2, u32 a3)
{
	DoAssert(a3 != 0, "bad time value in SetTripodMotion");

	if (this->field_128)
	{
		CBody *pTripod = this->mTripod;
		if (pTripod)
		{
			if (!pTripod->IsDead())
			{
				this->field_104 = pTripod->mPos;
			}
		}
	}

	this->field_128 = a3;
	this->field_110 = a2;

	this->field_11C = (a2 - this->field_104) / a3;
}

// @Ok
// @Matching
void CCamera::SetCamZOffset(i16 offset, u16 frames)
{
	if (this->mCameraMode != CAMERAMODE_START && this->mCameraMode != CAMERAMODE_FAR)
	{
		if (frames)
		{
			gCamZOffsetThree = ((offset - gCamZOffsetFour) << 12) / frames;
			gCamZOffsetOne = offset;
			gCamZOffsetTwo = frames;
		}
		else
		{
			gCamZOffsetFour = offset;
			gWtfCam[31] = offset;
			gCamZOffsetTwo = 0;
		}
	}
}

// @Ok
// @Matching
void CCamera::SetCamYOffset(i16 offset, u16 frames)
{
	if (this->mCameraMode != CAMERAMODE_START && this->mCameraMode != CAMERAMODE_FAR)
	{
		if (frames)
		{
			gCamYOffsetThree = ((offset - gCamYOffsetFour) << 12) / frames;
			gCamYOffsetOne = offset;
			gCamYOffsetTwo = frames;
		}

		else
		{
			gCamYOffsetFour = offset;
			gWtfCam[30] = offset;
			gCamYOffsetTwo = 0;
		}
	}
}

// @Ok
// @Matching
void CCamera::SetCamXOffset(i16 offset, u16 frames)
{
	if (this->mCameraMode != CAMERAMODE_START && this->mCameraMode != CAMERAMODE_FAR)
	{
		if (frames)
		{
			gCamXOffsetRelatedThree = ((offset - gCamXOffsetRelatedFour) << 12) / frames;
			gCamXOffsetRelatedOne = offset;
			gCamXOffsetRelatedTwo = frames;
		}
		else
		{
			gCamXOffsetRelatedFour = offset;
			gWtfCam[29] = offset;
			gCamXOffsetRelatedTwo = 0;
		}
	}
}

// @Ok
// @Matching
i16 CCamera::GetCamYDistance(void)
{
	return camYDist;
}

// @Ok
// @Matching
void CCamera::SetCamXZDistance(u16 dist, u16 frames)
{
	if (frames)
	{
		gCamXZRelatedThree = (dist - camXZDist) / frames;
		gCamXZRelatedTwo = dist;

		if (this->field_23C)
		{
			gCamXZDistanceRelated = frames;
		}
	}
	else
	{
		gCamXZDistanceRelated = 0;
		camXZDist = dist;
		gWtfCam[32] = dist;
	}
}

// @Ok
// @Matching
i16 CCamera::GetCamXZDistance(void)
{
	return camXZDist;
}

// @Ok
// @Test
void CCamera::CM_TripodFocus(void)
{
	SVECTOR v2;
	CVector a1;
	CVector v4;
	CVector v5;
	MATRIX a2;

	v2.vx = 0;
	v2.vy = 0;
	v2.vz = 0;
	Utils_CalcAim(
		reinterpret_cast<CSVector *>(&v2),
		&this->field_104,
		&this->field_144);

	M3dMaths_RotMatrixYXZ(&v2, &a2);
	gte_SetRotMatrix(&a2);
	a1.vx = 0;
	a1.vy = 4096;
	a1.vz = 0;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&a1));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&a1));
	v4.vx = 0;
	v4.vy = 0;
	v4.vz = -4096;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&v4));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v4));

	v5.vx = 0;
	v5.vy = 0;
	v5.vz = 0;
	gte_ldopv1(reinterpret_cast<VECTOR*>(&a1));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&v4));
	gte_op12();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v5));

	a2.m[0][0] = v5.vx;
	a2.m[1][0] = v5.vy;
	a2.m[2][0] = v5.vz;

	a2.m[0][1] = a1.vx;
	a2.m[1][1] = a1.vy;
	a2.m[2][1] = a1.vz;

	a2.m[0][2] = v4.vx;
	a2.m[1][2] = v4.vy;
	a2.m[2][2] = v4.vz;

	MToQ(a2, this->field_1F4);
}

// @Ok
CCamera::CCamera(CBody* tripod)
{
	this->field_144.vx = 0;
	this->field_144.vy = 0;
	this->field_144.vz = 0;
	this->field_150.vx = 0;
	this->field_150.vy = 0;
	this->field_150.vz = 0;
	this->field_15C.vx = 0;
	this->field_130 = 4;
	this->field_15C.vy = 0;
	this->field_15C.vz = 0;
	this->field_1A8 = 0;
	this->field_1AC = 0;
	this->field_1B0 = 0;
	this->field_1B8.vx = 0;
	this->field_1B8.vy = 0;
	this->field_1B8.vz = 0;
	this->field_1D8 = 0;
	this->field_1B4 = 4096;
	this->field_1DC = 0;
	this->field_1E0 = 0;
	this->field_1E4.x = 0;
	this->field_1E4.y = 0;
	this->field_1E4.z = 0;
	this->field_1E4.w = 4096;
	this->field_1F4.x = 0;
	this->field_134 = 8;
	this->field_1F4.y = 0;
	this->field_1F4.z = 0;
	this->field_1F4.w = 4096;
	this->field_204.x = 0;
	this->field_204.y = 0;
	this->field_204.z = 0;
	this->field_204.w = 4096;
	this->field_214.x = 0;
	this->field_214.y = 0;
	this->field_214.z = 0;
	this->field_214.w = 4096;
	this->field_224 = 0;
	this->field_F8 = 1;
	this->field_F9 = 0;
	this->mTripod = tripod;
	this->field_100 = 1;
	this->field_128 = 0;
	this->field_12C = -1;
	this->field_138 = 4;
	this->field_13C = tripod;
	this->field_140 = 1;
	this->field_168 = 0;
	this->field_16C = 0;
	this->mZoom = 2365;
	this->field_1CC = 0;
	this->field_1CE = 0;
	this->field_228 = 0;
	this->field_22C = 0;
	this->field_230 = 4096;
	this->field_234 = 0;
	this->field_236 = 0;
	this->field_240 = 0;
	this->field_238 = 0;
	this->field_24C.vx = 0;
	this->field_244 = 0;
	this->field_258.vx = 0;
	this->field_24C.vy = 0;

	this->field_248 = 0;
	this->field_258.vy = 0;

	this->field_24C.vz = 0;
	this->field_258.vz = 0;
	this->field_284.vx = 0;
	this->field_284.vy = 0;
	this->field_284.vz = 0;
	this->field_290.x = 0;
	this->field_290.y = 0;
	this->field_290.z = 0;
	this->field_2B0.vx = 0;
	this->field_2B0.vy = 0;
	this->field_2B0.vz = 0;
	this->field_2C4.x = 0;
	this->field_290.w = 4096;
	this->field_2C4.y = 0;
	this->field_2C4.z = 0;
	this->field_2D4.x = 0;
	this->field_2C4.w = 4096;
	this->field_2D4.y = 0;
	this->field_2D4.z = 0;
	this->field_2E8.vx = 0;
	this->field_23C = 1;
	this->mCollisionRayLR = -1;
	this->mCollisionRayBack = -1;
	this->mCollisionAngLR = 256;
	this->mCollisionAngBack = 784;
	this->field_2A4 = 31;
	this->field_2A8 = 512;
	this->field_2D4.w = 4096;
	this->field_2E8.vy = 0;
	this->field_2E8.vz = 0;


	this->mLastUpdateTick = G_TIMER_RELATED;
	print_if_false(tripod != 0, "Bad tripod");
	this->mType = 99;
	this->mFlags = 1;

	this->field_104 = this->mPos;
	this->field_144 = tripod->mPos;

	this->LoadIntoMikeCamera();
	this->AttachTo(reinterpret_cast<CBody**>(&G_CAMERA_LIST));

	this->mCBodyFlags &= ~2;
	NumCameras++;
	this->mRMinor = 0;
	this->mCameraMode = CAMERAMODE_DEMO;
}

// @Ok
void CCamera::LoadIntoMikeCamera(void)
{
	G_MIKE_CAMERA[0].Position.vx = this->mPos.vx >> 12;
	G_MIKE_CAMERA[0].Position.vy = this->mPos.vy >> 12;
	G_MIKE_CAMERA[0].Position.vz = this->mPos.vz >> 12;

	QToM(&this->field_214, &G_MIKE_CAMERA[0].Transform);
	TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);

	i32 two = G_MIKE_CAMERA[0].Transform.m[0][2];
	i32 eight = G_MIKE_CAMERA[0].Transform.m[2][2];
	if (two || eight)
	{
		this->field_23A = (-1024 - ratan2(eight, two)) & 0xFFF;
	}
}

// @Ok
// @Test
void CCamera::SetFixedPosAnglesMode(
		CVector *a2,
		CQuat *a3,
		u16 a4)
{
	this->mCameraMode = CAMERAMODE_FAR;
	this->field_24C = *a2;
	this->field_2D4 = *a3;
	this->field_2AC = 1;
	this->field_2BC = a4;

	if (a4)
	{
		this->field_2C0 = a4;
		this->field_2C4 = this->field_1E4;

		this->field_2B0 = (*a2 - this->mPos) / a4;
	}
}

// @Ok
void CCamera::SetTripodInterpolation(i32 a2, i32 a3, i32 a4)
{
	DoAssert(a2 <= 16, "Bad tripod interpolation value");
	DoAssert(a3 <= 16, "Bad tripod interpolation value");
	DoAssert(a4 <= 16, "Bad tripod interpolation value");
	this->field_130 = a2;
	this->field_134 = a3;
	this->field_138 = a4;
}

// @Ok
CCamera::~CCamera(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_CAMERA_LIST));
	--NumCameras;
}

// @Ok
// Disassembly-verified against 0x4166c0 (95 bytes). Store order, the
// signed-divide-by-3 reciprocal trick on a3 and the plain a4 store all
// match field for field: mCameraMode then field_2AC then field_2E8 (full
// CVector copy) then field_2E4 (i16 store of the /3 result) then
// field_2BC (full i32 store of a4).
void CCamera::SetFixedFocusMode(CVector *a2, u16 a3, u16 a4){

	this->mCameraMode = CAMERAMODE_OVERHEAD;
	this->field_2AC = 1;
	this->field_2E8 = *a2;
	this->field_2E4 = (0xFFFF & a3) / 3;
	this->field_2BC = a4;
}

// @Ok
// @Matching
i32 CCamera::SetMode(ECameraMode mode){

	ECameraMode oldMode = this->mCameraMode;
	this->mCameraMode = mode;
	if (mode == CAMERAMODE_FUNKYFLYING || mode == CAMERAMODE_LOOKAROUND)
	{
		// gCameraModeRelated and gCameraModeOne were two repo variables for one
		// global: SetMode at 0x41680A and SetCamAngle at 0x41793F both write
		// 0x0056F254.  Kept apart they broke SetMode's reset of the camera
		// angle interpolation counter, so both now go through one macro.
		G_CAMERA_MODE_ONE = 0;
	}

	return oldMode;
}

// @Ok
// @Matching
void CCamera::SetCollisionRayLR(i32 r)
{
	this->mCollisionRayLR = r;
}

// @Ok
// @Matching
void CCamera::SetCollisionRayBack(i32 r)
{
	this->mCollisionRayBack = r;
}

// @Ok
// @Matching
void CCamera::SetCollisionAngLR(i16 a)
{
	this->mCollisionAngLR = a;
}

// @Ok
// @Matching
void CCamera::SetCollisionAngBack(i16 a)
{
	this->mCollisionAngBack = a;
}

// @Ok
void CCamera::SetZoom(i32 a2, u16 a3){

	this->field_174 = a3;
	if (this->field_174)
	{
		this->field_178 = (a2 - this->mZoom) / (0xFFFF & a3);
		this->field_17C = a2;
	}
	else
	{
		this->mZoom = a2;
	}
}

// @Ok
i32 CCamera::GetZoom(void) const
{
	return this->mZoom;
}


// @Ok
void CCamera::PushMode(void){

	ECameraMode mode = this->mCameraMode;
	this->field_280 = mode;

	if (mode == 4 || mode == 5 || mode == 6){
		this->field_284 = this->mPos;
		this->field_290 = this->field_1E4;
	}
}

// @Ok
void CCamera::PopMode(void)
{
	ECameraMode mode = this->field_280;
	if (mode == 4 || mode == 5 || mode == 6){
		this->mPos = this->field_284;
		this->field_1E4 = this->field_290;
	}

	this->mCameraMode = mode;
}


// sin/cos table shared with quat.cpp/shell.cpp/spidey.cpp/manipob.cpp
// (same address, same file-local raw-address convention used there).
static i16 * const word_610C48 = (i16*)0x610C48;

// The camera's pitch (fixed-point, 4096 = full circle) around the tripod:
// CCamera::AI and CM_Boss3 set it from ratan2(-YDistance, XZDistance), CM_Normal
// and MoveToDesiredPos use it as a sin/cos table index.
static i32 * const gCameraLookAngle = (i32*)0x548858;
// The camera's distance from the tripod: AI and CM_Boss3 set it to the length
// of (XZDistance, YDistance), CM_FixedFocus eases it toward field_2E4,
// MoveToDesiredPos builds the (0, 0, -distance) camera offset from it.
static i32 * const gCameraDistance = (i32*)0x54885C;

// The tripod-to-camera-pivot offset, a CVector global (static initialiser at
// 0x415DE0). CCamera::AI rebuilds it every frame from the X/Y/Z offsets
// rotated by the camera yaw (field_236), CM_Boss3 zeroes x and z,
// MoveToDesiredPos and CM_FixedPos add it to field_104.
static CVector * const gCameraOffset = (CVector*)0x56F260;

// Camera position as of the last CCamera::AI, a CVector global (static
// initialiser at 0x415E60). Written there and read by nothing else in the
// binary, so probably left over for a debug display.
static CVector * const gCameraPos = (CVector*)0x56F2F0;

// Select-button edge detector in CCamera::AI: gCameraSelectReleased is 1
// while Select is up, gCameraSelectTriggered goes 1 the frame it is pressed.
// Only AI references either address and nothing reads Triggered back.
static i32 * const gCameraSelectTriggered = (i32*)0x56F3DC;
static i32 * const gCameraSelectReleased = (i32*)0x56F3E0;

// Zeroed every frame by CCamera::AI right before the mode switch and
// referenced by no other function in the binary; leftover debug counters as
// far as we can tell.
static i32 * const gCameraUnusedOne = (i32*)0x56F3E4;
static i32 * const gCameraUnusedTwo = (i32*)0x56F3E8;

// Region and model of the eight items held in field_184, two i32[8] filled and
// checked only by MoveToDesiredPos, so a slot whose item got recycled into a
// different object is dropped instead of un-flagged.
static i32 * const gCameraHeldItemRegions = (i32*)0x56F2D0;
static i32 * const gCameraHeldItemModels = (i32*)0x56F350;

// @Ok
// Disassembly-verified against 0x418e00 (IDA decompile of sub_418E00,
// with the mislabeled qt_register_signal_spy_callbacks/subroutine calls
// resolved back to gte_ldlvl/gte_ldopv1/gte_ldopv2/gte_op12/gte_stlvnl
// by matching call targets against tools/names.json). Two vectors get
// built from the same sin/cos table lookup and each rotated in place by
// the SAME per-object euler matrix (field_234/236/238):
// fwd = Rot * {0, -sinA, cosA} (a plain stack temp), and
// up2 = Rot * {0, cosA, sinA} (stored directly into field_1D8/1DC/1E0,
// which double as scratch storage here, matching the disassembly's use
// of &this->field_1D8 as the VECTOR argument to gte_ldlvl/gte_stlvnl).
// The final matrix's columns are opResult (= up2 outer-product negFwd),
// up2 and negFwd, one column per row (m[row][0]=opResult, m[row][1]=up2,
// m[row][2]=negFwd), confirmed from the v28[] index pattern in the
// decompile (v28[0..2] = row0, v28[3..5] = row1, v28[6..8] = row2).
void CCamera::CM_Normal(void)
{
	i32 idx = 2 * (*gCameraLookAngle & 0xFFF);
	i32 sinA = word_610C48[idx];
	i32 cosA = word_610C48[idx + 1];

	SVECTOR angles;
	angles.vx = this->field_234;
	angles.vy = this->field_236;
	angles.vz = this->field_238;

	MATRIX mat;
	M3dMaths_RotMatrixYXZ(&angles, &mat);
	gte_SetRotMatrix(&mat);

	CVector fwd;
	fwd.vx = 0;
	fwd.vy = -sinA;
	fwd.vz = cosA;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&fwd));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&fwd));

	this->field_24C = fwd * (*gCameraDistance) + this->field_104;
	this->field_1D8 = 0;
	this->field_258 = this->field_104;

	// second lookup at the same angle, unswapped this time (matches the
	// original's redundant global reload instead of reusing sinA/cosA)
	i32 idx2 = 2 * (*gCameraLookAngle & 0xFFF);
	this->field_1DC = word_610C48[idx2 + 1];
	this->field_1E0 = word_610C48[idx2];

	gte_ldlvl(reinterpret_cast<VECTOR*>(&this->field_1D8));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&this->field_1D8));

	CVector negFwd;
	negFwd.vx = -fwd.vx;
	negFwd.vy = -fwd.vy;
	negFwd.vz = -fwd.vz;

	gte_ldopv1(reinterpret_cast<VECTOR*>(&this->field_1D8));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&negFwd));
	gte_op12();

	CVector opResult;
	gte_stlvnl(reinterpret_cast<VECTOR*>(&opResult));

	MATRIX mat2;
	mat2.m[0][0] = opResult.vx;
	mat2.m[0][1] = this->field_1D8;
	mat2.m[0][2] = negFwd.vx;
	mat2.m[1][0] = opResult.vy;
	mat2.m[1][1] = this->field_1DC;
	mat2.m[1][2] = negFwd.vy;
	mat2.m[2][0] = opResult.vz;
	mat2.m[2][1] = this->field_1E0;
	mat2.m[2][2] = negFwd.vz;
	mat2.t[0] = 0;
	mat2.t[1] = 0;
	mat2.t[2] = 0;

	MToQ(mat2, this->field_1F4);

	if (this->field_234 != 0 || this->field_238 != 0)
	{
		this->field_236 = (-1024 - ratan2(mat2.m[2][2], mat2.m[0][2])) & 0xFFF;
	}
}


// @Ok
void CCamera::SetStartPosition(void){

	if (this->mCameraMode == CAMERAMODE_DEMO)
	{
		this->field_104 = this->mTripod->mPos;
		this->CM_Normal();
		this->mPos = this->field_24C;
		this->field_1E4 = this->field_1F4;
	}

}

// @Ok
// AlmostMatching: two instructions swapped around realted to assingment on 2AC and 24C, no biggie
void CCamera::SetFixedPosMode(CVector &a2, u16 a3){

	this->field_2AC = 1;
	this->mCameraMode = CAMERAMODE_START;
	this->field_24C = a2;

	this->field_2BC = a3;

	if (a3)
	{
		this->field_2B0 = (a2 - this->mPos)/a3;
	}
}


// @Ok
// @Matching
void CCamera::CM_FixedPosAngles(void)
{
	if (this->field_2BC)
	{
		if (this->field_2BC - this->mElapsedTicks > 0)
		{
			this->mPos += (this->field_2B0 * this->mElapsedTicks);
		}
		else
		{
			this->mPos = this->field_24C;
		}
	}
	else
	{
		this->mPos = this->field_24C;
	}

	i32 v4 = this->field_2BC;
	if (v4)
	{
		v4 -= this->mElapsedTicks;

		if (v4 > 0)
		{
			i32 tmp = ((this->field_2C0 - v4) << 12) / this->field_2C0;
			Quat_Slerp(
				this->field_2C4,
				this->field_2D4,
				tmp,
				this->field_1F4);
		}
		else
		{
			this->field_1F4 = this->field_2D4;
		}
	}
	else
	{
		this->field_1F4 = this->field_2D4;
	}

	this->field_2BC = v4;


	if (this->field_2AC)
	{
		this->field_2AC = 0;
		if (!this->field_2BC)
		{
			this->field_1E4 = this->field_1F4;
			this->field_204 = this->field_1E4;
		}
	}
}

// @Ok
// Disassembly-verified against 0x4189a0. Two fixes from the earlier draft:
// (1) v6 is field_144 minus *gCameraOffset (call target 0x4E7760 is the
// global CVector operator-, confirmed against the same address noted for
// CQuadBit::OrientUsing in bit.cpp), not a multiply; (2) the function
// ends with the same ratan2-based field_236 recompute idiom used
// elsewhere in this file (LoadIntoMikeCamera, CM_Normal), unconditional
// here (no field_234/238 gate), which the earlier draft dropped entirely.
void CCamera::CM_FixedPos(void){

	int v2; // eax
	CVector v6; // eax
	int v10; // eax
	VECTOR v15; // [esp+1Ch] [ebp-50h] BYREF
	VECTOR a1; // [esp+2Ch] [ebp-40h] BYREF
	VECTOR v17; // [esp+3Ch] [ebp-30h] BYREF
	MATRIX v18; // [esp+4Ch] [ebp-20h] BYREF

	v2 = this->field_2BC;
	if ( v2 )
	{
		this->field_2BC = v2 - this->mElapsedTicks;
		if ( this->field_2BC > 0 )
		{
			this->mPos += this->field_2B0 * this->mElapsedTicks;
		}
		else
		{
			this->field_2BC = 0;
			this->mPos = this->field_24C;
		}
	}
	else
	{
		this->mPos = this->field_24C;
	}


	this->field_258 = this->field_144;

	v6 = this->mPos - *gCameraOffset;

	v15.vx = (this->field_258.vx - v6.vx) >> 12;
	v15.vy = (this->field_258.vy - v6.vy) >> 12;
	v15.vz = (this->field_258.vz - v6.vz) >> 12;

	VectorNormal(&v15, &v15);
	a1.vx = 0;
	a1.vy = 4096;
	a1.vz = 0;
	gte_ldopv1(&a1);
	gte_ldopv2(&v15);
	gte_op12();
	gte_stlvnl(&v17);
	gte_ldopv1(&v15);
	gte_ldopv2(&v17);
	gte_op12();
	gte_stlvnl(&a1);

	v18.m[0][0] = v17.vx;
	v18.m[0][1] = v17.vy;
	v18.m[0][2] = v17.vz;

	v18.m[1][0] = a1.vx;
	v18.m[1][1] = a1.vy;
	v18.m[1][2] = a1.vz;

	v18.m[2][0] = v15.vx;
	v18.m[2][1] = v15.vy;
	v18.m[2][2] = v15.vz;


	MToQ(v18, this->field_1F4);
	if ( this->field_2AC )
	{
		v10 = this->field_2BC;
		this->field_2AC = 0;
		if ( !v10 )
		{
			this->field_1E4 = this->field_1F4;
			this->field_204 = this->field_1F4;
		}
	}

	this->field_236 = (-1024 - ratan2(v18.m[2][2], v18.m[0][2])) & 0xFFF;
}

// @Ok
// @Matching
void CCamera::SetCamYDistance(i16 dist, u16 frames)
{
	if (frames)
	{
		gCamYDistanceRelatedThree = (dist - camYDist) / frames;
		gCamYDistanceRelatedTwo = dist;

		if (this->field_23C)
		{
			gCamYDistanceRelated = frames;
		}
	}
	else
	{
		camYDist = dist;
		gWtfCam[33] = dist;
		gCamYDistanceRelated = 0;
	}
}


// @Ok
// @AlmostMatching: ecx is put into edx for some reason
void CCamera::SetCamAngle(i16 y, u16 frames)
{
	if (this->mCameraMode != CAMERAMODE_LOOSE 
			&& this->mCameraMode != CAMERAMODE_USER 
			&& this->mCameraMode != CAMERAMODE_LOOKAROUND)
	{
		i16 v4 = y & 0xFFF;

		if (frames)
		{
			i16 v5 = this->field_236;
			gCameraModeTwo = v4;
			if (v4 > v5)
			{
				i32 v6 = v4 - v5;
				gCameraModeOne = frames;
				if (v6 > 2048)
				{
					gCameraModeThree = (v6 - 4096) / frames;
				}
				else
				{
					gCameraModeThree = v6 / frames;
				}
			}
			else if (v4 < v5)
			{
				gCameraModeOne = frames;
				if (v5 - v4 > 2048)
				{
					gCameraModeThree = (v4 - v5 + 4096) / frames;
				}
				else
				{
					gCameraModeThree = (v4 - v5) / frames;
				}
			}
			else
			{
				gCameraModeThree = 0;
				gCameraModeOne = 0;
			}
		}
		else
		{
			gCameraModeOne = 0;
			this->field_236 = y & 0xFFF;
			gWtfCam[34] = this->field_236;
		}

	}
}

// @Ok
// Has no standalone address in the PC binary to diff against: it is
// INLINE'd away at every call site (CM_Boss3 and
// Camera_SelectOptimumViewingNode below both carry the expanded idiom).
// The Mac build has it as a real out-of-line function, CalcTheta(short,
// short), 52 bytes (tools/prototypes.json, idbs/spiderman_names.txt),
// confirming the parameter types here. The body reproduces the exact
// same shortest-signed-angle-delta idiom already verified in
// CCamera::SetCamAngle further down this file (mask both operands with
// 0xFFF, then wrap the difference into (-2048, 2048]); the i16 return
// type cannot overflow since both masked inputs are 0..4095, so the
// unwrapped difference is at most +-4095 and every wrapped branch lands
// in +-2047.
 i16 CalcTheta(i16 a1, i16 a2)
{
	i16 v2 = (a2 & 0xFFF) - (a1 & 0xFFF);
	if (v2 > 2048)
		return v2 - 4096;
	if (v2 < -2048)
		return v2 + 4096;
	return v2;
}


// @Ok
void CCamera::GetPosition(CVector &dst)
{
	dst = this->mPos;
}


// @Ok
// @Matching
void CCamera::Shake(CVector& pos, EShakeType ShakeMagnitude)
{
	switch (ShakeMagnitude)
	{
		case CAMERASHAKE_BIG:
			this->mShakeAmp = BigShakeAmp;
			this->mShakeDecay = LandShakeDecay;
			this->mShakeSpeed = LandShakeSpeed;
			break;
		case CAMERASHAKE_MEDIUM:
			this->mShakeAmp = MediumShakeAmp;
			this->mShakeDecay = LandShakeDecay;
			this->mShakeSpeed = LandShakeSpeed;
			break;
		case CAMERASHAKE_SMALL:
			this->mShakeAmp = SmallShakeAmp;
			this->mShakeDecay = LandShakeDecay;
			this->mShakeSpeed = LandShakeSpeed;
			break;
		case CAMERASHAKE_UNK:
			this->mShakeAmp = UnkShakeAmp;
			this->mShakeDecay = LandShakeDecay;
			this->mShakeSpeed = LandShakeSpeed;
			break;
		default:
			DoAssert(0, "Unknown EShakeType!");
			break;
	}
}

// @Ok
// 0x4164F0 (458 bytes). The euler-angle twin of the CQuat overload above:
// same mode/position/interpolation setup, then the angles go through
// M3dMaths_RotMatrixYXZ and the rotated up (0,4096,0) and back (0,0,-4096)
// vectors plus their cross product are packed into a matrix for MToQ, the
// same construction CM_TripodFocus and CM_FixedFocus use.
void CCamera::SetFixedPosAnglesMode(CVector &pos, CSVector &angles, u16 frames)
{
	this->mCameraMode = CAMERAMODE_FAR;
	this->field_2AC = 1;
	this->field_24C = pos;
	this->field_2BC = frames;

	if (frames)
	{
		this->field_2C0 = frames;
		this->field_2C4 = this->field_1E4;
		this->field_2B0 = (pos - this->mPos) / frames;
	}

	MATRIX mat;
	M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&angles), &mat);
	gte_SetRotMatrix(&mat);

	CVector up(0, 4096, 0);
	gte_ldlvl(reinterpret_cast<VECTOR*>(&up));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&up));

	CVector back(0, 0, -4096);
	gte_ldlvl(reinterpret_cast<VECTOR*>(&back));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&back));

	CVector right(0, 0, 0);
	gte_ldopv1(reinterpret_cast<VECTOR*>(&up));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&back));
	gte_op12();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&right));

	mat.m[0][0] = right.vx;
	mat.m[1][0] = right.vy;
	mat.m[0][1] = up.vx;
	mat.m[2][0] = right.vz;
	mat.m[0][2] = back.vx;
	mat.m[1][1] = up.vy;
	mat.m[2][1] = up.vz;
	mat.m[1][2] = back.vy;
	mat.m[2][2] = back.vz;

	MToQ(mat, this->field_2D4);
}

// @Ok
// 0x418C40 (428 bytes). Eases gCameraDistance toward field_2E4 by 16 units
// per tick, then aims from the tripod (field_104) at the fixed focus point
// (field_2E8) and turns that aim into the desired orientation field_1F4 with
// the same up/back/cross matrix construction as CM_TripodFocus. Ends with the
// yaw recompute idiom (ratan2 on the back vector) also used by CM_Normal.
void CCamera::CM_FixedFocus(void)
{
	i32 target = static_cast<u16>(this->field_2E4);

	if (*gCameraDistance >= target)
	{
		if (*gCameraDistance > target)
		{
			*gCameraDistance += -16 * this->mElapsedTicks;
			if (*gCameraDistance < target)
			{
				*gCameraDistance = target;
			}
		}
	}
	else
	{
		*gCameraDistance += 16 * this->mElapsedTicks;
		if (*gCameraDistance > target)
		{
			*gCameraDistance = target;
		}
	}

	SVECTOR aim;
	aim.vx = 0;
	aim.vy = 0;
	aim.vz = 0;
	Utils_CalcAim(reinterpret_cast<CSVector*>(&aim), &this->field_104, &this->field_2E8);

	MATRIX mat;
	M3dMaths_RotMatrixYXZ(&aim, &mat);
	gte_SetRotMatrix(&mat);

	CVector up(0, 4096, 0);
	gte_ldlvl(reinterpret_cast<VECTOR*>(&up));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&up));

	CVector back(0, 0, -4096);
	gte_ldlvl(reinterpret_cast<VECTOR*>(&back));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&back));

	CVector right(0, 0, 0);
	gte_ldopv1(reinterpret_cast<VECTOR*>(&up));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&back));
	gte_op12();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&right));

	mat.m[0][0] = right.vx;
	mat.m[1][0] = right.vy;
	mat.m[0][1] = up.vx;
	mat.m[1][1] = up.vy;
	mat.m[2][0] = right.vz;
	mat.m[0][2] = back.vx;
	mat.m[1][2] = back.vy;
	mat.m[2][1] = up.vz;
	mat.m[2][2] = back.vz;

	MToQ(mat, this->field_1F4);

	this->field_236 = (-1024 - ratan2(back.vz, back.vx)) & 0xFFF;
}

// @Ok
// 0x4192F0 (310 bytes). Boss camera: with no boss (gBossRelated == 0) it is
// plain CM_Normal focused on the tripod. With a boss it focuses on the boss,
// drops the x/z part of gCameraOffset, and when the tripod is further than
// field_2A8 from the boss it turns the camera yaw (field_236) toward the boss
// by at most field_2A4 per frame (CalcTheta idiom expanded inline in the
// original, dead zone of 8). Then it rebuilds gCameraDistance/gCameraLookAngle
// from the XZ/Y distances exactly like the CAMERAMODE_DEMO branch of AI and
// falls through to CM_Normal.
void CCamera::CM_Boss3(void)
{
	CBody* pBoss = reinterpret_cast<CBody*>(gBossRelated);
	if (!pBoss)
	{
		this->field_13C = this->mTripod;
		this->CM_Normal();
		return;
	}

	this->field_13C = pBoss;
	gCameraOffset->vx = 0;
	gCameraOffset->vz = 0;

	i32 dist = spidey::math::approximateDistance(this->field_104, pBoss->mPos);

	CSVector aim(0, 0, 0);
	Utils_CalcAim(&aim, &this->field_104, &pBoss->mPos);

	i16 yaw = this->field_236;
	i32 delta = CalcTheta(yaw, aim.vy);

	if (dist > this->field_2A8)
	{
		if (delta < -8)
		{
			if (delta < -this->field_2A4)
			{
				delta = -this->field_2A4;
			}
			this->field_236 = delta + yaw;
		}
		else if (delta > 8)
		{
			if (delta > this->field_2A4)
			{
				delta = this->field_2A4;
			}
			this->field_236 = delta + yaw;
		}
	}

	this->field_236 &= 0xFFF;

	i32 xz = G_CAM_XZ_DIST;
	i32 y = -G_CAM_Y_DIST;
	*gCameraDistance = M3dMaths_SquareRoot0(y * y + xz * xz);
	*gCameraLookAngle = ratan2(y, xz);

	this->CM_Normal();
}

// @Ok
// 0x416B10 (3064 bytes). Functional decomp from Hex-Rays plus the raw
// disassembly for the this-pointers it dropped: the two operator<<= calls are
// on the rotated back vector and on the (never read again) rotated right
// vector, the operator-= is on field_1B8. Flow: slerp field_1E4 toward the
// desired orientation field_1F4 (FRONT snaps), fixed modes copy it to
// field_214 and return. Otherwise cast a ray from tripod + gCameraOffset
// along the camera's back vector; on a hit within gCameraDistance the camera
// is pulled in to the hit point (field_1D0 eases toward the hit distance by
// halves), unless a second ray shows the player could instead be seen from
// behind, in which case SetCamAngle turns the camera there. Without a hit
// field_1D0 eases toward the full distance by eighths. Items between camera
// and tripod get CItem flag 0x800 (transparent) through field_184, cleared
// again when they drop out. mVel is the position delta of this frame.
void CCamera::MoveToDesiredPos(void)
{
	CBody* pTripod = this->mTripod;
	this->field_180 = 0;
	print_if_false(pTripod != 0 || this->field_100 == 0, "No valid tripod");

	if (G_POST_WATER_EFFECT)
	{
		return;
	}

	CVector oldPos = this->mPos;

	this->field_204 = this->field_1E4;
	if (this->mCameraMode == CAMERAMODE_FRONT)
	{
		this->field_1E4 = this->field_1F4;
		this->field_214 = this->field_1F4;
	}
	else
	{
		Quat_Slerp(this->field_204, this->field_1F4, 1023, this->field_1E4);
	}

	ECameraMode mode = this->mCameraMode;
	if (mode == CAMERAMODE_START || mode == CAMERAMODE_FAR || mode == CAMERAMODE_FRONT)
	{
		this->field_214 = this->field_1E4;
		return;
	}

	CVector back(0, 0, 0);
	CVector backIn(0, 0, -*gCameraDistance);
	CVector right(160, 0, 0);

	MATRIX mat;
	QToM(&this->field_1E4, &mat);
	gte_SetRotMatrix(&mat);
	gte_ldlvl(reinterpret_cast<VECTOR*>(&backIn));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&back));

	CVector dir(0, 0, 0);
	VectorNormal(reinterpret_cast<VECTOR*>(&back), reinterpret_cast<VECTOR*>(&dir));
	back <<= 12;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&right));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&right));
	right <<= 12;

	mode = this->mCameraMode;
	bool hit = false;
	SLineInfo line;
	line.pItem = 0;

	if (mode != CAMERAMODE_USER && mode != CAMERAMODE_LOOKAROUND)
	{
		line.StartCoords = *gCameraOffset + this->field_104;
		line.EndCoords = back + line.StartCoords + (dir << 8);
		M3dColij_InitLineInfo(&line);
		G_CAMERA_COLLISION_CHECK = 1;
		M3dZone_LineToItem(&line, 1);
		G_CAMERA_COLLISION_CHECK = 0;

		if (line.pItem)
		{
			i32 dot = -(dir.vx * line.Normal.vx + dir.vy * line.Normal.vy + dir.vz * line.Normal.vz);
			i32 pullIn = ((224 * (4096 - (abs(dot) >> 12))) >> 12) + 32;
			hit = line.Distance <= *gCameraDistance + pullIn;

			this->field_1B8 = line.Position;
			this->field_1B8 -= pullIn * dir;

			if (hit)
			{
				CPlayer* pPlayer = G_MECHLIST_PLAYER;

				if (line.Normal.vy >= -2600
						&& line.Normal.vy <= 3400
						&& !pPlayer->field_8E8
						&& (pPlayer->mVel.vx | pPlayer->mVel.vz))
				{
					SLineInfo line2;
					line2.StartCoords = line.EndCoords;
					line2.EndCoords = this->field_1B8;
					M3dColij_InitLineInfo(&line2);
					G_CAMERA_COLLISION_CHECK = 1;
					M3dZone_LineToItem(&line2, 1);
					G_CAMERA_COLLISION_CHECK = 0;

					if (line2.pItem
							&& line2.Normal.vy >= -2600
							&& line2.Normal.vy <= 3400
							&& abs(((line.Normal.vx * line2.Normal.vx) >> 12)
								+ ((line.Normal.vy * line2.Normal.vy) >> 12)
								+ ((line.Normal.vz * line2.Normal.vz) >> 12)) < 2048)
					{
						i16 heading = pPlayer->GetEffectiveHeading();

						i32 idx = 2 * (*gCameraLookAngle & 0xFFF);
						CVector behind(0, -word_610C48[idx], word_610C48[idx + 1]);

						SVECTOR angles;
						angles.vx = this->field_234;
						angles.vy = heading;
						angles.vz = this->field_238;
						M3dMaths_RotMatrixYXZ(&angles, &mat);
						gte_SetRotMatrix(&mat);
						gte_ldlvl(reinterpret_cast<VECTOR*>(&behind));
						gte_rtir();
						gte_stlvnl(reinterpret_cast<VECTOR*>(&behind));

						CVector savedBack = back;
						CVector savedDir = dir;

						back = behind * (*gCameraDistance);
						dir = back >> 12;
						VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

						line2.StartCoords = *gCameraOffset + this->field_104;
						line2.EndCoords = back + line.StartCoords;
						M3dColij_InitLineInfo(&line2);
						G_CAMERA_COLLISION_CHECK = 1;
						M3dZone_LineToItem(&line2, 1);
						G_CAMERA_COLLISION_CHECK = 0;

						back = savedBack;
						dir = savedDir;

						if (line2.pItem)
						{
							hit = true;
						}
						else
						{
							this->SetCamAngle(heading, 0);
							hit = false;
						}
					}
				}

				if (hit)
				{
					CVector toHit = this->field_1B8 - line.StartCoords;
					this->field_180 = 1;

					i32 len = toHit.Length();
					this->field_1D4 = len;
					if (this->field_1D0 != len)
					{
						this->field_1D0 += (len - this->field_1D0) / 2;
					}

					toHit >>= 12;
					VectorNormal(reinterpret_cast<VECTOR*>(&toHit), reinterpret_cast<VECTOR*>(&toHit));

					this->mPos = *gCameraOffset + this->field_104 + dir * this->field_1D0;

					pPlayer = G_MECHLIST_PLAYER;
					if (this->field_1D0 >= 200)
					{
						if (pPlayer->field_57C)
						{
							pPlayer->mFlags |= 0x800;
						}
						else
						{
							pPlayer->mFlags &= ~0x800;
						}
					}
					else
					{
						pPlayer->mFlags |= 0x800;
					}
				}
			}
		}
	}

	this->field_214 = this->field_1E4;

	if (!hit)
	{
		if (this->field_100)
		{
			CVector fullBack = back;
			i32 len = fullBack.Length();
			this->field_1D4 = len;
			if (this->field_1D0 != len)
			{
				this->field_1D0 += (len - this->field_1D0) / 8;
			}

			this->mPos = *gCameraOffset + this->field_104 + dir * this->field_1D0;

			if (this->field_1D0 < 200)
			{
				G_MECHLIST_PLAYER->mFlags |= 0x800;
			}
		}
		else
		{
			this->mPos = this->field_104;
		}
	}

	if (this->field_F9)
	{
		i32 i;
		for (i = 0; i < 8; i++)
		{
			CItem* pItem = this->field_184[i];
			if (!pItem)
			{
				break;
			}

			if (pItem->mRegion == gCameraHeldItemRegions[i] && pItem->mModel == gCameraHeldItemModels[i])
			{
				CPlayer* pPlayer = G_MECHLIST_PLAYER;
				if (pPlayer && pPlayer->mHeldObject == pItem)
				{
					continue;
				}
				pItem->mFlags &= ~0x800;
			}

			this->field_184[i] = 0;
		}

		line.StartCoords = this->mPos;
		line.EndCoords = this->field_104;
		M3dColij_InitLineInfo(&line);
		M3dZone_LineToItem(&line, 1);

		i32 count = 0;
		CVector step(0, 0, 4096);
		if (line.pItem)
		{
			MATRIX mat2;
			QToM(&this->field_214, &mat2);
			gte_SetRotMatrix(&mat2);
			gte_ldlvl(reinterpret_cast<VECTOR*>(&step));
			gte_rtir();
			gte_stlvnl(reinterpret_cast<VECTOR*>(&step));

			CItem* pItem = line.pItem;
			if (pItem)
			{
				CItem** pSlot = this->field_184;
				do
				{
					pItem->mFlags |= 0x800;
					gCameraHeldItemRegions[count] = pItem->mRegion;
					gCameraHeldItemModels[count] = pItem->mModel;
					*pSlot = pItem;
					count++;
					pSlot++;
					print_if_false(count < 8, "Overflow");
					if (count >= 8)
					{
						break;
					}
					if (line.Length <= 4)
					{
						break;
					}

					line.StartCoords = line.Position + step * 2;
					M3dColij_InitLineInfo(&line);
					M3dZone_LineToItem(&line, 1);
					pItem = line.pItem;
				}
				while (pItem);
			}
		}
	}

	this->mVel = this->mPos - oldPos;
	this->field_1A4 = 0;
}

// @Ok
// 0x417CB0 (2523 bytes). Functional decomp from Hex-Rays plus the raw
// disassembly for the this-pointers it dropped (the two operator+= calls go
// to field_104 and field_144). The gRenderTest & 0x200 block is the debug
// free camera: it drives gMikeCamera[0] straight from the pad (Left/Right
// yaw, Up/Down move along the view, Shift+Up/Down pitch; 42 and 54 are the
// two shift scancodes) and skips the game camera entirely. The rest is the
// per-frame camera update: tripod position (hook or body position, eased by
// field_130/134/138 sixteenths), tripod and focus motion (field_128/168
// frame counters), zoom, the SetCam* parameter interpolations into
// gWtfCam[29..34] and back into the camXZDist/camYDist/offset globals,
// gCameraOffset from the offsets rotated by the camera yaw, the mode switch,
// MoveToDesiredPos, the shake (three axis quaternions folded into field_214,
// amplitude decayed toward zero, zeroed on sign flip), LoadIntoMikeCamera.
void CCamera::AI(void)
{
	if (gRenderTest & 0x200)
	{
		G_MIKE_CAMERA[0].Style = 0;

		if (G_SCONTROL[0].Left.Pressed)
		{
			G_MIKE_CAMERA[0].Angles.vy = (G_MIKE_CAMERA[0].Angles.vy - 16) & 0xFFF;
		}
		else if (G_SCONTROL[0].Right.Pressed)
		{
			G_MIKE_CAMERA[0].Angles.vy = (G_MIKE_CAMERA[0].Angles.vy + 16) & 0xFFF;
		}

		if (G_SCONTROL[0].Up.Pressed)
		{
			if (PCINPUT_IsKeyPressed(42, 0) || PCINPUT_IsKeyPressed(54, 0))
			{
				G_MIKE_CAMERA[0].Angles.vx = (G_MIKE_CAMERA[0].Angles.vx + 16) & 0xFFF;
			}
			else
			{
				G_MIKE_CAMERA[0].Position.vx += (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vy & 0xFFF)]) >> 12;
				G_MIKE_CAMERA[0].Position.vz += (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vy & 0xFFF) + 1]) >> 12;
				G_MIKE_CAMERA[0].Position.vy -= (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vx & 0xFFF)]) >> 12;
			}
		}
		else if (G_SCONTROL[0].Down.Pressed)
		{
			if (PCINPUT_IsKeyPressed(42, 0) || PCINPUT_IsKeyPressed(54, 0))
			{
				G_MIKE_CAMERA[0].Angles.vx = (G_MIKE_CAMERA[0].Angles.vx - 16) & 0xFFF;
			}
			else
			{
				G_MIKE_CAMERA[0].Position.vx -= (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vy & 0xFFF)]) >> 12;
				G_MIKE_CAMERA[0].Position.vz -= (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vy & 0xFFF) + 1]) >> 12;
				G_MIKE_CAMERA[0].Position.vy += (32 * word_610C48[2 * (G_MIKE_CAMERA[0].Angles.vx & 0xFFF)]) >> 12;
			}
		}

		RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		return;
	}

	if (!this->field_F8)
	{
		return;
	}

	if (G_POST_WATER_EFFECT)
	{
		return;
	}

	*gCameraPos = this->mPos;
	CVector oldTripodPos = this->field_104;

	if (this->field_128 == 0 && this->mTripod && this->field_100 && !this->mTripod->IsDead())
	{
		CVector hookPos(0, 0, 0);
		if (this->field_12C == -1)
		{
			hookPos = this->mTripod->mPos;
		}
		else
		{
			M3dUtils_GetHookPosition(
					reinterpret_cast<VECTOR*>(&hookPos),
					reinterpret_cast<CSuper*>(this->mTripod),
					this->field_12C);
		}
		this->field_104 = hookPos;
	}

	this->field_104.vx = this->field_130 * (this->field_104.vx >> 4) + (oldTripodPos.vx >> 4) * (16 - this->field_130);
	this->field_104.vy = this->field_134 * (this->field_104.vy >> 4) + (oldTripodPos.vy >> 4) * (16 - this->field_134);
	this->field_104.vz = this->field_138 * (this->field_104.vz >> 4) + (oldTripodPos.vz >> 4) * (16 - this->field_138);

	i32 dt = this->mElapsedTicks;

	if (static_cast<u32>(this->field_128) <= static_cast<u32>(dt))
	{
		if (this->field_128)
		{
			this->field_128 = 0;
			this->field_104 = this->field_110;
		}
	}
	else
	{
		this->field_128 -= dt;
		this->field_104 += this->field_11C * this->mElapsedTicks;
	}

	if (static_cast<u32>(this->field_168) <= static_cast<u32>(dt))
	{
		if (this->field_168)
		{
			this->field_168 = 0;
			this->field_144 = this->field_150;
		}
		else
		{
			CBody* pFocus = this->field_13C;
			if (pFocus && this->field_140)
			{
				this->field_144 = pFocus->mPos;
			}
		}
	}
	else
	{
		this->field_168 -= dt;
		this->field_144 += this->field_15C * this->mElapsedTicks;
	}

	u16 zoomFrames = this->field_174;
	if (zoomFrames <= dt)
	{
		if (zoomFrames)
		{
			this->field_174 = 0;
			this->mZoom = this->field_17C;
		}
	}
	else
	{
		this->field_174 = zoomFrames - dt;
		this->mZoom = dt * this->field_178 + this->mZoom;
	}

	if (G_SCONTROL[0].Select.Pressed)
	{
		if (*gCameraSelectReleased)
		{
			*gCameraSelectTriggered = 1;
		}
		*gCameraSelectReleased = 0;
	}
	else
	{
		*gCameraSelectReleased = 1;
	}

	if (G_CAM_XZ_DISTANCE_RELATED <= dt)
	{
		if (G_CAM_XZ_DISTANCE_RELATED)
		{
			G_CAM_XZ_DISTANCE_RELATED = 0;
			G_WTF_CAM[32] = G_CAM_XZ_RELATED_TWO;
		}
	}
	else
	{
		G_CAM_XZ_DISTANCE_RELATED -= dt;
		G_WTF_CAM[32] = G_CAM_XZ_RELATED_THREE * dt + G_CAM_XZ_DIST;
	}

	if (G_CAM_Y_DISTANCE_RELATED <= dt)
	{
		if (G_CAM_Y_DISTANCE_RELATED)
		{
			G_CAM_Y_DISTANCE_RELATED = 0;
			G_WTF_CAM[33] = G_CAM_Y_DISTANCE_RELATED_TWO;
		}
	}
	else
	{
		G_CAM_Y_DISTANCE_RELATED -= dt;
		G_WTF_CAM[33] = G_CAM_Y_DISTANCE_RELATED_THREE * dt + G_CAM_Y_DIST;
	}

	i32 xOffset;
	if (G_CAM_X_OFFSET_TWO <= dt)
	{
		if (G_CAM_X_OFFSET_TWO)
		{
			xOffset = G_CAM_X_OFFSET_ONE;
			G_CAM_X_OFFSET_TWO = 0;
			G_WTF_CAM[29] = xOffset;
		}
		else
		{
			xOffset = G_CAM_X_OFFSET_FOUR;
		}
	}
	else
	{
		G_CAM_X_OFFSET_TWO -= dt;
		xOffset = ((dt * G_CAM_X_OFFSET_THREE) >> 12) + G_CAM_X_OFFSET_FOUR;
		G_WTF_CAM[29] = xOffset;
	}

	i32 yOffset;
	if (G_CAM_Y_OFFSET_TWO <= dt)
	{
		if (G_CAM_Y_OFFSET_TWO)
		{
			yOffset = G_CAM_Y_OFFSET_ONE;
			G_CAM_Y_OFFSET_TWO = 0;
			G_WTF_CAM[30] = yOffset;
		}
		else
		{
			yOffset = G_CAM_Y_OFFSET_FOUR;
		}
	}
	else
	{
		G_CAM_Y_OFFSET_TWO -= dt;
		yOffset = ((dt * G_CAM_Y_OFFSET_THREE) >> 12) + G_CAM_Y_OFFSET_FOUR;
		G_WTF_CAM[30] = yOffset;
	}

	i32 zOffset;
	if (G_CAM_Z_OFFSET_TWO <= dt)
	{
		if (G_CAM_Z_OFFSET_TWO)
		{
			G_CAM_Z_OFFSET_TWO = 0;
			zOffset = G_CAM_Z_OFFSET_ONE;
			G_WTF_CAM[31] = zOffset;
		}
		else
		{
			zOffset = G_CAM_Z_OFFSET_FOUR;
		}
	}
	else
	{
		G_CAM_Z_OFFSET_TWO -= dt;
		zOffset = ((dt * G_CAM_Z_OFFSET_THREE) >> 12) + G_CAM_Z_OFFSET_FOUR;
		G_WTF_CAM[31] = zOffset;
	}

	gCameraOffset->vy = yOffset << 12;
	i32 idx = 2 * (this->field_236 & 0xFFF);
	i32 cosYaw = word_610C48[idx + 1];
	i32 sinYaw = word_610C48[idx];
	gCameraOffset->vx = -(cosYaw * xOffset + sinYaw * zOffset);
	gCameraOffset->vz = sinYaw * xOffset - cosYaw * zOffset;

	i16 yaw;
	if (G_CAMERA_MODE_ONE <= dt)
	{
		if (G_CAMERA_MODE_ONE)
		{
			yaw = G_CAMERA_MODE_TWO;
			G_CAMERA_MODE_ONE = 0;
			this->field_236 = yaw;
		}
		else
		{
			yaw = G_WTF_CAM[34];
		}
	}
	else
	{
		G_CAMERA_MODE_ONE -= dt;
		yaw = (this->field_236 + G_CAMERA_MODE_THREE * static_cast<i16>(this->mElapsedTicks)) & 0xFFF;
		this->field_236 = yaw;
	}

	ECameraMode mode = this->mCameraMode;
	G_CAM_X_OFFSET_FOUR = G_WTF_CAM[29];
	G_CAM_Y_OFFSET_FOUR = G_WTF_CAM[30];
	G_CAM_Z_OFFSET_FOUR = G_WTF_CAM[31];
	G_CAM_XZ_DIST = G_WTF_CAM[32];
	G_CAM_Y_DIST = G_WTF_CAM[33];
	G_WTF_CAM[34] = yaw & 0xFFF;

	if (mode != CAMERAMODE_LOOSE && mode != CAMERAMODE_USER && mode != CAMERAMODE_LOOKAROUND)
	{
		this->field_236 = yaw & 0xFFF;
	}

	if (mode == CAMERAMODE_DEMO)
	{
		i32 y = -G_WTF_CAM[33];
		i32 xz = G_WTF_CAM[32];
		*gCameraDistance = M3dMaths_SquareRoot0(y * y + xz * xz);
		*gCameraLookAngle = ratan2(y, xz);
	}

	*gCameraUnusedOne = 0;
	*gCameraUnusedTwo = 0;

	switch (mode)
	{
		case CAMERAMODE_DEMO:
			this->CM_Normal();
			break;
		case CAMERAMODE_START:
			this->CM_FixedPos();
			break;
		case CAMERAMODE_FAR:
			this->CM_FixedPosAngles();
			break;
		case CAMERAMODE_OVERHEAD:
			this->CM_FixedFocus();
			break;
		case CAMERAMODE_FRONT:
			break;
		case CAMERAMODE_IDLE:
			this->CM_TripodFocus();
			break;
		case CAMERAMODE_LOOSE:
		case CAMERAMODE_USER:
		case CAMERAMODE_LOOKAROUND:
			this->CM_Boss3();
			break;
		default:
			print_if_false(0, "Unknown camera mode in switch!");
			break;
	}

	this->MoveToDesiredPos();
	Utils_CalcAim(&this->mAngles, &this->mPos, &this->field_144);

	if (this->mShakeAmp.vx || this->mShakeAmp.vy || this->mShakeAmp.vz)
	{
		CQuat qz = QFromZRot(this->mShakeAmp.vz * word_610C48[2 * ((G_TTIME * this->mShakeSpeed.vz) & 0xFFF)] / 4096);
		CQuat qx = QFromXRot(this->mShakeAmp.vx * word_610C48[2 * ((G_TTIME * this->mShakeSpeed.vx) & 0xFFF)] / 4096);
		CQuat qy = QFromYRot(this->mShakeAmp.vy * word_610C48[2 * ((G_TTIME * this->mShakeSpeed.vy) & 0xFFF)] / 4096);
		this->field_214 = qy * this->field_1E4 * qx * qz;

		CSVector oldAmp = this->mShakeAmp;

		if (this->mShakeAmp.vx < 0)
		{
			this->mShakeAmp.vx += this->mShakeDecay.vx;
		}
		else
		{
			this->mShakeAmp.vx -= this->mShakeDecay.vx;
		}

		if (this->mShakeAmp.vy >= 0)
		{
			this->mShakeAmp.vy -= this->mShakeDecay.vy;
		}
		else
		{
			this->mShakeAmp.vy += this->mShakeDecay.vy;
		}

		if (this->mShakeAmp.vz >= 0)
		{
			this->mShakeAmp.vz -= this->mShakeDecay.vz;
		}
		else
		{
			this->mShakeAmp.vz += this->mShakeDecay.vz;
		}

		if (static_cast<i16>(oldAmp.vx ^ this->mShakeAmp.vx) < 0)
		{
			this->mShakeAmp.vx = 0;
		}
		if (static_cast<i16>(oldAmp.vy ^ this->mShakeAmp.vy) < 0)
		{
			this->mShakeAmp.vy = 0;
		}
		if (static_cast<i16>(oldAmp.vz ^ this->mShakeAmp.vz) < 0)
		{
			this->mShakeAmp.vz = 0;
		}
	}

	this->LoadIntoMikeCamera();
}

// @Ok
// 0x419430 (939 bytes). Mac: Camera_SelectOptimumViewingNode(ulong, CVector*).
// The vector argument is two points (the caller always passes 2, the assert
// says so). Walks every type-13 trig node between 512 and 4096 (CrapDist) of
// the midpoint, keeps those that see both points within 341 (30 degrees) of
// the midpoint aim in yaw, scores 2048 minus the pitch spread between the two
// points minus 1024 per point that is occluded (two camera-collision rays),
// and moves CameraList to the best node with a zero-frame
// SetFixedPosAnglesMode as it goes. Returns the winning node index, 0 if none.
i32 Camera_SelectOptimumViewingNode(u32 numPoints, CVector *points)
{
	CVector mid(0, 0, 0);
	print_if_false(numPoints == 2, "Error");

	CVector boxMin(-0x7FFFFFFF, -0x7FFFFFFF, -0x7FFFFFFF);
	i32 bestNode = 0;
	i32 bestScore = 0;
	CVector boxMax(0x7FFFFFFF, 0x7FFFFFFF, 0x7FFFFFFF);
	Utils_SetVisibilityInBox(&boxMin, &boxMax, 1, 1);

	if (numPoints == 2)
	{
		mid = points[0] + ((points[1] - points[0]) >> 1);
	}

	i32 i = 1;
	if (G_NUMNODES <= 1)
	{
		return 0;
	}

	do
	{
		if (*G_OFFSETLIST[i] == 13)
		{
			CVector nodePos(0, 0, 0);
			Trig_GetPosition(&nodePos, i);

			i32 dist = spidey::math::approximateDistance(nodePos, mid);
			if (dist >= 512 && dist <= 4096)
			{
				CSVector aimMid(0, 0, 0);
				CSVector aimFirst(0, 0, 0);
				CSVector aimSecond(0, 0, 0);
				Utils_CalcAim(&aimMid, &nodePos, &mid);
				Utils_CalcAim(&aimFirst, &nodePos, &points[0]);

				if (CalcTheta(aimMid.vy, aimFirst.vy) <= 341)
				{
					Utils_CalcAim(&aimSecond, &nodePos, &points[1]);

					if (CalcTheta(aimMid.vy, aimSecond.vy) <= 341)
					{
						i32 score = 2048 - abs(CalcTheta(aimFirst.vx, aimSecond.vx));

						SLineInfo line;
						line.StartCoords = nodePos;
						line.EndCoords = points[0];
						M3dColij_InitLineInfo(&line);
						G_CAMERA_COLLISION_CHECK = 1;
						M3dZone_LineToItem(&line, 1);
						if (line.pItem)
						{
							score -= 1024;
						}

						line.EndCoords = points[1];
						M3dColij_InitLineInfo(&line);
						M3dZone_LineToItem(&line, 1);
						G_CAMERA_COLLISION_CHECK = 0;
						if (line.pItem)
						{
							score -= 1024;
						}

						if (score > bestScore)
						{
							bestNode = i;
							bestScore = score;
							G_CAMERA_LIST->SetFixedPosAnglesMode(nodePos, aimMid, 0);
						}
					}
				}
			}
		}

		i++;
	}
	while (i < G_NUMNODES);

	return bestNode;
}

void validate_CCamera(void){
	VALIDATE_SIZE(CCamera, 0x2F4);


	VALIDATE(CCamera, field_F8, 0xF8);
	VALIDATE(CCamera, field_F9, 0xF9);
	VALIDATE(CCamera, mTripod, 0xFC);
	VALIDATE(CCamera, field_100, 0x100);
	VALIDATE(CCamera, field_104, 0x104);

	VALIDATE(CCamera, field_110, 0x110);
	VALIDATE(CCamera, field_11C, 0x11C);

	VALIDATE(CCamera, field_128, 0x128);
	VALIDATE(CCamera, field_12C, 0x12C);
	VALIDATE(CCamera, field_130, 0x130);
	VALIDATE(CCamera, field_134, 0x134);
	VALIDATE(CCamera, field_138, 0x138);
	VALIDATE(CCamera, field_13C, 0x13C);
	VALIDATE(CCamera, field_140, 0x140);

	VALIDATE(CCamera, field_144, 0x144);

	VALIDATE(CCamera, field_150, 0x150);
	VALIDATE(CCamera, field_15C, 0x15C);
	VALIDATE(CCamera, field_168, 0x168);
	VALIDATE(CCamera, field_16C, 0x16C);
	VALIDATE(CCamera, mZoom, 0x170);

	VALIDATE(CCamera, field_174, 0x174);
	VALIDATE(CCamera, field_178, 0x178);
	VALIDATE(CCamera, field_17C, 0x17C);
	VALIDATE(CCamera, field_180, 0x180);
	VALIDATE(CCamera, field_184, 0x184);
	VALIDATE(CCamera, field_1A4, 0x1A4);
	VALIDATE(CCamera, field_1A8, 0x1A8);
	VALIDATE(CCamera, field_1AC, 0x1AC);
	VALIDATE(CCamera, field_1B0, 0x1B0);
	VALIDATE(CCamera, field_1B4, 0x1B4);
	VALIDATE(CCamera, field_1B8, 0x1B8);
	VALIDATE(CCamera, field_1C8, 0x1C8);
	VALIDATE(CCamera, field_1CC, 0x1CC);
	VALIDATE(CCamera, field_1CE, 0x1CE);
	VALIDATE(CCamera, field_1D0, 0x1D0);
	VALIDATE(CCamera, field_1D4, 0x1D4);
	VALIDATE(CCamera, field_1D8, 0x1D8);
	VALIDATE(CCamera, field_1DC, 0x1DC);
	VALIDATE(CCamera, field_1E0, 0x1E0);

	VALIDATE(CCamera, field_1E4, 0x1E4);

	VALIDATE(CCamera, field_1F4, 0x1F4);

	VALIDATE(CCamera, field_204, 0x204);

	VALIDATE(CCamera, field_214, 0x214);
	VALIDATE(CCamera, field_224, 0x224);
	VALIDATE(CCamera, field_228, 0x228);
	VALIDATE(CCamera, field_22C, 0x22C);
	VALIDATE(CCamera, field_230, 0x230);
	VALIDATE(CCamera, field_234, 0x234);
	VALIDATE(CCamera, field_236, 0x236);
	VALIDATE(CCamera, field_238, 0x238);

	VALIDATE(CCamera, field_23A, 0x23A);

	VALIDATE(CCamera, field_23C, 0x23C);
	VALIDATE(CCamera, field_240, 0x240);
	VALIDATE(CCamera, field_244, 0x244);
	VALIDATE(CCamera, field_248, 0x248);
	VALIDATE(CCamera, field_24C, 0x24C);

	VALIDATE(CCamera, field_258, 0x258);

	VALIDATE(CCamera, mCollisionRayLR, 0x264);
	VALIDATE(CCamera, mCollisionRayBack, 0x268);
	VALIDATE(CCamera, mCollisionAngLR, 0x26C);
	VALIDATE(CCamera, mCollisionAngBack, 0x26E);

	VALIDATE(CCamera, mShakeAmp, 0x270);
	VALIDATE(CCamera, mShakeDecay, 0x276);
	VALIDATE(CCamera, mShakeSpeed, 0x27A);

	VALIDATE(CCamera, field_280, 0x280);
	VALIDATE(CCamera, field_284, 0x284);

	VALIDATE(CCamera, field_290, 0x290);

	VALIDATE(CCamera, mCameraMode, 0x2A0);
	VALIDATE(CCamera, field_2A4, 0x2A4);

	VALIDATE(CCamera, field_2A8, 0x2A8);
	VALIDATE(CCamera, field_2AC, 0x2AC);

	VALIDATE(CCamera, field_2B0, 0x2B0);

	VALIDATE(CCamera, field_2BC, 0x2BC);
	VALIDATE(CCamera, field_2C0, 0x2C0);


	VALIDATE(CCamera, field_2C4, 0x2C4);

	VALIDATE(CCamera, field_2D4, 0x2D4);

	VALIDATE(CCamera, field_2E4, 0x2E4);

	VALIDATE(CCamera, field_2E8, 0x2E8);

}

void validate_SCamera(void)
{
	VALIDATE_SIZE(SCamera, 0x54);

	VALIDATE(SCamera, Style, 0x0);
	VALIDATE(SCamera, Position, 0x4);
	VALIDATE(SCamera, Focus, 0x14);
	VALIDATE(SCamera, Distance, 0x24);
	VALIDATE(SCamera, Height, 0x28);
	VALIDATE(SCamera, Angles, 0x2C);
	VALIDATE(SCamera, Transform, 0x34);
}

void validate_SViewport(void)
{
	VALIDATE_SIZE(SViewport, 0x10);

	VALIDATE(SViewport, Zoom, 0xC);
	VALIDATE(SViewport, field_E, 0xE);
}


// @Bogus
void patch_camera(void)
{
	PATCH_PUSH_RET_POLY(0x00415EE0, CCamera::CCamera, "??0CCamera@@QAE@PAVCBody@@@Z");
	PATCH_PUSH_RET_POLY(0x004162A0, CCamera::~CCamera, "??1CCamera@@UAE@XZ");
	PATCH_PUSH_RET(0x00416300, CCamera::SetTripodInterpolation);
	PATCH_PUSH_RET(0x00416370, CCamera::SetFixedPosMode);
	// SetFixedPosAnglesMode is overloaded since the euler-angle version
	// (0x4164F0) was added, so the hook needs the mangled name to pick the
	// CQuat overload that lives at 0x416410.
	PATCH_PUSH_RET_POLY(0x00416410, CCamera::SetFixedPosAnglesMode, "?SetFixedPosAnglesMode@CCamera@@QAEXPAVCVector@@PAVCQuat@@G@Z");
	PATCH_PUSH_RET(0x004166C0, CCamera::SetFixedFocusMode);
	PATCH_PUSH_RET(0x00416720, CCamera::PushMode);
	PATCH_PUSH_RET(0x00416780, CCamera::PopMode);
	PATCH_PUSH_RET(0x004167F0, CCamera::SetMode);
	PATCH_PUSH_RET(0x00416840, CCamera::SetCollisionRayLR);
	PATCH_PUSH_RET(0x00416850, CCamera::SetCollisionRayBack);
	PATCH_PUSH_RET(0x00416860, CCamera::SetCollisionAngLR);
	PATCH_PUSH_RET(0x00416870, CCamera::SetCollisionAngBack);
	PATCH_PUSH_RET(0x00416880, CCamera::Shake);
	PATCH_PUSH_RET(0x00416A00, CCamera::GetPosition);
	PATCH_PUSH_RET(0x00416A20, CCamera::LoadIntoMikeCamera);
	PATCH_PUSH_RET(0x00416AA0, CCamera::SetStartPosition);
	PATCH_PUSH_RET(0x00417710, CCamera::SetTripodMotion);
	PATCH_PUSH_RET(0x004178E0, CCamera::SetCamAngle);
	PATCH_PUSH_RET(0x004179E0, CCamera::GetCamXZDistance);
	PATCH_PUSH_RET(0x004179F0, CCamera::SetCamXZDistance);
	PATCH_PUSH_RET(0x00417A60, CCamera::GetCamYDistance);
	PATCH_PUSH_RET(0x00417A70, CCamera::SetCamYDistance);
	PATCH_PUSH_RET(0x00417AE0, CCamera::SetCamXOffset);
	PATCH_PUSH_RET(0x00417B60, CCamera::SetCamYOffset);
	PATCH_PUSH_RET(0x00417BE0, CCamera::SetCamZOffset);
	PATCH_PUSH_RET(0x00417C50, CCamera::SetZoom);
	PATCH_PUSH_RET(0x00417CA0, CCamera::GetZoom);
	PATCH_PUSH_RET(0x004186B0, CCamera::CM_TripodFocus);
	PATCH_PUSH_RET(0x00418800, CCamera::CM_FixedPosAngles);
	PATCH_PUSH_RET(0x004189A0, CCamera::CM_FixedPos);
	PATCH_PUSH_RET(0x00418E00, CCamera::CM_Normal);
}
