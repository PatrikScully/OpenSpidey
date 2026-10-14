"""Observe whole PC Jonah movement/hiding/hit/death and their concrete waypoint callees."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_jonah_brain_vectors import HARNESS as BRAIN_HARNESS, FIELDS


def harness():
    header, original_main = BRAIN_HARNESS.split('int main(int argc,char**argv)', 1)
    header = header.replace('std::vector<long long>snapshot()', 'void extraSnapshot(std::vector<long long>&);\nstd::vector<long long>snapshot()')
    header = header.replace('v.push_back(at<int>(procedure,16));return v;', 'v.push_back(at<int>(procedure,16));extraSnapshot(v);return v;')
    header = header.replace('return randomValue%maximum;', 'return maximum?randomValue%maximum:0;')
    header = header.replace('void*recover(void*handle)', 'void*recoverScorpion(void*handle)')
    header = header.replace('unsigned long long makeHandle(void*p)', 'unsigned long long makeScorpionHandle(void*p)')
    header = header.replace('void* __attribute__((thiscall))sendMessage(', 'void* __attribute__((thiscall))sendWaypointMessage(')
    extra = r'''
alignas(4)unsigned char environment[0x414],camera[0x400];
int environmentExists,environmentNode,environmentRecovery,held,role,reaction,obstruction,nextNode,hasNext,pathCount,hasCamera,initialAngle,suppressed,levelStatus,scorpionDistance,successor,health,hitFlags,attackKind,damage;unsigned completion;
struct Link{int node,type,role,parameter;};std::vector<Link>primary,secondary;
void extraSnapshot(std::vector<long long>&v){for(unsigned o:{0x2F8u,0x310u,0x318u,0x330u})v.push_back(at<int>(actor,o));
 v.push_back(at<void*>(actor,0x364)==environment?1:0);v.push_back(at<unsigned>(actor,0x368));
 for(unsigned o:{0x36Cu,0x370u,0x1F0u})v.push_back(at<int>(actor,o));v.push_back(at<unsigned short>(actor,0xDC));
 v.push_back(at<unsigned char>(camera,0x100));v.push_back(at<void*>(camera,0xFC)==actor?1:0);v.push_back(at<unsigned char>(camera,0x140));v.push_back(at<void*>(camera,0x13C)==actor?1:0);
 v.push_back(at<short>(camera,0x236));v.push_back(suppressed);v.push_back(mem<int>(0x60CFA4));v.push_back(at<short>(actor,0xE2));}
void*recover(void*handle){if(handle==actor+0x35C)return recoverScorpion(handle);assert(handle==actor+0x364);event(30);return environmentRecovery&&at<void*>(handle,0)==environment?environment:nullptr;}
unsigned long long makeHandle(void*p){if(p==scorp)return makeScorpionHandle(p);assert(p==environment);event(29);return (static_cast<unsigned long long>(777)<<32)|(unsigned)p;}
int readLinks(int node,Link*out,int maximum){event(28,{node,maximum});auto&links=node==15?primary:secondary;assert(maximum==4&&links.size()<=4);std::memcpy(out,links.data(),links.size()*sizeof(Link));return links.size();}
void* __attribute__((thiscall))findEnvironment(void*,int node,void*){event(31,{node});return environmentExists&&node==environmentNode?environment:nullptr;}
void* __attribute__((thiscall))sendMessage(void*p,void*from,void*to,int code,unsigned*data){if(code==9)return sendWaypointMessage(p,from,to,code,data);assert(p==procedure&&from==actor&&to==scorp&&code==8&&data[1]==1&&data[2]==0);event(32,{code,data[3]});return p;}
void* __attribute__((thiscall))move(void*p,void*self,int*data,int mask){assert(p==procedure&&self==actor);event(33,{data[0],data[1],data[2],data[3],data[4],data[5],mask});return p;}
int __attribute__((thiscall))script(void*){event(34,{at<int>(actor,0x1F4)});return 1;}
void __attribute__((thiscall))suppress(void*self,int ticks){assert(self==player);event(35,{ticks});suppressed=ticks;}
void __attribute__((thiscall))camMode(void*,int mode){event(36,{mode});}
void __attribute__((thiscall))camAngle(void*,short angle,unsigned short ticks){event(37,{angle,ticks});at<short>(camera,0x236)=angle;}
void __attribute__((thiscall))camX(void*,short value,unsigned short ticks){event(38,{value,ticks});}
void __attribute__((thiscall))camY(void*,short value,unsigned short ticks){event(39,{value,ticks});}
void __attribute__((thiscall))camZ(void*,short value,unsigned short ticks){event(40,{value,ticks});}
void __attribute__((thiscall))camHorizontal(void*,unsigned short value,unsigned short ticks){event(41,{value,ticks});}
void __attribute__((thiscall))camVertical(void*,short value,unsigned short ticks){event(42,{value,ticks});}
'''
    main = 'int main(int argc,char**argv)' + original_main
    main = main.replace('patch(0x445330,(void*)&follow);', '')
    main = main.replace('patch(0x446130,(void*)&hide);', '')
    main = main.replace('patch(0x4457B0,(void*)&takeHit);', '')
    main = main.replace('patch(0x445950,(void*)&die);', '')
    patches = r'''
 for(unsigned a:{0x445337u,0x4457B7u,0x444590u}){omit(a,6);mem<unsigned short>(a)=0xC031;}
 for(unsigned a:{0x44533Eu,0x44577Au,0x4457BEu,0x4457F1u,0x445875u,0x445896u,0x445944u,0x44459Eu,0x4445FDu,0x44463Fu,0x4446C8u})omit(a,7);
 patch(0x4DFE70,(void*)&readLinks);patch(0x4602F0,(void*)&findEnvironment);patch(0x4018D0,(void*)&move);patch(0x402E10,(void*)&script);
 patch(0x4C30A0,(void*)&suppress);patch(0x4167F0,(void*)&camMode);patch(0x4178E0,(void*)&camAngle);patch(0x417AE0,(void*)&camX);
 patch(0x417B60,(void*)&camY);patch(0x417BE0,(void*)&camZ);patch(0x4179F0,(void*)&camHorizontal);patch(0x417A70,(void*)&camVertical);
'''
    main = main.replace('int method,state,sub,wait', patches + '\n int method,state,sub,wait')
    main = main.replace('>>synth>>clearance){', '>>synth>>clearance>>role>>reaction>>obstruction>>nextNode>>hasNext>>pathCount>>environmentExists>>environmentNode>>environmentRecovery>>held>>hasCamera>>initialAngle>>completion>>scorpionDistance>>successor>>health>>hitFlags>>attackKind>>damage){')
    init = r'''
  std::memset(environment,0,sizeof environment);std::memset(camera,0,sizeof camera);suppressed=0;mem<int>(0x60CFA4)=0;
  at<int>(actor,0x2F8)=role;at<int>(actor,0x310)=55;at<int>(actor,0x318)=reaction;at<int>(actor,0x330)=obstruction;
  at<void*>(actor,0x364)=environment;at<unsigned>(actor,0x368)=999;at<int>(actor,0x36C)=nextNode;at<int>(actor,0x370)=hasNext;
  at<int>(actor,0x1F0)=pathCount;at<unsigned short>(actor,0xDC)=100;at<short>(environment,0x38)=401;
  for(int i=0;i<6;i++){at<int>(actor,0x1A8+12*i)=10000*(i+1);at<int>(actor,0x1AC+12*i)=-20000*(i+1);at<int>(actor,0x1B0+12*i)=30000*(i+1);}
  at<unsigned>(actor,0x288)=completion;at<short>(actor,0xE2)=health;at<int>(scorp,8)=100001+scorpionDistance*4096;at<int>(scorp,12)=-200002;at<int>(scorp,16)=300003;
  at<void*>(player,0xE48)=held?environment:nullptr;at<short>(player,0xE2)=synth?0:100;
  mem<void*>(0x56F3B8)=hasCamera?camera:nullptr;at<short>(camera,0x236)=initialAngle;
  primary.clear();secondary.clear();int count;std::cin>>count;for(int i=0;i<count;i++){Link l;std::cin>>l.node>>l.type>>l.role>>l.parameter;primary.push_back(l);}
  std::cin>>count;for(int i=0;i<count;i++){Link l;std::cin>>l.node>>l.type>>l.role>>l.parameter;secondary.push_back(l);}
'''
    main = main.replace('if(method==3){for(int i=2;i>=0;i--)', 'if(false){for(int i=2;i>=0;i--)')
    main = main.replace('auto fn=method==0?0x446340:method==1?0x444CF0:method==2?0x445B80:0x446470;', init + '\n unsigned functions[]={0x445330,0x444840,0x444B70,0x445A90,0x4457B0,0x446130,0x445950,0x446470,0x4446E0,0x4446E0};auto fn=functions[method];')
    main = main.replace('reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(fn)(actor);',
                        'alignas(4)unsigned char hit[56]={};at<unsigned char>(hit,0)=hitFlags;at<int>(hit,4)=attackKind;at<unsigned short>(hit,8)=damage;'
                        'int result=method==3?reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(fn)(actor,successor):method>=8?reinterpret_cast<int(__attribute__((thiscall))*)(void*,void*)>(fn)(actor,hit):reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(fn)(actor);'
                        'if(method==9)reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x446470)(actor);')
    # Only methods whose original signature returns an int expose a meaningful return value.
    main = main.replace('std::cout<<"{\\\"state\\\":";', 'std::cout<<"{\\\"result\\\":"<<((method==1||method==2||method==3||method>=8)?result:0)<<",\\\"state\\\":";')
    return header + extra + main


EXTRA_FIELDS = ['role', 'reaction', 'obstruction', 'nextNode', 'hasNext', 'pathCount', 'environmentExists',
                'environmentNode', 'environmentRecovery', 'held', 'hasCamera', 'cameraAngle', 'completion', 'scorpionDistance', 'successor',
                'health', 'hitFlags', 'attackKind', 'damage']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x445330)
    original = json.loads((root / 'modern/OpenSpidey.Tests/Fixtures/jonah-brain.json').read_text())
    cases = []
    for method, state, substates in [(0, 1, [0, 1, 2, 5, 10, 11]), (1, 1, [0, 1, 2]), (2, 1, [0]),
                                    (3, 1, [0]), (4, 6, [0, 1, 2]), (5, 5, [0, 1, 2, 3, 5, 6]), (6, 8, [0, 1, 2, 3]),
                                    (7, None, [0, 1, 2, 5, 6, 7, 10, 11]), (8, None, [0]), (9, None, [0])]:
        for _ in range(280):
            case = {field: rng.choice(original)[field] for field in FIELDS}
            case.update(method=method, state=state if state is not None else rng.randrange(9), substate=rng.choice(substates), hasProcedure=0,
                        signals=rng.choice([0, 1, 2, 4, 8, 0x20, 0x40, 0x80, 0x100, 0x200, 0x400, 0x2000, 0x220, 0xFFFFFFFF]),
                        role=rng.choice(range(8)), reaction=rng.choice([0, 999]), obstruction=rng.choice([0, 79, 80, 81]),
                        nextNode=91, hasNext=rng.choice([0, 1, 2]), pathCount=rng.choice([0, 1, 5]), environmentExists=rng.randrange(2),
                        environmentNode=rng.choice([10, 11, 12, 13]), environmentRecovery=rng.randrange(2), held=rng.randrange(2),
                        hasCamera=rng.randrange(2), cameraAngle=rng.choice([-32768, -1000, 0, 1000, 32767]),
                        wait=rng.choice([-1, 0, 1, 179, 180]), completion=rng.choice([0, 1, 4, 0xABCDEF09]),
                        scorpionDistance=rng.choice([0, 499, 500, 501]), successor=rng.randrange(2), health=rng.choice([-32768, -1, 0, 1, 120, 32767]),
                        hitFlags=rng.choice([0, 2, 4, 6, 8, 255]), attackKind=rng.choice([0, 6, 23]), damage=rng.choice([0, 1, 120, 65535]))
            case['links'] = [[10 + i, rng.choice([1, 2, 1000, 1002, 6]), rng.choice(range(7)), 0] for i in range(rng.randrange(5))]
            case['secondaryLinks'] = [[10 + i, rng.choice([1, 2, 1000, 1002, 6]), rng.choice(range(7)), 0] for i in range(rng.randrange(5))]
            cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-jonah-locomotion-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(harness())
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = []
        for c in cases:
            row = [c[field] for field in FIELDS + EXTRA_FIELDS]
            for links in [c['links'], c['secondaryLinks']]:
                row += [len(links)] + [value for link in links for value in link]
            rows.append(' '.join(map(str, row)))
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, output, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/jonah-locomotion.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC Jonah movement, arrival, waypoint, hiding, recovery and death cases.')


if __name__ == '__main__':
    main()
