#pragma once

#ifndef CAMERA_H
#define CAMERA_H

#include "engine/rendering/ps2funcs.h"
#include "engine/world/ob.h"
#include "core/fixed_vector.h"
#include "core/quaternion.h"

struct SViewport
{
	PADDING(0xC);

	u16 Zoom;
	u16 field_E;
};

struct SCamera
{
	u32 Style;
	VECTOR Position;
	VECTOR Focus;
	i32 Distance;
	i32 Height;
	SVECTOR Angles;
	MATRIX Transform;
};

enum ECameraMode {
	CAMERAMODE_NOTHING          = 0,
	CAMERAMODE_NORMAL           = 1,
	CAMERAMODE_NO_BIG_AIR       = 2,
	CAMERAMODE_DEMO             = 3,
	CAMERAMODE_START            = 4,
	CAMERAMODE_FAR              = 5,
	CAMERAMODE_OVERHEAD         = 6,
	CAMERAMODE_FRONT            = 7,
	CAMERAMODE_IDLE             = 8,
	CAMERAMODE_FLYING           = 9,
	CAMERAMODE_FUNKYFLYING      = 10,
	CAMERAMODE_ROLLERCOASTER    = 11,
	CAMERAMODE_PAN              = 12,
	CAMERAMODE_ITSYLOOKDOWN     = 13,
	CAMERAMODE_ITSYLOOKUP       = 14,
	CAMERAMODE_LOOSE            = 15,
	CAMERAMODE_USER             = 16,
	CAMERAMODE_LOOKAROUND       = 17,
	CAMERAMODE_UPSIDETEST       = 18,
	CAMERAMODE_BOSSBEAST        = 19,
	CAMERAMODE_BOSSWAR          = 20,
	CAMERAMODE_BOSSTANK         = 21,
	CAMERAMODE_DEBUG            = 22,
	CAMERAMODE_COMPETITIONINTRO = 23,
};

enum EShakeType
{
	CAMERASHAKE_BIG    = 0,
	CAMERASHAKE_MEDIUM = 1,
	CAMERASHAKE_SMALL  = 2,

	// @FIXME
	CAMERASHAKE_UNK  = 3,
};

enum ECameraModeIncDir
{
	ASCENDING  = 0,
	DESCENDING = 1,
};

class CCamera : public CBody {
public:
	EXPORT CCamera(CBody*);
	EXPORT virtual ~CCamera(void);

	// 0x417CB0, the per-frame driver (CBody::AI override): debug free camera,
	// tripod/focus/zoom interpolation, the SetCam* parameter interpolations,
	// dispatch to the CM_* mode handler, MoveToDesiredPos, shake, then
	// LoadIntoMikeCamera.
	EXPORT virtual void AI(void);
	// 0x416B10, turns field_104 (tripod) + gCameraOffset + the orientation
	// into mPos, pulling the camera in on collision.
	EXPORT void MoveToDesiredPos(void);
	// 0x418C40 and 0x4192F0, the two mode handlers that were still missing.
	EXPORT void CM_FixedFocus(void);
	EXPORT void CM_Boss3(void);
	// 0x4164F0, the euler-angle overload (Mac: SetFixedPosAnglesMode(CVector &,
	// CSVector &, ushort)). Only caller: Camera_SelectOptimumViewingNode.
	EXPORT void SetFixedPosAnglesMode(CVector &, CSVector &, u16);

	EXPORT void SetFixedFocusMode(CVector *, u16, u16);
	EXPORT i32 SetMode(ECameraMode mode);
	EXPORT void SetCollisionRayLR(i32);
	EXPORT void SetCollisionRayBack(i32);
	EXPORT void SetCollisionAngLR(i16);
	EXPORT void SetCollisionAngBack(i16);
	EXPORT void SetZoom(i32, u16);
	EXPORT i32 GetZoom(void) const;
	EXPORT void PushMode(void);
	EXPORT void PopMode(void);
	EXPORT void CM_Normal(void);
	EXPORT void SetStartPosition(void);
	EXPORT void SetFixedPosMode(CVector &a2, u16 a3);
	EXPORT void CM_FixedPosAngles(void);
	EXPORT void CM_FixedPos(void);
	EXPORT void SetCamYDistance(i16, u16);
	EXPORT void SetCamAngle(i16, u16);
	EXPORT void GetPosition(CVector &);
	EXPORT void Shake(CVector&, EShakeType);
	EXPORT void SetTripodInterpolation(i32, i32, i32);
	EXPORT void SetFixedPosAnglesMode(CVector *, CQuat *, u16);
	EXPORT void LoadIntoMikeCamera(void);
	EXPORT void CM_TripodFocus(void);
	EXPORT i16 GetCamXZDistance(void);
	EXPORT void SetCamXZDistance(u16, u16);
	EXPORT i16 GetCamYDistance(void);
	EXPORT void SetCamXOffset(i16,u16);
	EXPORT void SetCamYOffset(i16,u16);
	EXPORT void SetCamZOffset(i16,u16);
	EXPORT void SetTripodMotion(const CVector &,u32);


	PADDING(4);

	u8 field_F8;
	u8 field_F9;
	CBody* mTripod;
	u8 field_100;

	CVector field_104;

	CVector field_110;

	CVector field_11C;

	i32 field_128;
	i32 field_12C;
	i32 field_130;
	i32 field_134;
	i32 field_138;
	CBody* field_13C;
	u8 field_140;
	CVector field_144;

	// focus interpolation, driven by CCamera::AI: field_144 moves by
	// field_15C * field_80 per frame until field_168 frames are used up, then
	// snaps to field_150 (0x418041..0x418091, CVector operator+= on
	// &field_144). Used to be six i32s.
	CVector field_150;
	CVector field_15C;
	i32 field_168;
	i32 field_16C;
	i32 mZoom;

	i16 field_174;
	PADDING(2);

	i32 field_178;
	i32 field_17C;

	// set to 1 by MoveToDesiredPos when the camera ray hit something and the
	// camera was pulled in, cleared at its start (byte stores at 0x416B1C and
	// 0x41730B, so u8 and not the i32 it used to be declared as).
	u8 field_180;
	PADDING(3);

	// eight CItem* the camera is currently holding on to (each of them has
	// CItem flag 0x800 set). Display (main.cpp) walks the eight once a frame
	// while a scorpion is on the baddy list and drops every entry that is not
	// the player's mHeldObject, clearing 0x800 as it goes. Carved out of the
	// blind PADDING that used to cover 0x180 to 0x1A8; same byte range, so
	// nothing after it moves.
	CItem* field_184[8];

	// cleared (byte store, 0x4176F7) at the end of every MoveToDesiredPos.
	u8 field_1A4;
	PADDING(3);


	i32 field_1A8;
	i32 field_1AC;
	i32 field_1B0;
	i32 field_1B4;
	// where the camera ray hit, pulled back along the ray a little
	// (MoveToDesiredPos, CVector operator-= on &field_1B8 at 0x416EEF).
	// Used to be three i32s.
	CVector field_1B8;

	PADDING(4);


	i32 field_1C8;
	i16 field_1CC;
	i16 field_1CE;

	// MoveToDesiredPos: field_1D4 is the length of the tripod-to-camera
	// vector this frame, field_1D0 eases toward it (by 1/2 after a collision,
	// 1/8 otherwise) and is the distance the camera is actually placed at.
	i32 field_1D0;
	i32 field_1D4;


	i32 field_1D8;
	i32 field_1DC;
	i32 field_1E0;

	CQuat field_1E4;

	CQuat field_1F4;

	CQuat field_204;

	CQuat field_214;

	

	i32 field_224;
	i32 field_228;
	i32 field_22C;
	i32 field_230;
	i16 field_234;
	i16 field_236;
	i16 field_238;
	i16 field_23A;

	u8 field_23C;
	i32 field_240;
	i32 field_244;
	i32 field_248;
	CVector field_24C;

	CVector field_258;

	i32 mCollisionRayLR;
	i32 mCollisionRayBack;

	i16 mCollisionAngLR;
	i16 mCollisionAngBack;

	CSVector mShakeAmp;

	CFriction mShakeDecay;

	CSVector mShakeSpeed;

	ECameraMode field_280;

	CVector field_284;

	CQuat field_290;

	ECameraMode mCameraMode;

	i32 field_2A4;
	i32 field_2A8;

	u8 field_2AC;


	CVector field_2B0;

	i32 field_2BC;

	i32 field_2C0;

	CQuat field_2C4;
	CQuat field_2D4;

	i16 field_2E4;

	CVector field_2E8;

};

EXPORT i16 CalcTheta(i16, i16);
// 0x419430. Picks the type-13 trig node that sees both points best and moves
// CameraList there (SetFixedPosAnglesMode); returns the node index, 0 if none.
EXPORT i32 Camera_SelectOptimumViewingNode(u32, CVector *);
EXPORT extern CCamera *CameraList;
EXPORT extern SViewport gViewport;
EXPORT extern SCamera gMikeCamera[2];

// These three are read from a dozen other .cpp files, so the macros live here
// rather than in camera.cpp (one definition per shared global).  In the DLL
// build the exe still owns the camera (CCamera::AI and friends are decompiled
// but not hooked), so hooked code has to share the exe's memory, not our own
// copy.
// Addresses confirmed in the disassembly: CCamera::CCamera pushes 0x0056F3B8
// as &CameraList, Init_Cleanup writes gViewport.field_E at 0x0054D49E, and
// LoadIntoMikeCamera writes gMikeCamera[0].Position at 0x0056F1B4.
//#define G_CAMERA_LIST (CameraList)
#define G_CAMERA_LIST (*reinterpret_cast<CCamera**>(0x0056F3B8))
//#define G_VIEWPORT (gViewport)
#define G_VIEWPORT (*reinterpret_cast<SViewport*>(0x0054D490))
//#define G_MIKE_CAMERA (gMikeCamera)
#define G_MIKE_CAMERA (reinterpret_cast<SCamera*>(0x0056F1B0))

void validate_CCamera(void);
void validate_SCamera(void);
void validate_SViewport(void);

void patch_camera(void);

#endif
