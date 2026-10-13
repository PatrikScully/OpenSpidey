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

# Exercise final-area bookkeeping using a loaded bank scene. This verifies
# the application branch, not completion of the final mission or its boss.
set *(int*)0x54d38c = 0
set *(int*)0x68293c = 0
set {char[9]} 0x68285c = "l8a7_t"
set $completionIndex = Front_GetLevelIndex("l8a6_t")
python
import gdb
import os
index = int(gdb.parse_and_eval('$completionIndex'))
assert 0 <= index < 34, index
initial_count = int(os.environ.get('SPIDEY_COMPLETION_COUNT', '254'))
assert initial_count in (254, 255)
gdb.execute('set ((SSaveGame*)0x682858)->mLevelCompletionCounts[%d] = %d' % (index, initial_count))
end

break spidey::application::LevelSession::advanceFrame
commands
silent
set gLevelStatus = 3
continue
end
break '(anonymous namespace)::OriginalGameFlowActions::selectLevel'
continue
python
assert int(gdb.parse_and_eval('requests.finalLevelCompleted')) == 1
assert int(gdb.parse_and_eval('requests.statusRequested')) == 0
assert int(gdb.parse_and_eval('((SSaveGame*)0x682858)->mLevelCompletionCounts[%d]' % index)) == 255
print('PASS final-area completion bookkeeping, saturation and menu notification')
end
quit
