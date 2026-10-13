set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SPIDEY_SKIP fades
set env SPIDEY_QUIT_MS 20000
set env SPIDEY_KEYS 1000:enter,1000:enter,1000:enter
break SpideyMain
commands
silent
set *(int*)0x568fb0 = 2
continue
end
break PlayAway
run
disable breakpoints

# Duplicate an existing pickup through the real trigger object factory so
# suspension walks multiple nodes and each destructor owns its original list.
set $sourceNode = ((CPowerUp*)PowerUpList)->mNodeIndex
set $extra = Trig_CreateObject($sourceNode)

python
import gdb
actors = []
actor = gdb.parse_and_eval('PowerUpList')
while int(actor):
    actors.append(int(actor))
    actor = actor.dereference()['mNextItem'].cast(gdb.lookup_type('CBody').pointer())
assert len(actors) >= 2, actors
for index, address in enumerate(actors):
    gdb.execute('set ((CBody*)%d)->mPos = ((CBody*)*(int*)0x6a9038)->mPos' % address)
    gdb.execute('set ((CBody*)%d)->mPos.vx += %d' % (address, (100 + index) * 4096))
    gdb.execute('set ((CBody*)%d)->mCBodyFlags = (((CBody*)%d)->mCBodyFlags & ~0xc0) | 2' % (address, address))
end
set $first = PowerUpList
set $originalThreshold = SuspendedDistance
set SuspendedDistance = 0
call Ob_AI(&PowerUpList, 0)
python
assert int(gdb.parse_and_eval('PowerUpList')) == 0
for address in actors:
    assert int(gdb.parse_and_eval('((CBody*)%d)->mCBodyFlags' % address)) & 1
    assert int(gdb.parse_and_eval('((CBody*)%d)->mppOriginalList' % address)) == int(gdb.parse_and_eval('&PowerUpList'))
end

# Resume uses an inclusive threshold. Only the original first actor is at 100.
set SuspendedDistance = 99
call Ob_MaybeUnSuspendOrCull()
python
assert int(gdb.parse_and_eval('PowerUpList')) == 0
end
set SuspendedDistance = 100
call Ob_MaybeUnSuspendOrCull()
python
assert int(gdb.parse_and_eval('PowerUpList')) == actors[0]
assert int(gdb.parse_and_eval('$first->mCBodyFlags')) & 1 == 0
assert int(gdb.parse_and_eval('$first->mNextItem')) == 0
end

# Deletion takes precedence over radial suspension and skips timing/AI.
set $elapsedBefore = $first->mAccumulatedTicks
set SuspendedDistance = 0
set $first->mCBodyFlags = $first->mCBodyFlags | 0x40
call Ob_AI(&PowerUpList, 0)
python
assert int(gdb.parse_and_eval('PowerUpList')) == actors[0]
assert int(gdb.parse_and_eval('$first->mCBodyFlags')) & 0x80
assert int(gdb.parse_and_eval('$first->mAccumulatedTicks')) == int(gdb.parse_and_eval('$elapsedBefore'))
end
call Ob_AI(&PowerUpList, 0)
python
assert int(gdb.parse_and_eval('PowerUpList')) == 0
remaining = []
actor = gdb.parse_and_eval('SuspendedList')
while int(actor):
    remaining.append(int(actor))
    actor = actor.dereference()['mNextItem'].cast(gdb.lookup_type('CBody').pointer())
assert actors[0] not in remaining
assert all(address in remaining for address in actors[1:])
print('PASS actor suspension, inclusive resumption, home-list restoration and deletion grace')
end
set SuspendedDistance = $originalThreshold
quit
