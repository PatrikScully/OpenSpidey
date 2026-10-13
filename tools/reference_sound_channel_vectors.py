"""Compile original SFX allocation/play/stop/position routines with deterministic playback status."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_spatial_sound_vectors import harness


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/engine/audio/ps2lowsfx.cpp').read_text()
    code=harness(root)+r'''
using f32=float;
struct Entry {int field_0=0,field_4=0,field_8=0,field_C=0,field_10=0,field_14=0,field_16=0,field_18=0,field_1A=0,field_1B=0,field_1C=0,field_20=0;u32 field_24=0;};
Entry G_SFX_ENTRIES[32];bool playing[32]={};int pans[32]={};
using SSfxEntry=Entry;
int G_VOICE_INDEX=1,G_NUM_VOICES=0,G_GAMESTATE[14]={},G_BOOT_ROM_SOUND_MODE=0,G_SFX_PLAY_RELATED=0;
u32 G_SFX_ARRAY_ONE[192]={},G_SFX_LEVEL_SPECIFIC_ARRAY[192]={},G_SFX_FALLOFF_ARRAY[32]={};
struct Asset {int field_C=44100,field_14=16;};struct Bank {int field_4=1;Asset mAssets[128];};Bank G_SOUND_BANK,G_SFX_RELATED_OUT_LEVEL;
using SSFXBank=Bank;
void DoAssert(int,const char*){}void error(const char*){}void nullsub_3(){}
void SFX_KillVoice(u32);void SFX_FreeVoice(int);void SFX_ModifyVol(u32,int,int);
int DXSOUND_IsPlaying(int c){return playing[c];}void DXSOUND_Stop(int c){playing[c]=false;}
void DXSOUND_Close(int){}void DXSOUND_Open(int,int,int){}void DXSOUND_SetVolume(int,int){}
void DXSOUND_SetPan(int c,int p){pans[c]=p;}void DXSOUND_SetPitch(int,int){}
void DXSOUND_Play(int c,int){playing[c]=true;}
int DCSFX_AdjustVol(int){return 0;}
u32 translateLevelSpecificAliasToIndex(u32 alias){return alias>=80 && alias<88 ? alias-80 : 0xFFFFFFFF;}
'''
    for signature in [' i32 PSXPitchToDCPitch(i32 a1)',' i32 SFX_AllocVoice(i32 a1, bool a2)',
                      ' void SFX_FreeVoice(i32 a1)',' void SFX_KillVoice(u32 a1)','u32 playSFX(',
                      'u32 SFX_Play(','u32 SFX_PlayPos(','void SFX_ModifyVol(','void SFX_ModifyPos(',' void SFX_Stop(u32 a1)']:
        code+=controller_method(source,signature)+'\n'
    code+=r'''
int main(){InitializeHeading();for(int i=0;i<3;i++)gCameraViewMatrix[i][i]=4096;
 int pitches[]={60,59,61,0,255,127,64,80};
 for(int i=0;i<8;i++){G_SFX_ARRAY_ONE[i*3]=1|(i<<16)|(i%2 ? 0x80000000 : 0);
 G_SFX_ARRAY_ONE[i*3+1]=pitches[i]|((4096+i*100)<<16);G_SFX_ARRAY_ONE[i*3+2]=1024+i*1000;
 for(int j=0;j<3;j++)G_SFX_LEVEL_SPECIFIC_ARRAY[i*3+j]=G_SFX_ARRAY_ONE[i*3+j];G_SFX_LEVEL_SPECIFIC_ARRAY[i*3]|=0x40000000;}
 int previous=-1,sequence,kind,volume,channel,master,mono;u32 sound;CVector position;
 while(std::cin>>sequence>>kind>>sound>>volume>>channel>>master>>mono>>position.vx>>position.vy>>position.vz){
  if(previous!=sequence){for(int i=0;i<32;i++){G_SFX_ENTRIES[i]=Entry();playing[i]=false;pans[i]=0;G_SFX_FALLOFF_ARRAY[i]=0;}G_NUM_VOICES=0;G_VOICE_INDEX=1;previous=sequence;}
  G_GAMESTATE[12]=master;G_BOOT_ROM_SOUND_MODE=mono;u32 result=0;
  switch(kind){case 0:result=SFX_Play(sound,(i16)volume,0);break;case 1:playing[channel]=false;break;
  case 2:SFX_Stop(sound);break;case 3:result=SFX_PlayPos(sound,&position,0);break;case 4:SFX_ModifyPos(sound,&position,0);break;}
  std::cout<<'['<<result<<','<<G_NUM_VOICES;
  for(int i=0;i<32;i++){auto&e=G_SFX_ENTRIES[i];std::cout<<','<<e.field_1A<<','<<e.field_1B<<','<<playing[i]<<','<<e.field_16<<','<<pans[i]<<','<<e.field_20;}
  std::cout<<"]\n";
 }
}
'''
    rng=random.Random(0x472490);sequences=[];lines=[]
    for sequence in range(16):
        steps=[]
        for step in range(100):
            kind=0 if step<40 else rng.choice([0,0,1,2,3,4])
            sound=rng.randint(1,8) if kind in [0,3] else rng.getrandbits(32)
            if kind in [0,3] and rng.randrange(2):sound=0x8000+79+sound
            command=dict(kind=kind,sound=sound,volume=rng.choice([0,8192,-8192,16383]),channel=rng.randint(0,31),master=rng.choice([0,0x2FFF,16383]),mono=bool(rng.randrange(2)),position=[rng.randint(-10000<<12,10000<<12) for _ in range(3)])
            steps.append(command)
            lines.append(' '.join(map(str,[sequence,kind,sound,command['volume'],command['channel'],command['master'],int(command['mono']),*command['position']])))
        sequences.append(steps)
    with tempfile.TemporaryDirectory(prefix='openspidey-sfx-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        results=iter(subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines())
    for steps in sequences:
        for command in steps:command['expected']=json.loads(next(results))
    (root/'modern/OpenSpidey.Tests/Fixtures/sound-channels.json').write_text(json.dumps(sequences,separators=(',',':'))+'\n')
    print(f'Generated {len(sequences)} source sound-channel sequences / {len(lines)} operations.')


if __name__=='__main__':main()
