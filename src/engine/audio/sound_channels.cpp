#include "compatibility/legacy_log.h"
#include "engine/audio/sound_channels.h"
#include "game/application/DXinit.h"
#include "game/application/SpideyDX.h"
#include "compatibility/validate.h"
#include "engine/assets/pcdcFile.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif
#include <cstring>
#include <cstdlib>

// @Ok
EXPORT LPDIRECTSOUNDBUFFER g_pDSBuffer;

// @Ok
EXPORT IDirectSoundBuffer* gDxSoundBuffers[0x80];

// @Ok
EXPORT SDDXSoundHolder gDxSoundHolder[0x20];

void DXSOUND_Close(i32 channelIndex)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
	{
		HRESULT hr = gDxSoundHolder[channelIndex].pDSB->Release();

		DS_ERROR_LOG_AND_QUIT(hr);

		gDxSoundHolder[channelIndex].pDSB = 0;
		gDxSoundHolder[channelIndex].mFrequency = 0;
		gDxSoundHolder[channelIndex].forcePlayingStatus = 0;
		gDxSoundHolder[channelIndex].isLooping = 0;
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
	{
		Plat_SndDestroyVoice(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB));
		gDxSoundHolder[channelIndex].pDSB = 0;
		gDxSoundHolder[channelIndex].mFrequency = 0;
		gDxSoundHolder[channelIndex].forcePlayingStatus = 0;
		gDxSoundHolder[channelIndex].isLooping = 0;
	}
#endif
}

// @Ok
// No standalone PC address (fully inlined into DXSOUND_Load in the
// original, which has its own inline copy of the same logic, verified
// against Hex-Rays at 0x503B40 this session, see the DXSOUND_Load comment
// and dxsound.attempts.md). Not separately runnable or verifiable, kept as
// a real, checked translation of the same steps.
void DXSOUND_CreateDSBuffer(char *fileName, i32 index)
{
#ifdef _WIN32
	LPVOID ptr1 = 0;
	DWORD len1 = 0;
	LPVOID ptr2 = 0;
	DWORD len2 = 0;
	WAVEFORMATEX wfx;
	long size;
	DSBUFFERDESC dsbd;

	u8* pData = loadWAV(fileName, &wfx, &size);
	if (!pData)
	{
		stateLog("\t\tERROR Loading WAV file %s!!!\r\n", fileName);
		return;
	}

	memset(&dsbd, 0, sizeof(dsbd));
	dsbd.dwSize = sizeof(dsbd);
	dsbd.dwFlags = DSBCAPS_STATIC | DSBCAPS_CTRLFREQUENCY | DSBCAPS_CTRLPAN | DSBCAPS_CTRLVOLUME;
	dsbd.dwBufferBytes = size;
	dsbd.lpwfxFormat = &wfx;

	HRESULT hr = g_pDS->CreateSoundBuffer(&dsbd, &gDxSoundBuffers[index], 0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = gDxSoundBuffers[index]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
	if (hr == DSERR_BUFFERLOST)
	{
		hr = gDxSoundBuffers[index]->Restore();
		DS_ERROR_LOG_AND_QUIT(hr);
		hr = gDxSoundBuffers[index]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
	}
	DS_ERROR_LOG_AND_QUIT(hr);

	memcpy(ptr1, pData, len1);
	if (ptr2)
		memcpy(ptr2, pData + len1, len2);

	hr = gDxSoundBuffers[index]->Unlock(ptr1, len1, ptr2, len2);
	DS_ERROR_LOG_AND_QUIT(hr);

	free(pData);
#endif
}

// @Ok
// @Matching
void DXSOUND_Init(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	DSBUFFERDESC v6;

	memset(gDxSoundBuffers, 0, sizeof(gDxSoundBuffers));
	memset(gDxSoundHolder, 0, sizeof(gDxSoundHolder));
	memset(&v6, 0, sizeof(v6));

	v6.dwBufferBytes = 0;
	v6.dwSize = sizeof(v6);
	v6.dwFlags = DSBCAPS_CTRLVOLUME | DSBCAPS_PRIMARYBUFFER;

	HRESULT hr = g_pDS->CreateSoundBuffer(&v6, &g_pDSBuffer, 0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = g_pDSBuffer->SetVolume(0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = g_pDSBuffer->Play(0, 0, 1);
	DS_ERROR_LOG_AND_QUIT(hr);
#elif defined(SPIDEY_STANDALONE)
	memset(gDxSoundBuffers, 0, sizeof(gDxSoundBuffers));
	memset(gDxSoundHolder, 0, sizeof(gDxSoundHolder));
#endif
}

// @Ok
// @Matching
i32 DXSOUND_IsPlaying(i32 channelIndex)
{
	DWORD playbackStatus = 0;

#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)

	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[channelIndex].pDSB;
	if (!pDSB)
		return 0;

	if (gDxSoundHolder[channelIndex].forcePlayingStatus)
		return 1;

	HRESULT hr = pDSB->GetStatus(&playbackStatus);

	DS_ERROR_LOG_AND_QUIT(hr);
#elif defined(SPIDEY_STANDALONE)
	if (!gDxSoundHolder[channelIndex].pDSB)
		return 0;
	if (gDxSoundHolder[channelIndex].forcePlayingStatus)
		return 1;
	if (Plat_SndIsPlaying(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB)))
		playbackStatus = 4;   // DSBSTATUS_PLAYING
#endif

	return playbackStatus & 4;
}

// Table of 64 sound names per audio group, only DXSOUND_Load reads it.
// Group 0 starts with "missing", "s_burn". Owner TU is the audio group code.
// Macro, not a static pointer variable: a stored pointer forces a memory load
// at every use, the original computes the address as a constant offset.
#define gAudioGroupSoundNames ((char**)0x0055AD64)

// @Ok
// loadWAV and DXSOUND_CreateDSBuffer are both fully inlined here on PC (neither
// has its own PC address), whole body written inline instead of calling out
// to them. Verified step by step against Hex-Rays at 0x503B40 this session:
// the group index/dsIndex math, the gdFsOpen/GetFileSize/Read/Close calls
// and their control flow (Close runs on both the read-failed and
// read-succeeded paths), the mmio chunk walk and its "hmmio == 0" early out
// (only frees the file buffer, no mmioClose, matches our unconditional
// free(fileBuf) after the if block), the WAVEFORMATEX 18 byte read and the
// WAVE_FORMAT_PCM check, and the DSBUFFERDESC flags (DSBCAPS_STATIC |
// CTRLFREQUENCY | CTRLPAN | CTRLVOLUME = 0xE2, matches the disasm's literal
// 226) all match. The error prints (nullsub_1 at "could not load"/"error
// loading") and our stateLog calls are both empty 1 byte functions in this
// release build, so which one we call has no behavioural effect. 238
// mnemonic diffs left against 0x503B40, all register scheduling
// (specifically which register the compiler dedicates as the whole
// function's persistent zero, see dxsound.attempts.md), 15+ hypotheses
// tried across two sessions without moving it.
void DXSOUND_Load(char *groupName)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (!g_pDS)
		return;

	i32 group = AUDIOGROUPS_GetGroup(groupName);
	if (group == -1)
	{
		stateLog("\tCould not loads sound for %s\r\n", groupName);
		return;
	}

	i32 index = group != 1 ? 0x40 : 0;
	char** pName = &gAudioGroupSoundNames[group * 64];

	for (i32 i = 0; i < 64; i++)
	{
		char fileName[0x100];

		if (!*pName)
			return;

		strcpy(fileName, *pName);
		if (strlen(fileName))
		{
			strcat(fileName, ".wav");

			// DXSOUND_CreateDSBuffer(fileName, index++) inlined
			i32 dsIndex = index++;

			// loadWAV(fileName, &wfx, &size) inlined
			char path[0x100];
			u8* pData = 0;
			MMIOINFO mmioinfo;
			MMCKINFO ckRiff;
			MMCKINFO ckIn;
			MMCKINFO ckData;
			i32 fileSize = 0;
			WAVEFORMATEX wfx;
			long size = 0;
			LPVOID ptr1 = 0;
			DWORD len2 = 0;

			strcpy(path, "AUDIO\\");
			strcat(path, fileName);

			HANDLE h = gdFsOpen(path, 0);
			if (h)
			{
				gdFsGetFileSize((i32)h, &fileSize);
				u8* fileBuf = (u8*)malloc(fileSize);
				if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
				{
					free(fileBuf);
					gdFsClose(h);
				}
				else
				{
					gdFsClose(h);

					memset(&mmioinfo, 0, sizeof(mmioinfo));
					mmioinfo.fccIOProc = FOURCC_MEM;
					mmioinfo.pchBuffer = (HPSTR)fileBuf;
					mmioinfo.cchBuffer = fileSize;
					mmioinfo.adwInfo[0] = 0;

					HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
					ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
					if (hmmio)
					{
						if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
						{
							ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
							mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
							mmioRead(hmmio, (HPSTR)&wfx, sizeof(WAVEFORMATEX));
							mmioAscend(hmmio, &ckIn, 0);

							if (wfx.wFormatTag == WAVE_FORMAT_PCM)
							{
								ckData.ckid = mmioStringToFOURCC("data", 0);
								mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

								if (ckData.cksize)
								{
									size = (ckData.cksize + 3) & ~3;
									pData = (u8*)malloc(size);
									memset(pData, 0, size);
									if (pData)
										mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
								}
							}
						}

						mmioClose(hmmio, 0);
					}

					free(fileBuf);
				}
			}

			if (!pData)
			{
				stateLog("\t\tERROR Loading WAV file %s!!!\r\n", fileName);
			}
			else
			{
				DSBUFFERDESC dsbd;
				memset(&dsbd, 0, sizeof(dsbd));
				dsbd.dwSize = sizeof(dsbd);
				dsbd.dwFlags = DSBCAPS_STATIC | DSBCAPS_CTRLFREQUENCY | DSBCAPS_CTRLPAN | DSBCAPS_CTRLVOLUME;
				dsbd.dwBufferBytes = size;
				dsbd.lpwfxFormat = &wfx;

				DWORD len1 = 0;
				LPVOID ptr2 = 0;

				HRESULT hr = g_pDS->CreateSoundBuffer(&dsbd, &gDxSoundBuffers[dsIndex], 0);
				DS_ERROR_LOG_AND_QUIT(hr);

				hr = gDxSoundBuffers[dsIndex]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
				if (hr == DSERR_BUFFERLOST)
				{
					hr = gDxSoundBuffers[dsIndex]->Restore();
					DS_ERROR_LOG_AND_QUIT(hr);
					hr = gDxSoundBuffers[dsIndex]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
				}
				DS_ERROR_LOG_AND_QUIT(hr);

				memcpy(ptr1, pData, len1);
				if (ptr2)
					memcpy(ptr2, pData + len1, len2);

				hr = gDxSoundBuffers[dsIndex]->Unlock(ptr1, len1, ptr2, len2);
				DS_ERROR_LOG_AND_QUIT(hr);

				free(pData);
			}
		}

		pName++;
	}
#elif defined(SPIDEY_STANDALONE)
	// Same walk as above with a plain RIFF parser instead of mmio.
	if (!g_pDS)
		return;

	i32 group = AUDIOGROUPS_GetGroup(groupName);
	if (group == -1)
		return;

	i32 index = group != 1 ? 0x40 : 0;
	char** pName = &gAudioGroupSoundNames[group * 64];

	for (i32 i = 0; i < 64; i++, pName++)
	{
		if (!*pName)
			return;
		if (!strlen(*pName))
			continue;

		char path[0x100];
		strcpy(path, "AUDIO\\");
		strcat(path, *pName);
		strcat(path, ".wav");

		i32 dsIndex = index++;

		HANDLE h = gdFsOpen(path, 0);
		if (!h)
		{
			DXERR_printf("\t\tERROR Loading WAV file %s!!!\r\n", path);
			continue;
		}

		i32 fileSize = 0;
		gdFsGetFileSize((i32)h, &fileSize);
		u8* fileBuf = (u8*)malloc(fileSize + 0x800);
		gdFsRead((i32)h, (fileSize + 0x7FF) / 0x800, fileBuf);
		gdFsClose(h);

		// RIFF/WAVE: "fmt " then "data"
		i32 rate = 0, channels = 0, bits = 0, fmtTag = 0;
		const u8* pcm = 0;
		i32 pcmBytes = 0;
		if (fileSize >= 12 && !memcmp(fileBuf, "RIFF", 4) && !memcmp(fileBuf + 8, "WAVE", 4))
		{
			i32 pos = 12;
			while (pos + 8 <= fileSize)
			{
				u32 ckSize;
				memcpy(&ckSize, fileBuf + pos + 4, 4);
				const u8* ck = fileBuf + pos + 8;
				if (!memcmp(fileBuf + pos, "fmt ", 4) && ckSize >= 16)
				{
					u16 v16;
					memcpy(&v16, ck, 2); fmtTag = v16;
					memcpy(&v16, ck + 2, 2); channels = v16;
					memcpy(&rate, ck + 4, 4);
					memcpy(&v16, ck + 14, 2); bits = v16;
				}
				else if (!memcmp(fileBuf + pos, "data", 4))
				{
					pcm = ck;
					pcmBytes = (i32)ckSize;
					if (pos + 8 + pcmBytes > fileSize)
						pcmBytes = fileSize - pos - 8;
					break;
				}
				pos += 8 + (i32)((ckSize + 1) & ~1u);
			}
		}

		if (pcm && pcmBytes > 0 && fmtTag == 1)
			gDxSoundBuffers[dsIndex] = reinterpret_cast<IDirectSoundBuffer*>(
					Plat_SndCreateBuffer(pcm, pcmBytes, rate, channels, bits));
		else
			DXERR_printf("\t\tERROR Loading WAV file %s!!!\r\n", path);

		free(fileBuf);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_Open(
		i32 channelIndex,
		i32 sampleIndex,
		i32 secondaryBank)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (g_pDS)
	{
		if (secondaryBank)
			sampleIndex += 0x40;

		IDirectSoundBuffer *sourceBuffer = gDxSoundBuffers[sampleIndex];

		if (sourceBuffer)
		{
			HRESULT hr = g_pDS->DuplicateSoundBuffer(sourceBuffer, &gDxSoundHolder[channelIndex].pDSB);
			DS_ERROR_LOG_AND_QUIT(hr);

			DWORD baseFrequency;
			hr = gDxSoundHolder[channelIndex].pDSB->GetFrequency(&baseFrequency);
			DS_ERROR_LOG_AND_QUIT(hr);

			gDxSoundHolder[channelIndex].mFrequency = baseFrequency;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (g_pDS)
	{
		if (secondaryBank)
			sampleIndex += 0x40;

		PlatSoundBuffer* pBuf = reinterpret_cast<PlatSoundBuffer*>(gDxSoundBuffers[sampleIndex]);
		if (pBuf)
		{
			PlatSoundVoice* pVoice = Plat_SndCreateVoice(pBuf);
			gDxSoundHolder[channelIndex].pDSB = reinterpret_cast<LPDIRECTSOUNDBUFFER>(pVoice);
			gDxSoundHolder[channelIndex].mFrequency = Plat_SndGetFrequency(pVoice);
		}
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_Play(i32 channelIndex, i32 loop)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
	{
		gDxSoundHolder[channelIndex].forcePlayingStatus = 0;
		gDxSoundHolder[channelIndex].isLooping = loop != 0;

		HRESULT hr = gDxSoundHolder[channelIndex].pDSB->Play(0, 0, loop != 0);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
	{
		gDxSoundHolder[channelIndex].forcePlayingStatus = 0;
		gDxSoundHolder[channelIndex].isLooping = loop != 0;
		Plat_SndPlay(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB), loop != 0);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_SetPan(i32 channelIndex,i32 pan)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[channelIndex].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->SetPan(645 * pan - 10000);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
		Plat_SndSetPan(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB), 645 * pan - 10000);
#endif
}

// @Ok
// @Matching
void DXSOUND_SetPitch(i32 channelIndex, i32 pitchScale)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[channelIndex].pDSB;
	if (pDSB)
	{
		i32 frequency = (i32)(static_cast<f32>(pitchScale) / 1200.f * static_cast<f32>(gDxSoundHolder[channelIndex].mFrequency));
		HRESULT hr = pDSB->SetFrequency(frequency);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
	{
		i32 frequency = (i32)(static_cast<f32>(pitchScale) / 1200.f * static_cast<f32>(gDxSoundHolder[channelIndex].mFrequency));
		Plat_SndSetFrequency(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB), frequency);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_SetVolume(i32 channelIndex,i32 volume)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[channelIndex].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->SetVolume(39 * (volume - 255));
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
		Plat_SndSetVolume(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB), 39 * (volume - 255));
#endif
}

// @Ok
// @Matching
void DXSOUND_ShutDown(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	HRESULT hr = g_pDSBuffer->Stop();
	DS_ERROR_LOG_AND_QUIT(hr);

	DXSOUND_Unload(0, 1);
#elif defined(SPIDEY_STANDALONE)
	for (i32 i = 0; i < 0x20; i++)
		DXSOUND_Close(i);
	DXSOUND_Unload(0, 1);
#endif
}

// @Ok
// @Matching
void DXSOUND_Stop(i32 channelIndex)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[channelIndex].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->Stop();
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[channelIndex].pDSB)
		Plat_SndStop(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[channelIndex].pDSB));
#endif
}


// @Ok
// @Matching
void DXSOUND_Unload(char *groupName, i32 unloadAll)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (unloadAll)
	{
		for (i32 i = 0; i < 0x80; i++)
		{
			IDirectSoundBuffer *pBuf = gDxSoundBuffers[i];
			if (pBuf)
			{
				HRESULT hr = pBuf->Release();
				DS_ERROR_LOG_AND_QUIT(hr);
				gDxSoundBuffers[i] = 0;
			}
		}
	}
	else
	{
		i32 startIndex = AUDIOGROUPS_GetGroup(groupName) != 1 ? 0x40 : 0;

		for (i32 j = 0; j < 64; j++)
		{
			IDirectSoundBuffer *pBuf = gDxSoundBuffers[startIndex];
			if (pBuf)
			{
				HRESULT hr = pBuf->Release();
				DS_ERROR_LOG_AND_QUIT(hr);
				gDxSoundBuffers[startIndex] = 0;
			}

			startIndex++;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	i32 startIndex = 0, count = 0x80;
	if (!unloadAll)
	{
		startIndex = AUDIOGROUPS_GetGroup(groupName) != 1 ? 0x40 : 0;
		count = 64;
	}
	for (i32 i = 0; i < count; i++, startIndex++)
	{
		if (gDxSoundBuffers[startIndex])
		{
			Plat_SndDestroyBuffer(reinterpret_cast<PlatSoundBuffer*>(gDxSoundBuffers[startIndex]));
			gDxSoundBuffers[startIndex] = 0;
		}
	}
#endif
}

// @Ok
// @Matching
// @Ok
// No PC address at all, proven dead code: scanned every CALL in the whole
// .text section against the mmioOpenA/mmioDescend/mmioRead/mmioClose/
// mmioAscend import thunks, the only call sites anywhere in the binary are
// inside DXSOUND_Load's own inlined copy of this same parsing logic (see
// loadWAV above and dxsound.attempts.md). The Mac source has a standalone
// ParseWavHeader (spiderman_names.txt 0x166040), the PC port folded it
// (and DXSOUND_CreateDSBuffer) straight into DXSOUND_Load instead. Same
// chunk-walk verified against Hex-Rays for DXSOUND_Load/loadWAV this
// session, adapted here to its pointer-to-pointer output style. Since
// nothing calls it, it cannot be exercised or byte-verified on its own.
void ParseWavHeader(char *fileName, tWAVEFORMATEX **ppwfx, long *pSize, u8 **ppData)
{
#ifdef _WIN32
	char path[0x100];
	MMIOINFO mmioinfo;
	MMCKINFO ckRiff;
	MMCKINFO ckIn;
	MMCKINFO ckData;
	i32 fileSize;

	*ppwfx = 0;
	*ppData = 0;

	strcpy(path, "AUDIO\\");
	strcat(path, fileName);

	HANDLE h = gdFsOpen(path, 0);
	if (!h)
		return;

	gdFsGetFileSize((i32)h, &fileSize);
	u8* fileBuf = (u8*)malloc(fileSize);
	if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
	{
		free(fileBuf);
		gdFsClose(h);
		return;
	}
	gdFsClose(h);

	memset(&mmioinfo, 0, sizeof(mmioinfo));
	mmioinfo.fccIOProc = FOURCC_MEM;
	mmioinfo.pchBuffer = (HPSTR)fileBuf;
	mmioinfo.cchBuffer = fileSize;
	mmioinfo.adwInfo[0] = 0;

	HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
	ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
	if (!hmmio)
	{
		free(fileBuf);
		return;
	}

	if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
	{
		tWAVEFORMATEX* pwfx = (tWAVEFORMATEX*)malloc(sizeof(WAVEFORMATEX));

		ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
		mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
		mmioRead(hmmio, (HPSTR)pwfx, sizeof(WAVEFORMATEX));
		mmioAscend(hmmio, &ckIn, 0);

		if (pwfx->wFormatTag == WAVE_FORMAT_PCM)
		{
			ckData.ckid = mmioStringToFOURCC("data", 0);
			mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

			if (ckData.cksize)
			{
				*pSize = (ckData.cksize + 3) & ~3;
				u8* pData = (u8*)malloc(*pSize);
				memset(pData, 0, *pSize);
				if (pData)
					mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
				*ppData = pData;
			}
		}

		*ppwfx = pwfx;
	}

	mmioClose(hmmio, 0);
	free(fileBuf);
#endif
}

// @Ok
// No standalone PC address (fully inlined into DXSOUND_Load in the original,
// alongside DXSOUND_CreateDSBuffer). DXSOUND_Load has its own inline copy,
// verified against Hex-Rays at 0x503B40 this session (see its comment).
// Reads AUDIO\<fileName> through the PKR file system and parses it with
// mmio from memory. Returns the sample data, *pSize is the size rounded up
// to 4. Not separately runnable or byte-verifiable on its own.
u8* loadWAV(char *fileName, tWAVEFORMATEX *pwfx, long *pSize)
{
#ifdef _WIN32
	char path[0x100];
	u8* pData = 0;
	MMIOINFO mmioinfo;
	MMCKINFO ckRiff;
	MMCKINFO ckIn;
	MMCKINFO ckData;
	i32 fileSize;

	strcpy(path, "AUDIO\\");
	strcat(path, fileName);

	HANDLE h = gdFsOpen(path, 0);
	if (!h)
		return 0;

	gdFsGetFileSize((i32)h, &fileSize);
	u8* fileBuf = (u8*)malloc(fileSize);
	if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
	{
		free(fileBuf);
		gdFsClose(h);
		return 0;
	}
	gdFsClose(h);

	memset(&mmioinfo, 0, sizeof(mmioinfo));
	mmioinfo.fccIOProc = FOURCC_MEM;
	mmioinfo.pchBuffer = (HPSTR)fileBuf;
	mmioinfo.cchBuffer = fileSize;
	mmioinfo.adwInfo[0] = 0;

	HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
	ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
	if (!hmmio)
	{
		free(fileBuf);
		return 0;
	}

	if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
	{
		ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
		mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
		mmioRead(hmmio, (HPSTR)pwfx, sizeof(WAVEFORMATEX));
		mmioAscend(hmmio, &ckIn, 0);

		if (pwfx->wFormatTag == WAVE_FORMAT_PCM)
		{
			ckData.ckid = mmioStringToFOURCC("data", 0);
			mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

			if (ckData.cksize)
			{
				*pSize = (ckData.cksize + 3) & ~3;
				pData = (u8*)malloc(*pSize);
				memset(pData, 0, *pSize);
				if (pData)
					mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
			}
		}
	}

	mmioClose(hmmio, 0);
	free(fileBuf);
	return pData;
#else
	return 0;
#endif
}

// @Ok
void validate_SDXSoundHolder(void)
{
	VALIDATE_SIZE(SDDXSoundHolder, 0xC);

	VALIDATE(SDDXSoundHolder, pDSB, 0x0);
	VALIDATE(SDDXSoundHolder, mFrequency, 0x4);

	VALIDATE(SDDXSoundHolder, forcePlayingStatus, 0x8);
	VALIDATE(SDDXSoundHolder, isLooping, 0x9);
}

void validate_SDxSomething(void)
{
	VALIDATE_SIZE(gDxSoundBuffers, 0x200);
}

void validate_DSBUFFERDESC(void)
{
#ifdef _WIN32
	VALIDATE_SIZE(DSBUFFERDESC, 36);

	VALIDATE(DSBUFFERDESC, dwSize, 0x0);
	VALIDATE(DSBUFFERDESC, dwFlags, 0x4);
	VALIDATE(DSBUFFERDESC, dwBufferBytes, 0x8);
#endif
}
