#include "game/levels/training.h"
#include "game/player/spidey.h"
#include "engine/world/trig.h"

#include "game/objects/level_one_bomb.h"
#include "game/ui/mess.h"
#include "game/ui/pshell.h"
#include "game/ui/shell.h"
#include "game/ui/front.h"
#include <cstdio>

// @Ok
EXPORT u8 gTrainingFlags[3];

// @Ok
EXPORT i32 gTrainingStuff[8];

// @Ok
// @Matching
void Training_SpideyDied(const u32*, u32*)
{
	gTrainingFlags[0] = 1;
}

// game-memory counter, incremented on every enemy kill notification.
// Already used file-local under this name in trig.cpp and spidey.cpp
// (0x60CFBC).
static i32 * const gKillNotifyCallCount = (i32*)0x0060CFBC;

// tentative name/address: read-only game-memory Y anchor shared with the
// l2a1 "scorpion approach" bar (gL2A1TrainingBarBaseY in l2a1lsc.cpp,
// 0x60F76C, right before gBombDieRelatedOne in the same state block).
static i32 * const gTrainingBarBaseY = (i32*)0x0060F76C;

// already used as a file-local #define in pshell.cpp (0x55129C, the score
// value PShell_EndTrainingInit reads when it builds the end-of-training
// screen). Duplicated here as a plain global for the same reason repo.md
// allows duplicating static address globals across files.
#define gTrainingScore (*reinterpret_cast<i32*>(0x0055129C))

// already used as a file-local #define in pshell.cpp (0x551288). Duplicated
// here for the same reason as gTrainingScore above.
#define gTrainingSeconds (*reinterpret_cast<i32*>(0x00551288))

// tentative name/address: byte right after gWhatIf (0x60CFC5, ob.cpp) and
// right before gKillNotifyCallCount (0x60CFBC is before both; real order
// is gWhatIf 0x60CFC5, this byte 0x60CFC6). Set to 0 at the start of a
// time-attack style training exercise and checked/consumed at its end;
// guess: some other system sets it to 1 to signal the time-attack run is
// complete. Not in idb_globals.txt.
static u8 * const gTimeAttackComplete = (u8*)0x0060CFC6;

// helper matching LABEL_103 in the original disassembly: clear the bomb/
// training countdown flags and hand off to the end-of-training screen.
// @Bogus
static void Training_EndExercise(void)
{
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;
	gBombDieRelatedOne = 0;
	PShell_EndTrainingInit();
}

// @Ok
// address 0x4ddcf0 (IDA, not in names.json/tools/functions). Functional
// decomp, not byte matched. This is a dispatcher on gTrainingStuff[0] (set
// from Trig_GetLevelID() in RelocatableModuleInit): each training exercise
// uses its own gTrainingStuff[0] value range to show its own on-screen
// readout and end condition, sharing gTrainingStuff[1..3] as scratch state
// (countdown-armed flag, "ending" flag, timestamp). Same pattern as
// gBombAIRelated/gBombDieRelatedOne/Two/gBombDieTimerRelated being reused
// across bomb and training levels.
// Text is built with sprintf here instead of the original's manual
// byte-poked string literal plus digit patching; the displayed text is the
// same either way, this is a source-level simplification only.
void Training_MonitorLevel(const u32*, u32*)
{
	if ((u32)gTrainingStuff[0] > 0x1303)
	{
		if (gTrainingStuff[0] == 0x1501)
		{
			i32 score;
			if (G_MECHLIST_PLAYER)
			{
				score = G_MECHLIST_PLAYER->field_568;
				gTrainingScore = score;
			}
			else
			{
				score = gTrainingScore;
			}

			if (gTrainingStuff[1])
			{
				if (gTrainingStuff[1] == 1 && gBombAIRelated == 0)
				{
					Training_EndExercise();
					return;
				}
			}
			else if (G_MECHLIST_PLAYER && G_MECHLIST_PLAYER->field_E18 == 0)
			{
				gBombAIRelated = 3600;
				gBombDieRelatedOne = 1;
				gBombDieRelatedTwo = 1;
				gBombDieTimerRelated = G_TIMER_RELATED;
				gTrainingStuff[1] = 1;
			}

			char text[16];
			sprintf(text, "Score: %c%02d", score < 0 ? '-' : ' ', my_abs(score));

			Mess_SetScale(256);
			Mess_SetTextJustify(1);
			Mess_DrawText(348, *gTrainingBarBaseY + 36, text, 0, 0x1000);
			return;
		}

		if (gTrainingStuff[0] != 0x1601)
		{
			if (gTrainingStuff[0] != 0x1701)
				return;

			if (gTrainingStuff[1])
			{
				if (gTrainingStuff[1] == 1 && gBombAIRelated == 0)
				{
					gTrainingScore = G_MECHLIST_PLAYER->field_5D0;
					Training_EndExercise();
					return;
				}
			}
			else if (G_MECHLIST_PLAYER && G_MECHLIST_PLAYER->field_E18 == 0)
			{
				gBombDieRelatedOne = 1;
				gBombDieRelatedTwo = 1;
				gBombAIRelated = 60 * gTrainingSeconds;
				gBombDieTimerRelated = G_TIMER_RELATED;
				gTrainingStuff[1] = 1;
			}

			if (G_MECHLIST_PLAYER->field_5D0 > 7500 && (G_SAVE_GAME.field_80 & 0x100) == 0)
			{
				if (NumNodes > 1)
				{
					i32 node = 1;
					u8 found = 0;

					while (true)
					{
						if (*G_OFFSETLIST[node] == 1)
						{
							CVector v;
							Trig_GetPosition(&v, node);

							if (v.vx == 0 && v.vy == 0 && v.vz == 0)
							{
								found = 1;
								break;
							}
						}

						if (++node >= NumNodes)
							break;
					}

					if (found)
						Trig_SendPulseToNode(node);
				}

				G_SAVE_GAME.field_80 |= 0x100;
			}

			i32 score = G_MECHLIST_PLAYER->field_5D0;
			char text[16];
			sprintf(text, "Score: %4d", score);

			Mess_SetScale(256);
			Mess_SetTextJustify(1);
			Mess_DrawText(348, *gTrainingBarBaseY + 36, text, 0, 0x1000);

			if (!G_MECHLIST_PLAYER || G_MECHLIST_PLAYER->mHealth > 0 || gTrainingStuff[2] != 0)
			{
				if (gTrainingStuff[2] != 1)
					return;
			}
			else
			{
				gTrainingStuff[2] = 1;
				gTrainingStuff[3] = G_TIMER_RELATED;
			}

			if ((u32)(G_TIMER_RELATED - gTrainingStuff[3]) > 0x1E)
			{
				gTrainingScore = G_MECHLIST_PLAYER->field_5D0;
				Training_EndExercise();
			}
			return;
		}

		// gTrainingStuff[0] == 0x1601
		if (gTrainingStuff[1])
		{
			gBombAIRelated = G_TIMER_RELATED - gTrainingStuff[1];
		}
		else
		{
			gBombAIRelated = 0;
			gBombDieRelatedOne = 1;
			gBombDieRelatedTwo = 0;
			gBombDieTimerRelated = G_TIMER_RELATED;

			if (G_MECHLIST_PLAYER && G_MECHLIST_PLAYER->field_E18 == 0)
				gTrainingStuff[1] = G_TIMER_RELATED;
		}

		if (G_MECHLIST_PLAYER)
		{
			if (G_MECHLIST_PLAYER->field_5D0 >= 40 && gTrainingStuff[2] == 0)
			{
				gTrainingStuff[2] = 1;
				gTrainingStuff[3] = G_TIMER_RELATED;
				gTrainingScore = gBombAIRelated;
			}

			if (G_MECHLIST_PLAYER->mHealth <= 0 && gTrainingStuff[2] == 0)
			{
				gTrainingScore = -1000;
				gTrainingStuff[2] = 1;
				gTrainingStuff[3] = G_TIMER_RELATED;
			}
		}

		if (gTrainingStuff[2] == 1)
		{
			if ((u32)(G_TIMER_RELATED - gTrainingStuff[3]) > 0xF)
				Training_EndExercise();
		}

		if (G_MECHLIST_PLAYER->field_5D0 >= 40 || gBombAIRelated < 0x4650)
			return;

		gTrainingScore = -1000;
		Training_EndExercise();
		return;
	}

	if ((u32)gTrainingStuff[0] >= 0x1301)
	{
		// gTrainingStuff[0] in [0x1301, 0x1303]
		i32 stuffOne = gTrainingStuff[1];

		if (gTrainingStuff[1])
		{
			if (!G_MECHLIST_PLAYER)
				goto trainingLabel38;

			if (G_MECHLIST_PLAYER->mHealth > 0 && G_MECHLIST_PLAYER->field_E18 == 0)
			{
				gBombAIRelated = G_TIMER_RELATED - gTrainingStuff[1];

				if ((u32)(G_TIMER_RELATED - gTrainingStuff[1]) > 0x8CA0)
				{
					gTrainingScore = -1000;
					Training_EndExercise();
					return;
				}
			}
		}
		else
		{
			gBombAIRelated = 0;
			gBombDieRelatedOne = 1;
			gBombDieRelatedTwo = 0;
			gBombDieTimerRelated = G_TIMER_RELATED;
			gTrainingFlags[0] = 0;
			*gTimeAttackComplete = 0;

			if (!G_MECHLIST_PLAYER)
				goto trainingLabel38;

			if (G_MECHLIST_PLAYER->field_E18 == 0)
			{
				stuffOne = G_TIMER_RELATED;
				gTrainingStuff[1] = G_TIMER_RELATED;
			}
		}

		if (stuffOne != 0 && *gTimeAttackComplete)
		{
			gTrainingScore = gBombAIRelated;
			Training_EndExercise();
		}

		if (G_MECHLIST_PLAYER->mHealth <= 0)
		{
			gTrainingScore = -1000;
			Training_EndExercise();
			return;
		}

trainingLabel38:
		if (!gTrainingFlags[0])
			return;

		gTrainingScore = -1000;
		Training_EndExercise();
		return;
	}

	if ((u32)gTrainingStuff[0] > 0x1104)
	{
		if ((u32)gTrainingStuff[0] < 0x1201 || (u32)gTrainingStuff[0] > 0x1204)
			return;

		goto trainingLabel18;
	}

	if ((u32)gTrainingStuff[0] >= 0x1101)
	{
trainingLabel18:
		// gTrainingStuff[0] in [0x1101,0x1104] or [0x1201,0x1204]
		if (gTrainingStuff[1])
		{
			if (!G_MECHLIST_PLAYER)
				goto trainingLabel27;

			if (G_MECHLIST_PLAYER->mHealth <= 0)
			{
trainingLabel28:
				gTrainingScore = gBombAIRelated;
				Training_EndExercise();
				return;
			}

			if (G_MECHLIST_PLAYER->field_E18 == 0)
				gBombAIRelated = G_TIMER_RELATED - gTrainingStuff[1];
		}
		else
		{
			gTrainingFlags[0] = 0;
			gBombAIRelated = 0;
			gBombDieRelatedOne = 1;

			if (!G_MECHLIST_PLAYER)
				return;

			if (G_MECHLIST_PLAYER->field_E18 == 0)
			{
				gTrainingStuff[1] = G_TIMER_RELATED;
				gBombDieRelatedTwo = 0;
				gBombDieTimerRelated = G_TIMER_RELATED;
			}
		}

		if (G_MECHLIST_PLAYER->mHealth > 0)
		{
trainingLabel27:
			if (!gTrainingFlags[0])
				return;

			goto trainingLabel28;
		}

		goto trainingLabel28;
	}

	if ((u32)gTrainingStuff[0] >= 0x901 && (u32)gTrainingStuff[0] <= 0x904)
	{
		// gTrainingStuff[0] in [0x901, 0x904]: enemy-kill-count exercise,
		// draws a live "Enemies killed: NN" readout.
		if (G_MECHLIST_PLAYER && G_MECHLIST_PLAYER->mHealth <= 0)
		{
			gBombDieRelatedTwo = 0;
			gTrainingScore = *gKillNotifyCallCount;
			gBombDieTimerRelated = G_TIMER_RELATED;
			gBombDieRelatedOne = 0;
			PShell_EndTrainingInit();
		}
		else if (gTrainingStuff[1])
		{
			if (gBombAIRelated == 0)
			{
				gBombDieRelatedTwo = 0;
				gTrainingScore = *gKillNotifyCallCount;
				gBombDieTimerRelated = G_TIMER_RELATED;
				gBombDieRelatedOne = 0;
				PShell_EndTrainingInit();
			}
		}
		else
		{
			gBombDieRelatedOne = 1;
			gBombDieRelatedTwo = 1;
			gBombAIRelated = 60 * gTrainingSeconds;
			gBombDieTimerRelated = G_TIMER_RELATED;
			gTrainingStuff[1] = 1;
		}

		char text[32];
		sprintf(text, "Enemies killed: %02d", *gKillNotifyCallCount);
		Mess_SetTextJustify(0);
		Mess_DrawText(256, 220, text, 0, 0x1000);
	}
}

// @Ok
// @AlmostMatching: reloc assignements
void Training_RelocatableModuleClear(void)
{
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;
	gBombDieRelatedOne = 0;
}

// @Ok
// @Matching
void Training_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Training_RelocatableModuleClear;
	pMod->field_C[0] = Training_MonitorLevel;
	pMod->field_C[1] = Training_SpideyDied;

	Spidey_SetUserFunction("training", 1u);

	gTrainingStuff[0] = Trig_GetLevelID();
	for (i32 i = 1; i < 8; i++)
	{
		gTrainingStuff[i] = 0;
	}

	gBombRelated = 4096;
	gTrainingFlags[2] = 0;
	gTrainingFlags[1] = 0;
	gTrainingFlags[0] = 0;
}
