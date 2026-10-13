"""Compile original ShootHostage and real shot helpers; actor/camera/message effects recorded."""
import ast
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_chase_vectors import recovered_harness


def ranged_harness(root):
    tree = ast.parse((root/'tools/reference_thug_ranged_vectors.py').read_text())
    main = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'main')
    statements = []
    for statement in main.body:
        if isinstance(statement, ast.AugAssign) and isinstance(statement.value, ast.Constant) and '\nint main(){' in str(statement.value.value):
            break
        statements.append(statement)
    context = dict(__file__=str(root/'tools/reference_thug_ranged_vectors.py'), Path=Path,
                   recovered_harness=recovered_harness, controller_method=controller_method)
    exec(compile(ast.Module(body=statements,type_ignores=[]),'<original-ranged-harness>','exec'), context)
    return context['code']


def main():
    root = Path(__file__).resolve().parent.parent
    code = ranged_harness(root)
    code = code.replace('struct CBaddy{', 'struct Handle{CBaddy*pWhatever=nullptr;};\nint G_DIFFICULTY_LEVEL=0,recoverMask=3,recoverIndex=0;\nCBaddy*Mem_RecoverPointer(Handle*h){events.insert(events.end(),{30,recoverIndex});return (recoverMask&(1<<recoverIndex++))?h->pWhatever:nullptr;}\nstruct CBaddy{')
    code = code.replace('bool IsDead(){', 'Handle mHandle;void ShootHostage();\n bool IsDead(){')
    code += '''\nvoid Camera_SelectOptimumViewingNode(int count,CVector*p){events.insert(events.end(),{31,count,p[0].vx,p[0].vy,p[0].vz,p[1].vx,p[1].vy,p[1].vz});}\n'''
    code += controller_method((root/'src/game/actors/thug.cpp').read_text(),'void CThug::ShootHostage(').replace('CThug::','CBaddy::')+'\n'
    code += r'''
int main(){int sub;while(std::cin>>sub){events.clear();recoverIndex=randomIndex=0;CBaddy t,p,target;G_MECHLIST_PLAYER=&p;
 int flags,laser;std::cin>>t.mType>>t.field_230>>t.field_288>>t.field_2A8>>t.field_218>>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished
 >>t.mFlags>>t.field_338>>t.field_344>>t.field_3A8>>G_DIFFICULTY_LEVEL>>visible>>laserResult>>recoverMask>>target.field_2A8>>flags>>laser
 >>randomValues[0]>>randomValues[1]>>randomValues[2]>>randomValues[3];
 t.dumbAssPad=sub;t.field_31C.bothFlags=8;t.field_310=99;t.mElapsedTicks=2;t.mHandle.pWhatever=&target;
 t.mPos={100001,-200002,300003};target.mPos={4000000,-5000000,6000000};p.mPos={9000000,7000000,-11000000};t.field_380=400;
 t.mVel={11,17,13};t.mAcc={-7,-23,5};t.field_27C={11,17,13};t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};
 collisionAdjustment={11,-23,17};t.field_3A0=laser?new FakeLaser:nullptr;
 CAIProc old;old.mAIProcType=1794;old.field_10=flags;old.pBaddy=&t;t.mAIProcList=&old;
 t.ShootHostage();std::cout<<'['<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.field_230<<','<<t.field_288<<','<<t.field_2A8<<','<<t.field_218
 <<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.field_310<<','<<t.field_344<<','<<t.field_3A8<<','<<(t.field_3A0!=nullptr)
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz
 <<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz<<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz
 <<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz<<','<<old.field_10<<",[";
 int count=0;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(count++)std::cout<<',';std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    rng = random.Random(0x4D6C80)
    cases = []
    for i in range(650):
        sub = rng.randrange(5); kind = rng.choice([304,312])
        cases.append([sub,kind,rng.choice([-2147483648,-1,0,1,2,49,75,2147483647]),rng.choice([0,2,0xffffffff]),
                      rng.choice([0,1,4,0xffffffff]),rng.choice([0,0x1000,0x3000,0x7000,0xffffffff]),
                      rng.choice([1,4,7,8,9]),rng.choice([0,1,2,5,6,10,11,14,20]),rng.randrange(2),rng.randrange(2),
                      rng.choice([0,0x8000]),rng.randrange(2),rng.choice([0,17]),rng.choice([0,1234]),rng.choice([0,1,2,3]),
                      rng.randrange(2),rng.choice([-1,0,1,2]),rng.choice([0,1,3]),rng.choice([0,0x1000]),rng.choice([0,4]),
                      rng.randrange(2),*[rng.randrange(500) for _ in range(4)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-hostage-reference-') as folder:
        cpp=Path(folder)/'reference.cpp'; binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-hostage.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Generated {len(result)} original hostage state/shot cases.')


if __name__ == '__main__':main()
