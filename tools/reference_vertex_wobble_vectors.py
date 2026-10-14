"""Capture original CVertexWobble (0x43B7C0) construction and Move (0x43BB30) against synthetic SModel vertices."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
struct SVECTOR{short vx,vy,vz,pad;};
struct SModel{unsigned short Flags,NumVertices,NumNormals,NumFaces;char header[20];SVECTOR Vertices[16];};
static_assert(offsetof(SModel,Vertices)==28);
unsigned rng;int Rnd(int n){rng=rng*1664525u+1013904223u;return n?(int)(rng%unsigned(n)):0;}
void diagnostic(int,const char*){}
void*alloc(unsigned bytes,int,int,int,int){return std::calloc(1,bytes);}
void release(void*p){std::free(p);}
struct Handle{void*p;unsigned id;};
void*recover(Handle*h){return h&&h->p?h->p:nullptr;}
void* __attribute__((thiscall)) nonRendered(void*p){
 auto*b=(unsigned char*)p;std::memset(b,0,0x3C);b[0x34]=b[0x35]=b[0x36]=1;return p;}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)f-a-5;}
void fillSin(){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;reinterpret_cast<short*>(0x610C48)[2*i]=sin(a)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(a)*4096;}}
// Mem_MakeHandle returns {pointer,id} in eax:edx.
void __attribute__((naked)) makeHandle(){asm volatile("movl 4(%esp),%eax; movl $0xABCDEF01,%edx; ret");}
void setupRegion(SModel*model){
 alignas(4) static unsigned char region[68]={};
 alignas(4) static struct{unsigned count;SModel*ptrs[1];}table{};
 table.count=1;table.ptrs[0]=model;SModel**models=table.ptrs;
 std::memset(region,0,sizeof region);region[0xA]=1;
 *reinterpret_cast<void**>(region+0x14)=models;
 *reinterpret_cast<void**>(region+0x18)=(void*)0x12345678;
 std::memcpy(reinterpret_cast<void*>(0x6B2440),region,68);
}
void printEntries(unsigned char*bit){
 int n=*reinterpret_cast<int*>(bit+0x50);auto*e=*reinterpret_cast<unsigned char**>(bit+0x54);
 std::cout<<'['<<*(short*)(bit+0x58)<<','<<*(short*)(bit+0x5A)<<','<<*(short*)(bit+0x5C)<<','<<n<<",[";
 for(int i=0;i<n;i++){unsigned char*p=e+22*i;if(i)std::cout<<',';
  std::cout<<'['<<*(short*)p<<','<<*(short*)(p+2)<<','<<*(short*)(p+4)<<','<<*(short*)(p+6)<<','<<*(short*)(p+8)<<','<<*(short*)(p+10)<<','
   <<int(p[12])<<','<<*(short*)(p+14)<<','<<*(unsigned short*)(p+16)<<','<<*(short*)(p+18)<<','<<*(short*)(p+20)<<']';}
 std::cout<<"]]";}
void printVerts(SModel*m){std::cout<<'[';for(int i=0;i<m->NumVertices;i++){if(i)std::cout<<',';std::cout<<'['<<m->Vertices[i].vx<<','<<m->Vertices[i].vy<<','<<m->Vertices[i].vz<<']';}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);fillSin();
patch(0x408B10,(void*)&nonRendered);patch(0x4581A0,(void*)&alloc);patch(0x458210,(void*)&release);
patch(0x458360,(void*)&makeHandle);patch(0x4583F0,(void*)&recover);patch(0x4E5DA0,(void*)&Rnd);patch(0x4015B0,(void*)&diagnostic);
// Strip Windows SEH fs:0 registration from CVertexWobble::CVertexWobble and its destructor.
for(unsigned char*c=(unsigned char*)0x43B7C0;c<(unsigned char*)0x43BC40;c++){
 if(c[0]==0x64&&c[1]==0xa1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xc0;std::memset(c+2,0x90,4);}
 if(c[0]==0x64&&c[1]==0x89&&!*(unsigned*)(c+3))std::memset(c,0x90,7);}
*reinterpret_cast<void**>(0x56EAD8)=nullptr;*reinterpret_cast<unsigned*>(0x56EB48)=0;
char mode;while(std::cin>>mode){
 if(mode=='C'||mode=='M'){int count,a5,a6,a7,a8,frames;unsigned seed;std::cin>>count>>a5>>a6>>a7>>a8>>seed>>frames;
  std::vector<unsigned char>indices(count);int x;for(int i=0;i<count;i++){std::cin>>x;indices[i]=(unsigned char)x;}
  SModel model{};model.NumVertices=16;for(int i=0;i<16;i++){std::cin>>x;model.Vertices[i].vx=x;std::cin>>x;model.Vertices[i].vy=x;std::cin>>x;model.Vertices[i].vz=x;}
  setupRegion(&model);rng=seed;alignas(4) unsigned char bit[0x60]={};
  reinterpret_cast<void*(__attribute__((thiscall))*)(void*,unsigned,unsigned,unsigned,unsigned char*,int,int,int,int)>(0x43B7C0)
   (bit,0,0,count,indices.data(),a5,a6,a7,a8);
  std::cout<<'[';printEntries(bit);std::cout<<',';printVerts(&model);
  if(mode=='M'){
   int safe=1;auto*e=*reinterpret_cast<unsigned char**>(bit+0x54);for(int i=0;i<count;i++)if(*(short*)(e+22*i+14)==0)safe=0;
   std::cout<<",[";if(safe)for(int f=0;f<frames;f++){if(f)std::cout<<',';
    reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x43BB30)(bit);printVerts(&model);}std::cout<<']';}
  std::cout<<"]\n";release(*reinterpret_cast<void**>(bit+0x54));}
 else{int level;unsigned seed;std::cin>>level>>seed;(void)level;SModel model{};model.NumVertices=16;
  for(int i=0;i<16;i++){model.Vertices[i].vx=short(i*23-31);model.Vertices[i].vy=short(i*17-13);model.Vertices[i].vz=short(i*11+7);}
  setupRegion(&model);rng=seed;*reinterpret_cast<void**>(0x56EAD8)=nullptr;
  unsigned char ia[6]={2,3,4,9,8,7},ib[6]={11,10,0,1,6,5};
  alignas(4) unsigned char a[0x60]={},b[0x60]={};rng=seed;
  reinterpret_cast<void*(__attribute__((thiscall))*)(void*,unsigned,unsigned,unsigned,unsigned char*,int,int,int,int)>(0x43B7C0)
   (a,0,0,6,ia,0x50,0x50,0xC8,0x96);
  reinterpret_cast<void*(__attribute__((thiscall))*)(void*,unsigned,unsigned,unsigned,unsigned char*,int,int,int,int)>(0x43B7C0)
   (b,0,0,6,ib,0x50,0x50,0xC8,0x96);
  std::cout<<'[';printEntries(a);std::cout<<',';printEntries(b);std::cout<<',';printVerts(&model);std::cout<<"]\n";
  release(*reinterpret_cast<void**>(a+0x54));release(*reinterpret_cast<void**>(b+0x54));}
}
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x43B7C0)
    cases = []

    def verts():
        return [v for i in range(16) for v in (i * 23 - 31, i * 17 - 13, i * 11 + 7)]

    for indices, a5, a6, a7, a8 in [
        ([2, 3, 4, 9, 8, 7], 0x50, 0x50, 0xC8, 0x96),
        ([11, 10, 0, 1, 6, 5], 0x50, 0x50, 0xC8, 0x96),
        ([0, 1, 2], 53, 53, 133, 100),
        ([5, 7, 9, 11], 20, 15, 300, 300),
    ]:
        for seed in [0, 1, 99, 0x00ABCDEF]:
            cases.append(dict(mode='C', input=[len(indices), a5, a6, a7, a8, seed, 0, *indices, *verts()]))
            cases.append(dict(mode='M', input=[len(indices), a5, a6, a7, a8, seed, 4, *indices, *verts()]))
    for _ in range(40):
        count = rng.choice([3, 4, 6])
        indices = rng.sample(range(12), count)
        a5, a6 = rng.randrange(1, 100), rng.randrange(1, 100)
        a7, a8 = rng.randrange(1, 400), rng.randrange(1, 400)
        seed = rng.randrange(2**31)
        base = [count, a5, a6, a7, a8, seed]
        # Keep the spread verts(): identical corners make distance0 and the original idiv faults.
        cases.append(dict(mode='C', input=[*base, 0, *indices, *verts()]))
        cases.append(dict(mode='M', input=[*base, 3, *indices, *verts()]))
    for level in [0x701, 0x803]:
        for seed in [0, 7, 12345]:
            cases.append(dict(mode='W', input=[level, seed]))

    rows = []
    for case in cases:
        rows.append(case['mode'] + ' ' + ' '.join(map(str, case['input'])))

    with tempfile.TemporaryDirectory(prefix='openspidey-vertex-wobble-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fwrapv', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)],
                       check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row))
                for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    out = root / 'modern/OpenSpidey.Tests/Fixtures/vertex-wobble.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original CVertexWobble constructor/Move/MakeVertexWibbler cases.')


if __name__ == '__main__':
    main()
