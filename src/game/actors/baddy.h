#pragma once

#ifndef BADDY_H
#define BADDY_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "engine/world/stateflags.h"

union IntToBytes
{
	i32 Int;
	u8 Bytes[4];
};

class CMessage;
class CAIProc;
class CBaddy : public CSuper {

public:
	EXPORT CBaddy(void);
	EXPORT ~CBaddy(void);
	EXPORT int DistanceToPlayer(int a2);
	EXPORT int TrapWeb(void);
	EXPORT void CleanUpMessages(i32, i32);
	EXPORT u16 CheckStateFlags(SStateFlags *, int);
	EXPORT int YawTowards(int, int);
	EXPORT int RunTimer(int*);
	EXPORT int PathCheckGuts(CVector*, CVector*, CVector*, int);
	EXPORT int PathCheck(CVector*, CVector*, CVector*, int);
	EXPORT CBody* StruckGameObject(int, int);
	EXPORT void MarkAIProcList(int, int, int);
	EXPORT void Neutralize(void);
	EXPORT int TugWeb(void);
	EXPORT int MakeSpriteRing(CVector*);
	EXPORT void GetLocalPos(CVector *, CVector *, CSVector *);
	EXPORT int SetHeight(int, int, int);
	EXPORT void SendDeathPulse(void);
	EXPORT int Die(int);
	EXPORT void CleanUpAIProcList(i32);
	EXPORT int BumpedIntoSpidey(int);
	EXPORT int ShouldFall(int, int);
	EXPORT int CheckSightCone(int, int, int, int, CBody*);
	EXPORT void ParseScript(u16*);
	EXPORT void DoPhysics(int);
	EXPORT void ElasticCollision(CBody*, CBody*, CVector&, i32);
	EXPORT void Baddy_SendSignal(void);
	EXPORT int SmackSpidey(int, CVector*, int, int);
	EXPORT void RunAppropriateAnim(void);
	EXPORT i32 GetNextWaypoint(void);
	EXPORT i32 AddPointToPath(CVector*, i32);
	EXPORT void GetWaypointNearTarget(CVector*, i32, i32, CVector*);
	EXPORT i16 GetScriptValue(void);

	EXPORT virtual int PlayerIsVisible();
	EXPORT virtual void CreateCombatImpactEffect(CVector*, i32);
	EXPORT virtual u8 TugImpulse(CVector*, CVector*, CVector*);
	EXPORT virtual void Victorious(void);
	EXPORT virtual void SetParamByIndex(i32, i32);
	EXPORT virtual u8 Grab(CVector*);
	EXPORT virtual void Shouldnt_DoPhysics_Be_Virtual(void);
	EXPORT virtual void UnknownCBaddyFunctionFive(int);
	EXPORT virtual CBaddy* GetClosest(int, int);
	EXPORT virtual int ExecuteCommand(u16);
	EXPORT virtual void SetVariable(u16);
	EXPORT virtual i16 GetVariable(u16);

	// Read (never written) by CThug::DetermineFightState (0x4d9b02) as a
	// live gate flag ("if (field_1A4 != 0) return false"). Carved out of
	// what was previously a blind 4 byte PADDING right before field_1A8;
	// same byte range, so no other field shifts.
	i32 field_1A4;

	CVector field_1A8[6];

	i32 field_1F0;
	i32 field_1F4;
	i32 field_1F8;
	u16 field_1FC;
	i16 field_1FE;
	PADDING(1);

	i32 field_204;
	i32 field_208;

	u8 field_20C;

	PADDING(0x1);
	u8 field_20E;

	u8 field_20F;
	u8 field_210;
	u8 field_211;
	u8 field_212;
	u8 field_213;

	u16 field_214;

	i16 field_216;
	i32 field_218;

	PADDING(0x1);

	u8 field_21D;

	i16 field_21E;

	//i16 registerArr[6];
	i16 field_220;
	i16 realRegisterArr[6];

	PADDING(2);

	i32 field_230;
	u8 field_234;

	PADDING(3);

	// script frame-wait counter (CScriptOnlyBaddy::AI 0x407840 counts it
	// down once per frame before running the next script command).
	i32 field_238;

	i32 field_23C;

	CVector field_240;
	i16 *field_24C;

	// script GOTO/label table: found while decompiling ExecuteCommand
	// (0x4050B0). C_SET_SCRIPT_LABEL ("Label exceeds MAX_BADDY_LABELS",
	// bounds-checked against 8) stores the current field_24C into
	// labelArr[index]; C_GOTO/C_GOTO_AND_something read it back. Fully
	// fills what used to be a 32-byte PADDING block here (0x250, right
	// after field_24C, ending exactly at attributeArr's validated 0x270),
	// so this is a real field, not a guess about the gap's existence.
	i16 *labelArr[8];

	i16 attributeArr[6];
	CVector field_27C;

	i32 field_288;
	
	CAIProc *mAIProcList;
	CMessage* pMessage;

	IntToBytes field_294;
	IntToBytes field_298;
	i32 field_29C;

	i32 field_2A0;
	CBody *field_2A4;
	i32 field_2A8;
	i32 field_2AC;
	i32 field_2B0;

	i32 field_2B4;
	CVector field_2B8;

	// CBaddy::ExecuteCommand 0x4280 (0x406125): mPos after the timed
	// physics steps, a CVector (field_2B8 is mPos before)
	CVector field_2C4;


	CVector field_2D0;

	// CBaddy::ExecuteCommand 0x4280: mAngles before (0x406078) and after
	// (0x406141) the timed physics steps, two CSVectors
	CSVector field_2DC;
	CSVector field_2E2;
	CSVector field_2E8;
	PADDING(0x2F0 - 0x2E8 - sizeof(CSVector));


	i32 field_2F0;
	i32 field_2F4;
	i32 field_2F8;

	CVector field_2FC;
	i32 field_308;
	i32 field_30C;

	i32 field_310;
	i16 field_314;
	PADDING(2);

	i32 field_318;
	SStateFlags field_31C;
	i32 dumbAssPad;
};


class CScriptOnlyBaddy : public CBaddy {

public:
	// Owned object the destructor deletes through vtable slot 0 (0x407789:
	// `mov ecx,[edi+324h]; test; mov eax,[ecx]; push 1; call [eax]`). Nothing
	// decompiled writes it yet, so its concrete class is unknown; CClass is
	// the smallest base with that virtual destructor. Zero from CItem's
	// operator new, like in the original.
	CClass* field_324;

	i32 field_328;
	i16 field_32C;
	i16 field_32E;

	EXPORT CScriptOnlyBaddy(i16*, i32);
	EXPORT virtual ~CScriptOnlyBaddy(void);

	// The three overrides the original vtable (0x53B2E8) has besides the
	// destructor: slot 2, 14 and 15. Without them the class ran CBody's
	// empty AI, so a level's cutscene actor never executed its script
	// (found 2026-09-03 in the standalone build: the level 1 intro never
	// moved the camera, and Spidey fell off the spawn point).
	EXPORT virtual void AI(void);
	EXPORT virtual int ExecuteCommand(u16);
	EXPORT virtual void SetVariable(u16);
};

EXPORT CBaddy* FindBaddyOfType(int);
EXPORT extern CBaddy* BaddyList;
//#define G_BADDY_LIST (BaddyList)
#define G_BADDY_LIST (*reinterpret_cast<CBaddy**>(0x0056E990))

EXPORT extern CBody* ControlBaddyList;
//#define G_CONTROL_BADDY_LIST (ControlBaddyList)
#define G_CONTROL_BADDY_LIST (*reinterpret_cast<CBody**>(0x0056E994))

EXPORT extern i32 gAttackRelated;
//#define G_ATTACK_RELATED (gAttackRelated)
#define G_ATTACK_RELATED (*reinterpret_cast<i32*>(0x005FCCF4))

EXPORT extern i32 gBossRelated;

// 0x0060D9E0 (gTrajectoryVector in the maintainer's IDB). Defined here, read
// by lizman.cpp, platform.cpp, wire.cpp and spid_ai0.cpp, and written by code
// we do not hook, so it has to be the exe's copy. No extern declaration here
// on purpose: those files already declare it without EXPORT and MSVC6
// rejects the storage class mismatch.
//#define G_TRAJECTORY_VECTOR (gTrajectoryVector)
#define G_TRAJECTORY_VECTOR (*reinterpret_cast<CSVector*>(0x0060D9E0))

// 0x00682C18 (gAttackFlagRelated in the maintainer's IDB). One bitmask shared
// by cop.cpp and thug.cpp: both ClearAttackFlags bodies clear a bit in it and
// both SetAttacker bodies read it. Our sources used to keep a private static
// in each file, so the two halves could never see each other. It lives here
// because both cop.h and thug.h include this header.
EXPORT extern u8 gAttackFlagRelated;
//#define G_ATTACK_FLAG_RELATED (gAttackFlagRelated)
#define G_ATTACK_FLAG_RELATED (*reinterpret_cast<u8*>(0x00682C18))

EXPORT extern u8 gObjFileRegion;
//#define G_OBJ_FILE_REGION (gObjFileRegion)
#define G_OBJ_FILE_REGION (*reinterpret_cast<u8*>(0x006B3824))

void validate_CBaddy(void);
void validate_CScriptOnlyBaddy(void);
void validate_SStateFlags(void);

void patch_baddy(void);

#endif
