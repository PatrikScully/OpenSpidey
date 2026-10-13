#include "engine/rendering/backgrnd.h"
#include "engine/assets/spool.h"

#include "compatibility/validate.h"

#ifndef SPIDEY_STANDALONE
CSuper* BackgroundList;
#else
extern CSuper* BackgroundList;
#endif
EXPORT i16 gBackgroundIndex = 0;

// @Ok
// @Matching
void CBackground::AI(void)
{
	this->mAngles += this->mAngVel;
}

// @Ok
// @Matching
CBackground::CBackground(
		u32 checksum,
		CSVector* ang_vel)
{
	this->InitItem(G_OBJ_FILE);
	this->mModel = Spool_GetModel(checksum, this->mRegion);
	this->mFlags &= ~2u;
	this->mCBodyFlags &= ~2u;
	this->mType = 502;
	this->AttachTo(reinterpret_cast<CBody**>(&BackgroundList));
	this->field_F8 = gBackgroundIndex++;
}

// @Ok
CBackground::~CBackground(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&BackgroundList));
	print_if_false(gBackgroundIndex != 0, "Background index error");
	--gBackgroundIndex;
}

// @Ok
// Functional: turn on background, logic verified against Hex-Rays at 0x402880.
void Backgrnd_On(u16 a1)
{

	CSuper* cur = BackgroundList;
	if (cur)
	{
		while (cur)
		{
			if (cur->field_F8 != a1)
			{
				cur = reinterpret_cast<CSuper*>(cur->mNextItem);
			}
			else
			{
				cur->mFlags &= 0xFFFE;
				break;
			}
		}
	}
}

// @Ok
// Functional: turn off background, logic verified against Hex-Rays at 0x4028b0.
void Backgrnd_Off(u16 a1)
{

	CSuper* cur = BackgroundList;
	if (cur)
	{
		while (cur)
		{
			if (cur->field_F8 != a1)
			{
				cur = reinterpret_cast<CSuper*>(cur->mNextItem);
			}
			else
			{
				cur->mFlags |= 1;
				break;
			}
		}
	}
}

void validate_CBackground(void)
{
	VALIDATE_SIZE(CBackground, 0xFC);

	VALIDATE(CBackground, field_F8, 0xF8);
}
