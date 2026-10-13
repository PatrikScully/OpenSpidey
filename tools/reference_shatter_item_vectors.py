"""Execute original PC Shatter_Item/Face/Split over synthetic bounded source models; preserve full emission/RNG order."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_shatter_fragment_vectors import build_reference

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=build_reference(root).split('int main(')[0]+r'''
std::vector<unsigned char*>allocated;std::vector<std::vector<long long>>traces;
void*allocation(unsigned size){auto*p=(unsigned char*)std::calloc(1,size);allocated.push_back(p);return p;}
void initLine(unsigned char*p){*(void**)(p+0x68)=nullptr;}
void zone(unsigned char*p,int env){assert(env==1);std::vector<long long>t;for(int i=0;i<6;i++)t.push_back(((int*)p)[i]);traces.push_back(t);*(void**)(p+0x68)=(void*)1;*(int*)(p+0x70)=1000<<12;}
void invTexture(int id,float*w,float*h){assert(id==99);*w=1.0f/32;*h=1.0f/16;}
void*rotation(void*,short*out){std::memset(out,0,32);out[0]=out[4]=out[8]=4096;return out;}
int squareRoot(int v){return v<=-32768?32768:v<0?0:int(std::sqrt(v));}
int main(int argc,char**argv){assert(argc==2);setup(argv[1]);patch(0x4088A0,(void*)&allocation);patch(0x4524C0,(void*)&initLine);patch(0x4549A0,(void*)&zone);patch(0x50F180,(void*)&invTexture);patch(0x46E730,(void*)&rotation);patch(0x46D430,(void*)&squareRoot);
 for(unsigned addr:{0x48C730u,0x48D0F7u}){unsigned char*p=(unsigned char*)addr;assert(p[0]==0x64);p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);}
 for(unsigned addr:{0x48C743u,0x48CE9Au,0x48D0FEu,0x48D59Cu}){unsigned char*p=(unsigned char*)addr;assert(p[0]==0x64);std::memset(p,0x90,7);}

 int count,check,mark,low,px,py,pz,nx,ny,nz;unsigned seed;
 while(std::cin>>count>>check>>mark>>low>>px>>py>>pz>>nx>>ny>>nz>>seed){
  std::vector<unsigned>flags(count),colors(count),states(count);for(int i=0;i<count;i++)std::cin>>flags[i]>>colors[i]>>states[i];
  *(void**)0x56E9A0=nullptr;*(void**)0x56EA34=nullptr;*(int*)0x60D224=low;*(int*)0x6A7690=0;
  unsigned char item[40]={},texture[40]={};int pos[3]={px,py,pz};std::memcpy(item+8,pos,12);texture[2]=99;texture[4]=31;texture[9]=15;texture[10]=31;texture[11]=15;*(unsigned*)(texture+12)=4u*((unsigned(-16)&0xF8u)|0xF8800000u)|((unsigned(-32)>>3)&0x1Fu);
  std::vector<unsigned char>model(28+5*8+count*32);*(unsigned short*)(model.data()+2)=4;*(unsigned short*)(model.data()+4)=1;*(unsigned short*)(model.data()+6)=count;
  short vertices[4][4]={{-301,-201,0,0},{401,-201,0,0},{-301,501,0,0},{401,501,0,0}};std::memcpy(model.data()+28,vertices,sizeof vertices);short normal[4]={short(nx),short(ny),short(nz),0};std::memcpy(model.data()+60,normal,8);
  for(int i=0;i<count;i++){auto*f=model.data()+68+i*32;*(unsigned*)f=(8u<<18)|flags[i];if(!(flags[i]&0x40))*(unsigned*)f^=0x80;for(int j=0;j<4;j++)f[4+j]=j;*(unsigned*)(f+8)=colors[i];if(!(flags[i]&0x800))*(unsigned*)(f+8)&=0xFFFFFF;*(unsigned*)(f+12)=states[i]<<16;*(void**)(f+16)=texture;}
  void*models[]={model.data()};*(void**)0x6B2454=models;unsigned table[256];for(unsigned i=0;i<256;i++)table[i]=i|(i*3%256)<<8|(i*5%256)<<16;*(void**)0x6B2468=table;
  rng=seed;ranges.clear();traces.clear();using Shatter=int(*)(void*,int,int);int result=((Shatter)0x48CFF0)(item,check,mark);
  std::cout<<"{\"result\":"<<result<<",\"flags\":"<<*reinterpret_cast<unsigned short*>(item+4)<<",\"faceFlags\":[";for(int i=0;i<count;i++){if(i)std::cout<<',';std::cout<<*(unsigned*)(model.data()+68+i*32);}std::cout<<"],\"faceStates\":[";for(int i=0;i<count;i++){if(i)std::cout<<',';std::cout<<*(unsigned short*)(model.data()+68+i*32+14);}std::cout<<"],\"pieces\":[";
  for(unsigned i=0;i<allocated.size();i++){if(i)std::cout<<',';auto*p=allocated[i];int kind=*(unsigned*)p==0x53BE88?0:1;auto state=snap(p,kind);if(kind==0){state.push_back(*(unsigned*)(p+0xC8));for(int j=0;j<6;j++)state.push_back(int(*(float*)(p+0x94+j*4)*(j%2?16:32)));}state.insert(state.begin(),kind);print(state);}
  std::cout<<"],\"ranges\":[";for(unsigned i=0;i<ranges.size();i++){if(i)std::cout<<',';std::cout<<ranges[i];}std::cout<<"],\"traces\":[";for(unsigned i=0;i<traces.size();i++){if(i)std::cout<<',';print(traces[i]);}std::cout<<"]}\n";
  for(auto*p:allocated)std::free(p);allocated.clear();
 }
}
'''
    cases=[];rng=random.Random(0x48CFF0)
    for index in range(96):
        count=rng.choice([1,2,6,8,12]);check=index%3==0;mark=index%4!=0;low=index%11==0
        faces=[[rng.choice([0x13,3,0x40,0x50,0x33,0x803,0x813]),rng.randrange(1<<32),rng.choice([0,1,8,9])] for _ in range(count)]
        cases.append([count,int(check),int(mark),int(low),*[rng.randint(-30000,30000)*4096+rng.randrange(4096) for _ in range(3)],*[rng.randint(-4096,4096) for _ in range(3)],rng.randrange(1<<32),*sum(faces,[])])
    with tempfile.TemporaryDirectory(prefix='shatter-item-pc-') as temp:
        path=Path(temp);(path/'reference.cpp').write_text(code);inputs='\n'.join(' '.join(map(str,c)) for c in cases);(path/'input').write_text(inputs)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),str(root/'src/core/fixed_vector.cpp'),'-o',str(path/'reference')],check=True)
        result=subprocess.run([str(path/'reference'),str(args.original_exe)],input=inputs,text=True,capture_output=True)
        if result.returncode:
            subprocess.run(['gdb','-q','-batch','-ex',f'run {args.original_exe} < {path}/input','-ex','bt','-ex','x/8i $pc','--args',str(path/'reference'),str(args.original_exe)],check=False);raise RuntimeError(result.stderr)
    (root/'modern/OpenSpidey.Tests/Fixtures/shatter-item.json').write_text(json.dumps({'originalPcItemFaceSplitEmissionVerified':True,'cases':[{'input':c,**json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
    print(f'Original PC complete Item/Face/Split emissions captured: {len(cases)} source models.')
if __name__=='__main__':main()
