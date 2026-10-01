#include "pkr.h"
#include "zlib.h"

#include "validate.h"
#include <cstring>
#include <cstdlib>

#include <cstdarg>
#include <cstdio>

#define PKRFILE_UNCOMPRESSED -2
#define PKRFILE_COMPRESSED_ZLIB 2

#ifndef SPIDEY_STANDALONE
LIBPKR_HANDLE* gDataPkr;
#else
extern LIBPKR_HANDLE* gDataPkr;
#endif

// guess: shared error-message buffer for PKR_ReportError/PKR_GetLastError, size 0x200.
// idb_globals.txt nearest neighbours: gSbMallocRelated 0x02E09BDC, gSbInitRelated 0x02E09BE0 (0x1C before this address, no named object covers it).
static char* const gPkrErrorMsg = reinterpret_cast<char*>(0x02E09BFC);

#ifndef _WIN32
#define strcmpi strcasecmp
#endif

#ifdef _WIN32
#define VSNPRINTF _vsnprintf
#else
#define VSNPRINTF vsnprintf
#endif

// @NB: the original was built as library and built in debug mode, I won't do the same
// too much hassle for little gain

// @Ok
// checked against the decompiled 0x519194: field offsets (fileOffset 0x28,
// uncompressedSize 0x2C, compressedSize 0x30, crc 0x20, PKR_DIRINFO
// numFiles 0x24 / field_20 0x20, NODE_FILEINFO/NODE_DIRINFO mNext at
// 0x144 / 0x28), loop shapes, and the strcmpi/fseek/fread/decompress/CRC
// call sequence all match. the CRT's locked fread wrapper decompiles under
// the misleading name "fwrite__0" (it is really a lock_file/fread/
// unlock_file shim, confirmed by decompiling 0x52a9f6), not an actual
// write, so our fread() call here is correct.
// byte-diff residue is a build/toolchain difference, not a logic gap: this
// file was originally compiled unoptimized (see the @NB note above), same
// root cause documented on PKR_LockFile below. functional parity is the
// bar for this session, not byte-matching, so left as is rather than
// retrofitting #pragma optimize for a function this size.
u8 PKR_ReadFile(
		LIBPKR_HANDLE* pHandle,
		const char* pDirName,
		const char* pFileName,
		void** ppBuf,
		i32* pSize)
{
	PKR_FILEINFO fileInfo;
	memset(&fileInfo, 0, sizeof(fileInfo));

	fileInfo.uncompressedSize = -1;
	*ppBuf = 0;
	*pSize = 0;

	if (!pHandle->mFooter.numFiles)
	{
		PKR_ReportError("PKR_ReadFile: There are no files");
		return 0;
	}

	NODE_DIRINFO* pNodeDir = pHandle->pDirInfo;
	for (u32 i = 0; i < pHandle->mFooter.numDirs && strcmpi(pNodeDir->dirInfo.name, pDirName); i++)
		pNodeDir = pNodeDir->mNext;

	if (!pNodeDir)
	{
		PKR_ReportError("PKR_ReadFile: The directory %s is not found", pDirName);
		return 0;
	}

	PKR_DIRINFO dirInfo = pNodeDir->dirInfo;
	if (!dirInfo.numFiles)
	{
		PKR_ReportError("PKR_ReadFile: There are no files in %s", dirInfo.name);
		return 0;
	}

	if (dirInfo.numFiles + dirInfo.field_20 > pHandle->mFooter.numFiles)
	{
		PKR_ReportError("PKR_ReadFile: Number of files in dir %s, %i, is too many", pDirName, dirInfo.numFiles);
		return 0;
	}

	NODE_FILEINFO* pFileInfo = pHandle->pFileInfo;
	for (u32 j = 0; j < dirInfo.field_20; j++ )
		pFileInfo = pFileInfo->mNext;

	for (u32 k = 0; k < dirInfo.numFiles; k++)
	{
		if ( !strcmpi(pFileName, pFileInfo->fileInfo.name) )
		{
			fileInfo = pFileInfo->fileInfo;
			break;
		}

		pFileInfo = pFileInfo->mNext;
	}

	if (fileInfo.uncompressedSize == -1)
	{
		PKR_ReportError("PKR_ReadFile: File %s not found in dir %s", pFileName, pDirName);
		return 0;
	}

	if(!fileInfo.compressedSize)
	{
		PKR_ReportError("PKR_ReadFile: File has no size, cannot verify");
		return 0;
	}

	u8* pBuf = new u8[fileInfo.compressedSize];
	if(!pBuf)
	{
		PKR_ReportError("PKR_ReadFile: Not enough memory");
		return 0;
	}

	fseek(pHandle->fp, fileInfo.fileOffset, 0);
	if (fread(pBuf, fileInfo.compressedSize, 1, pHandle->fp) != 1)
	{
		delete[] pBuf;
		PKR_ReportError("PKR_ReadFile: Error reading file %s\\%s", pDirName, pFileName);
		return 0;
	}

	pBuf = PKRComp_DecompressFile(&fileInfo, pBuf, 1);
	if (!fileCRCCheck(pBuf, fileInfo.uncompressedSize, fileInfo.crc))
	{
		delete[] pBuf;
		return 0;
	}

	*ppBuf = pBuf;
	*pSize = fileInfo.uncompressedSize;

	return 1;
}

// @Ok
u8 PKR_GetFileInfo(
		LIBPKR_HANDLE* pHandle,
		const char* pDirName,
		const char* pFileName,
		PKR_FILEINFO* pFileInfo)
{
	memset(pFileInfo, 0, sizeof(PKR_FILEINFO));
	pFileInfo->uncompressedSize = -1;

	if (!pHandle->mFooter.numFiles)
	{
		PKR_ReportError("PKR_GetFileInfo: There are no files");
		return 0;
	}

	NODE_DIRINFO* pCurNodeDir = pHandle->pDirInfo;
	for (u32 i = 0; i < pHandle->mFooter.numDirs && strcmpi(pCurNodeDir->dirInfo.name, pDirName); i++ )
		pCurNodeDir = pCurNodeDir->mNext;

	if (!pCurNodeDir)
	{
		PKR_ReportError("PKR_GetFileInfo: The directory %s is not found", pDirName);
		return 0;
	}

	PKR_DIRINFO dirInfo;
	memcpy(&dirInfo, &pCurNodeDir->dirInfo, sizeof(dirInfo));

	if (!dirInfo.numFiles)
	{
		PKR_ReportError("PKR_GetFileInfo: There are no files in %s", dirInfo.name);
		return 0;
	}

	 if (dirInfo.numFiles + dirInfo.field_20 > pHandle->mFooter.numFiles)
	 {
		PKR_ReportError("PKR_GetFileInfo: Number of files in dir %s, %i, is too many", pDirName, dirInfo.numFiles);
		return 0;
	 }

	NODE_FILEINFO* pCurNodeFile = pHandle->pFileInfo;
	for (u32 j = 0; j < dirInfo.field_20; j++ )
		pCurNodeFile = pCurNodeFile->mNext;

	for (u32 k = 0; k < dirInfo.numFiles; k++ )
	{
		if ( !strcmpi(pFileName, pCurNodeFile->fileInfo.name) )
		{
			memcpy(pFileInfo, pCurNodeFile, sizeof(PKR_FILEINFO));
			break;
		}

		pCurNodeFile = pCurNodeFile->mNext;
	}

	if (pFileInfo->uncompressedSize == -1)
	{
		PKR_ReportError("PKR_GetFileInfo: File %s not found in dir %s", pFileName, pDirName);
		return 0;
	}

	return 1;
}

// @Ok
void fileRemoveFromPKR(
		LIBPKR_HANDLE* pHandle,
		NODE_FILEINFO* pNodeFile)
{
	if (pHandle->pFileInfo == pNodeFile)
		pHandle->pFileInfo = pNodeFile->mNext;
	if (pNodeFile->mPrev)
		pNodeFile->mPrev->mNext = pNodeFile->mNext;
	if (pNodeFile->mNext)
		pNodeFile->mNext->mPrev = pNodeFile->mPrev;
	pHandle->mFooter.numFiles--;
	pNodeFile->pDirInfo->numFiles--;
	delete pNodeFile;
}

// @Ok
void dirRemoveFromPKR(
		LIBPKR_HANDLE* pHandle,
		NODE_DIRINFO* pDirInfo)
{
	if (pHandle->pDirInfo == pDirInfo)
		pHandle->pDirInfo = pDirInfo->mNext;

	if (pDirInfo->mPrev)
		pDirInfo->mPrev->mNext = pDirInfo->mNext;

	if (pDirInfo->mNext)
		pDirInfo->mNext->mPrev = pDirInfo->mPrev;

	pHandle->mFooter.numDirs--;
	delete pDirInfo;
}

// callees outside our assigned range, forwarded to the original rather than
// guessed at (same idea as gsub_513FF0/gsub_511860 in DXsound.cpp, but a
// naked jmp-trampoline instead of a function-pointer global: flushPKR is
// compiled with optimizations off, and under that a function-pointer global
// call is a real memory-indirect call, one byte longer than the original's
// direct call; a naked function is called directly by its caller, so this
// keeps the call site itself byte-identical).
// gsub_517FB6 sits immediately after flushPKR in the binary; called with
// (buf, len) and its result is stored into a PKR_FILEINFO.crc, so it is
// probably a crc32-like checksum. gsub_51AC61 is called with
// (pFileNode, buf, 1) and its result replaces buf for the following fwrite
// and delete[], so it probably compresses (or passes through) the file
// data before it is written to the archive.
// naked (MSVC only, no C prologue) so the call site jumps straight into the
// original code with the stack untouched; the Linux sanity build (which
// does not need byte-matching, just to link and run) uses a plain
// function-pointer call instead, since g++ has no naked/__asm equivalent.
#ifdef _MSC_VER
// @Bogus
__declspec(naked) u32 gsub_517FB6(u8*, i32)
{
	__asm
	{
		mov eax, 00517FB6h
		jmp eax
	}
}
#else
// @Bogus
u32 gsub_517FB6(u8* buf, i32 len)
{
	typedef u32 (*func_t)(u8*, i32);
	return ((func_t)0x00517FB6)(buf, len);
}
#endif

#ifdef _WIN32
// @Bogus
__declspec(naked) u8* gsub_51AC61(NODE_FILEINFO*, u8*, i32)
{
	__asm
	{
		mov eax, 0051AC61h
		jmp eax
	}
}
#else
// @Bogus
u8* gsub_51AC61(NODE_FILEINFO* pFileNode, u8* buf, i32 flag)
{
	typedef u8* (*func_t)(NODE_FILEINFO*, u8*, i32);
	return ((func_t)0x0051AC61)(pFileNode, buf, flag);
}
#endif

// guess: an empty string, used to clear NODE_FILEINFO::name once a newly
// added file has been written into the archive. sits right after the 0x200
// byte gPkrErrorMsg buffer.
static char* const gPkrEmptyStr = reinterpret_cast<char*>(0x02E09DFC);

// unoptimized, like PKR_GetLastError/PKR_ReportError above (see the note on
// PKR_GetLastError). also force the real memset/strcpy calls, same reason as
// the strlen/strcpy note there.
#pragma function(memset, strcpy)
#pragma optimize("", off)
// @Ok
// @Matching
u8 flushPKR(LIBPKR_HANDLE* a1)
{
	LIBPKR_HANDLE* pHandle = a1;
	u32 dirOffset = pHandle->mHeader.dirOffset;
	u32 pad = 0;

	if (pHandle->field_128 & 1)
	{
		i32 fseekResult = fseek(pHandle->fp, dirOffset, 0);
		if (fseekResult)
		{
			PKR_ReportError("flushPKR: fseek error to %i", dirOffset);
			return 0;
		}

		NODE_DIRINFO* pDirNode = pHandle->pDirInfo;
		NODE_FILEINFO* pFileNode = pHandle->pFileInfo;
		u32 i;

		for (i = 0; i < pHandle->mFooter.numDirs; i++)
		{
			PKR_DIRINFO dirInfo = pDirNode->dirInfo;

			if (dirInfo.numFiles > 0)
			{
				for (u32 j = 0; j < dirInfo.field_20; j++)
					pFileNode = pFileNode->mNext;

				for (u32 k = 0; k < dirInfo.numFiles; k++)
				{
					if (pFileNode->field_13C & 1)
					{
						pFileNode->fileInfo.fileOffset = dirOffset;

						FILE* fp2 = fopen(pFileNode->name, "rb");
						if (!fp2)
						{
							printf("flushPKR: cannot open %s: %s\n", pFileNode->name, strerror(0));
							PKR_ReportError("flushPKR: cannot open %s", pFileNode->name);
							pFileNode = pFileNode->mNext;
							continue;
						}

						u8* buf = new u8[pFileNode->fileInfo.uncompressedSize];
						memset(buf, 0, pFileNode->fileInfo.uncompressedSize);
						fread(buf, pFileNode->fileInfo.uncompressedSize, 1, fp2);
						fclose(fp2);

						pFileNode->fileInfo.crc = gsub_517FB6(buf, pFileNode->fileInfo.uncompressedSize);
						buf = gsub_51AC61(pFileNode, buf, 1);

						fwrite(buf, pFileNode->fileInfo.compressedSize, 1, pHandle->fp);
						delete[] buf;

						dirOffset += pFileNode->fileInfo.compressedSize;

						pad = (dirOffset + pHandle->mFooter.field_0 - 1) / pHandle->mFooter.field_0 * pHandle->mFooter.field_0 - dirOffset;
						for (u32 m = 0; m < pad; m++)
							fputc(0, pHandle->fp);
						dirOffset += pad;

						pFileNode->field_13C &= ~1;
						strcpy(pFileNode->name, gPkrEmptyStr);
					}

					pFileNode = pFileNode->mNext;
				}
			}

			pDirNode = pDirNode->mNext;
			pFileNode = pHandle->pFileInfo;
		}

		pHandle->mHeader.dirOffset = dirOffset;

		fseek(pHandle->fp, 0, 0);
		fwrite(&pHandle->mHeader, sizeof(PKR_HEADER), 1, pHandle->fp);

		fseek(pHandle->fp, dirOffset, 0);
		fwrite(&pHandle->mFooter, sizeof(PKR_FOOTER), 1, pHandle->fp);

		i32 fileOffsetAccum = 0;
		pDirNode = pHandle->pDirInfo;
		for (i = 0; i < pHandle->mFooter.numDirs; i++)
		{
			pDirNode->dirInfo.field_20 = fileOffsetAccum;
			fileOffsetAccum += pDirNode->dirInfo.numFiles;

			fwrite(&pDirNode->dirInfo, sizeof(PKR_DIRINFO), 1, pHandle->fp);

			pDirNode = pDirNode->mNext;
		}

		pFileNode = pHandle->pFileInfo;
		for (i = 0; i < pHandle->mFooter.numFiles; i++)
		{
			fwrite(&pFileNode->fileInfo, sizeof(PKR_FILEINFO), 1, pHandle->fp);
			pFileNode = pFileNode->mNext;
		}

		pHandle->field_128 &= ~1;
	}

	return 1;
}
#pragma optimize("", on)
#pragma intrinsic(memset, strcpy)

// @Ok
u8 PKR_Close(LIBPKR_HANDLE* pHandle)
{
	flushPKR(pHandle);
	fclose(pHandle->fp);
	while (pHandle->pFileInfo)
		fileRemoveFromPKR(pHandle, pHandle->pFileInfo);
	while (pHandle->pDirInfo)
		dirRemoveFromPKR(pHandle, pHandle->pDirInfo);
	delete pHandle;
	return 1;
}

// @Ok
// checked against the decompiled 0x518883: fp at offset 0, name at offset
// 4, both matching LIBPKR_HANDLE's validated layout, same branch shape
// (fp already open -> 1, fopen "rb+" -> 1 on success, else report+0).
// byte-diff residue is the same unoptimized-library build difference
// documented on PKR_ReadFile above, not a logic gap.
u8 PKR_LockFile(LIBPKR_HANDLE* pHandle)
{
	if (pHandle->fp)
		return 1;

	pHandle->fp = fopen(pHandle->name, "rb+");
	if (pHandle->fp)
		return 1;

	PKR_ReportError("PKR_UnlockFile: Error cannot open %s", &pHandle->name);
	return 0;
}

// @Ok
u8 fileAddToPKR(
		LIBPKR_HANDLE* pHandle,
		PKR_FILEINFO fileInfo,
		PKR_DIRINFO* pDirInfo,
		char* pName)
{
	NODE_FILEINFO* pRes = new NODE_FILEINFO;

	if (!pRes)
	{
		PKR_ReportError("fileAddToPKR: Cannot allocate new file node.");
		return 0;
	}

	memset(pRes, 0, sizeof(NODE_FILEINFO));
	memcpy(&pRes->fileInfo, &fileInfo, sizeof(fileInfo));
	pRes->pDirInfo = pDirInfo;

	if (pName && strlen(pName))
	{
		strcpy(pRes->name, pName);
		pRes->field_13C |= 1u;
	}

	if (!pHandle->pFileInfo)
	{
		pHandle->pFileInfo = pRes;
		pRes->mNext = 0;
		pRes->mPrev = 0;
	}
	else
	{
		PKR_DIRINFO dirInfo;
		memcpy(&dirInfo, pDirInfo, sizeof(dirInfo));
		NODE_FILEINFO* pFileInfo = pHandle->pFileInfo;

		for (u32 i = 0; i < dirInfo.field_20; i++)
			pFileInfo = pFileInfo->mNext;
		for (u32 j = 0; j < dirInfo.numFiles; j++)
		{
			if ( !strcmp(pFileInfo->fileInfo.name, pRes->fileInfo.name) )
			{
				PKR_ReportError("fileAddToPKR: File Already added.");
				delete pRes;
				return 0;
			}
			pFileInfo = pFileInfo->mNext;
		}
		NODE_FILEINFO* k;
		for (
				k = pHandle->pFileInfo;
				k->mNext;
				k = k->mNext );

		k->mNext = pRes;
		pRes->mPrev = k;
		pRes->mNext = 0;
	}

	pHandle->mFooter.numFiles++;
	pDirInfo->numFiles++;
	return 1;
}

// @Ok
u8 dirAddToPKR(LIBPKR_HANDLE* pHandle, PKR_DIRINFO dirInfo)
{
	NODE_DIRINFO* newNode = new NODE_DIRINFO;
	if (!newNode)
	{
		PKR_ReportError("dirAddToPKR: Cannot allocate new dir node.");
		return 0;
	}

	memset(newNode, 0, sizeof(NODE_DIRINFO));
	memcpy(newNode, &dirInfo, sizeof(PKR_DIRINFO));

	if (!pHandle->pDirInfo)
	{
		pHandle->pDirInfo = newNode;
	}
	else
	{
		NODE_DIRINFO* pNode;
		for (pNode = pHandle->pDirInfo;
				pNode->mNext;
				pNode = pNode->mNext)
		{
			if (!strcmp(pNode->dirInfo.name, newNode->dirInfo.name))
			{
				PKR_ReportError("dirAddToPKR: Dir already added.");
				delete newNode;
				return 0;
			}
		}

		if (!strcmp(pNode->dirInfo.name, newNode->dirInfo.name))
		{
			PKR_ReportError("dirAddToPKR: Dir already added.");
			delete newNode;
			return 0;
		}

		pNode->mNext = newNode;
		newNode->mPrev = pNode;
		newNode->mNext = 0;
	}

	pHandle->mFooter.numDirs++;
	return 1;
}

// this function was compiled with all optimizations off (matches the rest of
// the PKR library note at the top of this file: it was built unoptimized).
// without this the compiler folds strlen/strcpy into inline scans and omits
// the stack frame, none of which the original does.
#pragma function(strlen, strcpy)
#pragma optimize("", off)
// @Ok
// @Matching
u8 PKR_GetLastError(char* a1)
{
	if (!strlen(gPkrErrorMsg))
		return 0;

	strcpy(a1, gPkrErrorMsg);
	return 1;
}
#pragma optimize("", on)
#pragma intrinsic(strlen, strcpy)

// @Ok
u8 PKR_Open(
		LIBPKR_HANDLE** ppHandle,
		const char* name,
		i32 readOnly)
{
	LIBPKR_HANDLE* pHandle = new LIBPKR_HANDLE;
	if (!pHandle)
	{
		*ppHandle = 0;
		PKR_ReportError("PKR_Open: Cannot allocate handle");
		return 0;
	}

	memset(pHandle, 0, sizeof(LIBPKR_HANDLE));
	strcpy(pHandle->name, name);

	FILE* fp = fopen(name, "rb");
	if (!fp)
	{
		*ppHandle = 0;
		PKR_ReportError("PKR_Open: PKR file is not found at %s", name);
		return 0;
	}

	fclose(fp);
	if (readOnly)
		pHandle->fp = fopen(name, "rb");
	else
		pHandle->fp = fopen(name, "rb+");

	if (fread(&pHandle->mHeader, sizeof(PKR_HEADER), 1, pHandle->fp) != 1)
	{
		fclose(pHandle->fp);
		*ppHandle = 0;
		PKR_ReportError("PKR_Open: Error reading PKR_HEADER from %s", name);
		return 0;
	}

	if (pHandle->mHeader.pkrMagic != 0x33524B50)
	{
		fclose(pHandle->fp);
		*ppHandle = 0;
		PKR_ReportError(
			"PKR_Open: %s is not a valid PKR. Magic code %08X isn't correct, should be %08X",
			name,
			pHandle->mHeader.pkrMagic,
			0x33524B50);
		return 0;
	}

	fseek(pHandle->fp, pHandle->mHeader.dirOffset, SEEK_SET);

	if (fread(&pHandle->mFooter, sizeof(PKR_FOOTER), 1u, pHandle->fp) != 1)
	{
		fclose(pHandle->fp);
		*ppHandle = 0;
		PKR_ReportError("PKR_Open: Error reading PKR_FOOTER from %s", name);
		return 0;
	}

	u32 numDirs = pHandle->mFooter.numDirs;
	pHandle->mFooter.numDirs = 0;
	pHandle->mFooter.numFiles = 0;
	for (u32 i = 0; i < numDirs; i++)
	{
		PKR_DIRINFO dirInfo;
		if (fread(&dirInfo, sizeof(PKR_DIRINFO), 1, pHandle->fp) != 1)
		{
			fclose(pHandle->fp);
			*ppHandle = 0;
			PKR_ReportError("PKR_Open: Error reading PKR_DIRINFO %i from %s", i, name);
			return 0;
		}

		if (!dirAddToPKR(pHandle, dirInfo))
			return 0;
	}
	
	NODE_DIRINFO* pDirInfo = pHandle->pDirInfo;
	for (u32 dirNum = 0; dirNum < pHandle->mFooter.numDirs; dirNum++)
	{
		u32 numFiles = pDirInfo->dirInfo.numFiles;
		pDirInfo->dirInfo.numFiles = 0;
		for (u32 j = 0; j < numFiles; j++ )
		{
			PKR_FILEINFO fileInfo;
			if ( fread(&fileInfo, sizeof(PKR_FILEINFO), 1u, pHandle->fp) != 1 )
			{
				fclose(pHandle->fp);
				*ppHandle = 0;
				PKR_ReportError("PKR_Open: Error reading PKR_FILEINFO %i from %s", dirNum, name);
				return 0;
			}
			if (!fileAddToPKR(pHandle, fileInfo, &pDirInfo->dirInfo, 0))
				return 0;
		}

		pDirInfo = pDirInfo->mNext;
	}


	pHandle->field_108 = -2;
	*ppHandle = pHandle;
	return 1;
}

// @Ok
u8 PKR_UnlockFile(LIBPKR_HANDLE* hPkr)
{
	if (hPkr->fp)
	{
		fclose(hPkr->fp);
		hPkr->fp = 0;
		return 1;
	}

	return 0;
}

// @Ok
u8* decompressZLIB(u8* src, u32 srcLen, u32 dstLen)
{
	u8* dst = new u8[dstLen];

	if (dst)
	{
		i32 rcode = uncompress(
				dst,
				reinterpret_cast<unsigned long*>(&dstLen),
				src,
				srcLen);
		if (rcode)
		{
			delete[] dst;

			switch (rcode)
			{
				case Z_BUF_ERROR:
					printf("decompressZLIB(): Decompressed data buffer is too small!\n");
					break;
				case Z_MEM_ERROR:
					printf("decompressZLIB(): Out of memory in ZLIB uncompress() routine!\n");
					break;
				case Z_DATA_ERROR:
					printf("decompressZLIB(): ZLIB uncompress() routine says data is invalid!\n");
					break;
				default:
					printf("decompressZLIB(): Unexpected rcode = %i\n", rcode);
					break;
			}

		}
	}

	return dst;
}

// unoptimized, like PKR_GetLastError above (see the note there).
#pragma optimize("", off)
// @Ok
u8 PKR_ReportError(const char* a1, ...)
{
	va_list va;
	va_start(va, a1);
	VSNPRINTF(gPkrErrorMsg, 0x200, a1, va);
	va_end(va);
	return 1;
}
#pragma optimize("", on)

// @Ok
u8 fileCRCCheck(u8* buf, i32 size, u32 expected)
{
	u32 res = crc32(crc32(0,0,0), buf, size);

	if (res != expected)
	{
		PKR_ReportError("fileCRCCheck: Bad CRC. %08X bad. %08X correct. File is Damaged.", res, expected);
		return 0;
	}

	return 1;
}

// @Ok
u8* PKRComp_DecompressFile(PKR_FILEINFO* pFile, u8* buf, i32 deleteBuf)
{
	if (pFile->compressed == PKRFILE_UNCOMPRESSED)
		return buf;

	// it's always zlib compressed
	// on the original code there's a array of decompressors but we don't need those
	u8* res = decompressZLIB(buf, pFile->compressedSize, pFile->uncompressedSize);
	if (deleteBuf)
		delete buf;

	return res;
}

void validate_PKR_FILEINFO(void)
{
	VALIDATE_SIZE(PKR_FILEINFO, 0x34);

	VALIDATE(PKR_FILEINFO, name, 0x0);
	VALIDATE(PKR_FILEINFO, crc, 0x20);
	VALIDATE(PKR_FILEINFO, compressed, 0x24);
	VALIDATE(PKR_FILEINFO, fileOffset, 0x28);
	VALIDATE(PKR_FILEINFO, uncompressedSize, 0x2C);
	VALIDATE(PKR_FILEINFO, compressedSize, 0x30);
}

void validate_PKR_FOOTER(void)
{
	VALIDATE_SIZE(PKR_FOOTER, 0xC);

	// field_0: used in flushPKR (0x517b8b) as a divisor to round a file
	// offset up ("(offset + field_0 - 1) / field_0 * field_0"), then the gap
	// is written out with fputc(0, fp) in a loop. guess: a sector/alignment
	// size for the packed data (CD-ROM style padding). not renamed since
	// this is a guess, not confirmed against his IDB.
	VALIDATE(PKR_FOOTER, field_0, 0x0);
	VALIDATE(PKR_FOOTER, numDirs, 0x4);
	VALIDATE(PKR_FOOTER, numFiles, 0x8);
}

void validate_PKR_DIRINFO(void)
{
	VALIDATE_SIZE(PKR_DIRINFO, 0x28);

	VALIDATE(PKR_DIRINFO, name, 0x0);
	VALIDATE(PKR_DIRINFO, field_20, 0x20);
	VALIDATE(PKR_DIRINFO, numFiles, 0x24);
}

void validate_LIBPKR_HANDLE(void)
{
	VALIDATE_SIZE(LIBPKR_HANDLE, 0x12C);

	VALIDATE(LIBPKR_HANDLE, fp, 0x0);
	VALIDATE(LIBPKR_HANDLE, name, 0x4);

	VALIDATE(LIBPKR_HANDLE, field_108, 0x108);
	VALIDATE(LIBPKR_HANDLE, mHeader, 0x10C);

	VALIDATE(LIBPKR_HANDLE, mFooter, 0x114);
	VALIDATE(LIBPKR_HANDLE, pDirInfo, 0x120);
	VALIDATE(LIBPKR_HANDLE, pFileInfo, 0x124);
	VALIDATE(LIBPKR_HANDLE, field_128, 0x128);
}

void validate_NODE_DIRINFO(void)
{
	VALIDATE_SIZE(NODE_DIRINFO, 0x30);

	VALIDATE(NODE_DIRINFO, dirInfo, 0x0);
	VALIDATE(NODE_DIRINFO, mNext, 0x28);
	VALIDATE(NODE_DIRINFO, mPrev, 0x2C);
}

void validate_PKR_HEADER(void)
{
	VALIDATE(PKR_HEADER, pkrMagic, 0x0);
	VALIDATE(PKR_HEADER, dirOffset, 0x4);
}

void validate_NODE_FILEINFO(void)
{
	VALIDATE_SIZE(NODE_FILEINFO, 0x14C);

	VALIDATE(NODE_FILEINFO, fileInfo, 0x0);
	VALIDATE(NODE_FILEINFO, pDirInfo, 0x34);
	VALIDATE(NODE_FILEINFO, name, 0x38);

	VALIDATE(NODE_FILEINFO, field_13C, 0x13C);
	VALIDATE(NODE_FILEINFO, mNext, 0x144);
	VALIDATE(NODE_FILEINFO, mPrev, 0x148);
}

#include "my_patch.h"

// @Bogus
void patch_pkr(void)
{
	PATCH_PUSH_RET(0x00517FE9, PKR_Open);
	PATCH_PUSH_RET(0x005186D5, PKR_Close);

	PATCH_PUSH_RET(0x005188D4, PKR_UnlockFile);
	PATCH_PUSH_RET(0x00518883, PKR_LockFile);

	PATCH_PUSH_RET(0x00518C88, PKR_GetFileInfo);

	PATCH_PUSH_RET(0x00519194, PKR_ReadFile);
}
