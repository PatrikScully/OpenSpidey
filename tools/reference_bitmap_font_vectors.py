"""Compile recovered original font construction/mapping/layout against genuine archived fonts."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import struct
import subprocess
import tempfile
import zlib
from reference_ground_response_vectors import controller_method


def font_method(source, signature):
    start = source.index(signature)
    body = source.index('{', start)
    depth = 0
    tokens = re.finditer(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/|[{}]', source[body:])
    for match in tokens:
        token = match.group()
        if token == '{': depth += 1
        elif token == '}':
            depth -= 1
            if depth == 0: return source[start:body + match.end()]
    raise ValueError(f'Missing recovered body for {signature}')


def font_entries(path):
    data = path.read_bytes(); directory = struct.unpack_from('<I', data, 4)[0]
    _, directories, count = struct.unpack_from('<3I', data, directory)
    table = directory + 12 + directories * 40
    for index in range(count):
        record = table + 52 * index
        name = data[record:record + 32].split(b'\0')[0].decode('ascii')
        if not name.lower().endswith('.fnt'): continue
        _, compression, offset, _, size = struct.unpack_from('<5I', data, record + 32)
        packed = data[offset:offset + size]
        yield name, zlib.decompress(packed) if compression == 2 else packed


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--archive', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    source = (root/'src/game/ui/FontTools.cpp').read_text()
    code = r'''
#include "core/types.h"
#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>
#include <iterator>
#include <vector>
#undef FASTCALL
#define FASTCALL
std::vector<int>events;int imageIndex=0;u16 palette[16],gSlicedImageRelated[256];int Pal16X=0,Pal16Y=0;
void print_if_false(int,const char*){}void*DCMem_New(int size,int,int,int,int){return std::calloc(1,size);}
void Mem_Delete(void*p){std::free(p);}int GetFree16Slot(){return 0;}u16*PCTex_CreateClut(int){return palette;}
void _LoadImage(){}int GetClut(int,int){return 42;}
int G_DIFFICULTY_LEVEL=2;struct Controls{int DigitalMapping[20];}G_SCONTROL[1];
struct SDataGlyph{int mSliceWidth,mHeight,mBaseline,mWidth;};
struct SlicedImage2{
 int index,width,height;u8 field_4=0,field_6=0,field_7=0,field_8=0,field_9=0,field_A=0,field_B=0,Shaded=0;unsigned field_C=0;std::vector<u8>pixels;
 SlicedImage2(void*data,int w,int h,int,int,int,int,int):index(imageIndex++),width(w),height(h),pixels(static_cast<u8*>(data),static_cast<u8*>(data)+w*h/2){}
 void pack(){}void removeFromMemory(){}
 void draw(int x,int y,int flags,float depth){int r=field_7,g=field_8,b=field_9;if(field_4&&field_6==2){r=255-r;g=255-g;b=255-b;}
  events.insert(events.end(),{4,index,x,y,int(field_C),r,g,b,flags|1,int(depth*8),field_4!=0});}
};
struct FontCharacter{SlicedImage2*pImage;u8 W,H,Baseline,field_7;};
struct Font{
 int field_4=0,field_8=0,field_C=0,field_10=0;unsigned mRed=0,mGreen=0,mBlue=0;u8 field_20=0,field_21=0;
 int field_24=0,field_28=0,field_2C=0,field_30=0,field_34=0;char field_38[16];FontCharacter*pCharTab=nullptr;int NumChars=0,Clut=0;
 int field_54=0,field_58=0;u8 mSavedRed=0,mSavedGreen=0,mSavedBlue=0,field_5F[256];u8*field_160=nullptr;
 Font(u8*,const char*);~Font(){for(int i=0;i<NumChars+1;i++)delete pCharTab[i].pImage;std::free(pCharTab);}
 void SetCharMap(int);char getCharIndex(char);int isEscapeChar(char);void handleEscapeChar(char);int fixedCharWidth(char);
 int width(const char*);int heightAboveBaseline(char*);int heightBelowBaseline(char*);void draw(int,int,const char*,int,float);
};
void metrics(void*,int*width,int*baseline){events.insert(events.end(),{1,*width,*baseline});*width+=31;*baseline-=17;}
void colour(void*,u8*r,u8*g,u8*b){events.insert(events.end(),{2,*r,*g,*b});*r+=3;*g+=5;*b+=7;}
void drawn(void*,int x,int before,int after,int index){events.insert(events.end(),{3,x,before,after,index});}
'''
    for signature in ['Font::Font(\n', 'void Font::SetCharMap(', 'char Font::getCharIndex(', 'int Font::isEscapeChar(',
                      'void Font::handleEscapeChar(', 'i32 Font::fixedCharWidth(', 'i32 Font::width(',
                      'i32 Font::heightAboveBaseline(', 'i32 Font::heightBelowBaseline(', 'void Font::draw(']:
        code += font_method(source, signature) + '\n'
    code += controller_method((root/'src/engine/rendering/PCTex.cpp').read_text(), 'u8 ConvertPSXPaletteToPC(') + '\n'
    code += r'''
int main(int argc,char**argv){std::ifstream file(argv[1],std::ios::binary);std::vector<u8>bytes((std::istreambuf_iterator<char>(file)),{});Font font(bytes.data(),argv[2]);
 u16 pc[16];int alpha=ConvertPSXPaletteToPC(palette,pc,16,1);std::cout<<"{\"glyphs\":[";
 for(int i=0;i<=font.NumChars;i++){if(i)std::cout<<',';auto&g=font.pCharTab[i];std::cout<<"{\"metrics\":["<<g.pImage->width/4<<','<<int(g.H)<<','<<int(g.Baseline)<<','<<int(g.W)<<"],\"rgbaHex\":\"";char hex[]="0123456789abcdef";
 for(u8 pair:g.pImage->pixels)for(int shift:{0,4}){u16 color=pc[(pair>>shift)&15];int r=(color>>(alpha?10:11))&31,g=(color>>5)&(alpha?31:63),b=color&31;u8 channels[4]={u8((r<<3)|(r>>2)),u8(alpha?((g<<3)|(g>>2)):((g<<2)|(g>>4))),u8((b<<3)|(b>>2)),u8(alpha?(color&0x8000?255:0):255)};for(u8 c:channels)std::cout<<hex[c>>4]<<hex[c&15];}
 std::cout<<"\"}";}
 std::cout<<"],\"cases\":[";int v[13],caseIndex=0;while(std::cin>>v[0]){for(int i=1;i<13;i++)std::cin>>v[i];std::string text;std::cin>>text;std::replace(text.begin(),text.end(),'@',' ');
 font.SetCharMap(v[0]);font.field_34=v[1];font.field_C=v[2];font.field_4=v[3];font.field_8=v[4];font.field_30=v[5];font.field_21=v[6];font.field_24=v[7];font.field_28=v[8];font.field_2C=v[9];font.mRed=128;font.mGreen=64;font.mBlue=32;font.mSavedRed=17;font.mSavedGreen=19;font.mSavedBlue=23;
 void*table[9]={};table[5]=reinterpret_cast<void*>(&metrics);table[6]=reinterpret_cast<void*>(&colour);table[8]=reinterpret_cast<void*>(&drawn);void**tablePointer=table;font.field_54=v[12]?reinterpret_cast<int>(&tablePointer):0;
 int width=font.width(text.c_str()),above=font.heightAboveBaseline(text.data()),below=font.heightBelowBaseline(text.data());events.clear();font.draw(v[10],v[11],text.c_str(),0,.125f);
 if(caseIndex++)std::cout<<',';std::cout<<"{\"measure\":["<<width<<','<<above<<','<<below<<"],\"colour\":["<<font.mRed<<','<<font.mGreen<<','<<font.mBlue<<','<<int(font.mSavedRed)<<','<<int(font.mSavedGreen)<<','<<int(font.mSavedBlue)<<"],\"events\":[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}";
 }std::cout<<"]}\n";
}
'''
    assets = []
    with tempfile.TemporaryDirectory(prefix='bitmap-font-source-') as temporary:
        directory = Path(temporary); (directory/'reference.cpp').write_text(code)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fsigned-char','-I',str(root/'src'),str(directory/'reference.cpp'),'-o',str(directory/'reference')],check=True)
        for name, original in font_entries(args.archive):
            (directory/'font.bin').write_bytes(original)
            cases = []
            for text in ['0123456789:','A@B?!.','[BLUE]{RED}<GRAY>|PINK>~GREEN>^','/UNKNOWN/','abc']:
                for mapping in [0,1,2]:
                    for scale in [2048,3800,4096,8192]:
                        for fixed in [0,1]:
                            for callback in [0,1]:
                                for align in [0,1,2]:
                                    cases.append({'text':text.replace('@',' '),'input':[mapping,scale,-1,align,(align+1)%4,fixed,1,1,-1,255,100,-10,callback]})
            lines = [' '.join(map(str,c['input']))+' '+c['text'].replace(' ','@') for c in cases]
            output = json.loads(subprocess.run([str(directory/'reference'),str(directory/'font.bin'),name],input='\n'.join(lines),text=True,capture_output=True,check=True).stdout)
            for glyph in output['glyphs']:
                glyph['rgbaSha256'] = hashlib.sha256(bytes.fromhex(glyph.pop('rgbaHex'))).hexdigest()
            for c,gold in zip(cases,output['cases'],strict=True):c['expected']=gold
            assets.append({'name':name,'sha256':hashlib.sha256(original).hexdigest(),'palette':list(struct.unpack_from('<16H',original,4+16*struct.unpack_from('<I',original)[0])),
                           'glyphs':output['glyphs'],'cases':cases})
    destination=root/'modern/OpenSpidey.Tests/Fixtures/bitmap-font.json'
    destination.write_text(json.dumps({'scope':'Compiled recovered Font constructor/mapping/measurement/draw plus original PC paletteconversion; actual archive glyph pixel hashes, no replacement font.', 'fonts':assets},separators=(',',':'))+'\n')
    print(f'Compiled original bitmap font behaviour: {len(assets)} genuine fonts, {sum(len(f["cases"]) for f in assets)} layout comparisons.')


if __name__=='__main__':main()
