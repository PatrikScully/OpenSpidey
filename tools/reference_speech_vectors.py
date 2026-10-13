"""Compile recovered Redbook speech control and its vblank update with a synthetic backend."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    catalog = json.loads((root/'modern/OpenSpidey.Core/Audio/speech-catalog.json').read_text())
    audio = (root/'src/engine/audio/ps2redbook.cpp').read_text()
    utils = (root/'src/core/utils.cpp').read_text()
    harness = '#include <iostream>\n#include <cstdint>\nusing i32=int;using u8=uint8_t;using u16=uint16_t;\n'
    globals = ['G_REDBOOK_BUSY','G_REDBOOK_XA_PAUSED','G_REDBOOK_XA_CURRENT_PRIORITY','G_CARNAGE_XA_RELATED_TWO',
               'G_PENDING_XA_ONE','G_PENDING_XA_TWO','G_PENDING_XA_THREE','G_FILE_IO_STATUS','G_GAME_FMV_ACTIVE',
               'G_XA_ALLOWED','G_CARNAGE_XA_RELATED','G_REDBOOK_XA_RELATED_ONE','G_REDBOOK_XA_RELATED_TWO',
               'G_REDBOOK_RELATED_ONE','G_REDBOOK_RELATED_TWO','G_REDBOOK_RELATED_THREE','G_ADXT_INITIALIZED',
               'G_SB_USE_SEMAPHORES','G_SB_SEMAPHORE_ONE','G_SB_SEMAPHORE_TWO','G_ADXT','G_POST_WATER_EFFECT','G_GAME_FADE']
    harness += 'int '+','.join(name+'=0' for name in globals)+';int G_GAMESTATE[14]={};\n'
    harness += 'int G_XA_TRACK_IDS[]={'+','.join('1' if v else '-1' for v in catalog['Available'])+'};\n'
    harness += 'int hasMedia[]={'+','.join('0' if v=='blank.bik' else '1' for v in catalog['Names'])+'};\n'
    harness += r'''
int finished=1,plays=0,stops=0,pauses=0,lastTrack=-1;
void Redbook_XAInit(){G_ADXT_INITIALIZED=1;}
void Redbook_XASetVol(int){}
void Sb_SemWait(int){}void Sb_SemSignal(int){}
void ADXT_StartAfs(int,int,int track){lastTrack=track;plays++;finished=!hasMedia[track];}
void ADXT_Stop(int){stops++;finished=1;}
void ADXT_Pause(int,int){pauses++;}
int ADXT_GetStat(int){return finished ? 4 : 0;}
void print_if_false(int,const char*){}
'''
    for signature in ['void Redbook_XAReset(void)','void Redbook_XAStop(void)','void Redbook_XAPause(bool pause)',
                      'u8 Redbook_XAPlay(int a1, int a2, int a3)']:
        harness += controller_method(audio,signature)+'\n'
    harness += controller_method(utils,'void Utils_VblankProcessing(void)')+'\n'
    harness += r'''
int main(){int sequence,kind,bank,slot,priority,water,io,movie;int previous=-1;
while(std::cin>>sequence>>kind>>bank>>slot>>priority>>water>>io>>movie){
 if(sequence!=previous){Redbook_XAReset();G_ADXT_INITIALIZED=1;finished=1;plays=stops=pauses=0;lastTrack=-1;previous=sequence;}
 G_POST_WATER_EFFECT=water;G_FILE_IO_STATUS=io;G_GAME_FMV_ACTIVE=movie;int result=-1;
 switch(kind){case 0:result=Redbook_XAPlay(bank,slot,priority);break;case 1:Utils_VblankProcessing();break;
 case 2:Redbook_XAStop();break;case 3:Redbook_XAPause(bank);break;case 4:finished=1;break;case 5:G_XA_ALLOWED=bank!=0;break;}
 std::cout<<'['<<result<<','<<G_REDBOOK_BUSY<<','<<G_REDBOOK_XA_PAUSED<<','<<G_REDBOOK_XA_CURRENT_PRIORITY<<','
 <<G_CARNAGE_XA_RELATED_TWO<<','<<G_PENDING_XA_ONE<<','<<G_PENDING_XA_TWO<<','<<G_PENDING_XA_THREE<<','
 <<G_XA_ALLOWED<<','<<plays<<','<<stops<<','<<pauses<<','<<lastTrack<<"]\n";
}}
'''
    rng = random.Random(0x479ee0)
    cases = []
    for _ in range(60):
        operations = [[0,67,15,250,0,0,0],[0,0,16,251,0,0,0],[0,1,-1,252,0,0,0],
                      [0,0,0,200,0,0,0],[0,0,0,251,0,0,0],[4,0,0,0,0,0,0],
                      [1,0,0,0,0,0,0],[0,0,1,10,0,0,0],[0,0,2,20,0,0,0]]
        operations += [[1,0,0,0,0,0,0] for _ in range(30)]
        for _ in range(40):
            operations.append([rng.choice([0,0,1,1,2,3,4,5]),rng.choice([0,1,3,67,78,79]),
                               rng.randrange(16),rng.choice([-1,0,1,10,200,250,251]),rng.randrange(2),rng.randrange(2),rng.randrange(2)])
        cases.append({'operations':operations,'expected':[]})
    lines = [str(index)+' '+' '.join(map(str,op)) for index,c in enumerate(cases) for op in c['operations']]
    with tempfile.TemporaryDirectory(prefix='openspidey-speech-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference'
        cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=iter(subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines())
    for case in cases:case['expected']=[json.loads(next(results)) for _ in case['operations']]
    (root/'modern/OpenSpidey.Tests/Fixtures/speech.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f"Generated {len(cases)} speech sequences / {sum(len(c['operations']) for c in cases)} operations.")


if __name__=='__main__':main()
