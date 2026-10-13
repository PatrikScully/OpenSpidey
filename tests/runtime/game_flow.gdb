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

python
import gdb

level_sessions = 0
exit_reasons = []
shutdowns = 0

class LevelEntry(gdb.Breakpoint):
    def stop(self):
        global level_sessions
        level_sessions += 1
        # Forced level entry bypasses the shell's reset of these flags.
        gdb.execute('set *(int*)0x54d38c = 0')
        gdb.execute('set *(int*)0x68293c = 0')
        assert level_sessions <= 2, level_sessions
        return False

class EndLevel(gdb.Breakpoint):
    def stop(self):
        # First reload the same level; then quit this shell-free game.
        gdb.execute('set gLevelStatus = %d' % (1 if level_sessions == 1 else 7))
        return False

class HandleExit(gdb.Breakpoint):
    def stop(self):
        exit_reasons.append(int(gdb.parse_and_eval('reason')))
        return False

class Shutdown(gdb.Breakpoint):
    def stop(self):
        global shutdowns
        shutdowns += 1
        return False

LevelEntry('PlayAway')
EndLevel('spidey::application::LevelSession::advanceFrame')
HandleExit('spidey::application::GameFlow::handleLevelExit')
Shutdown('(anonymous namespace)::OriginalGameFlowActions::shutdown')
end

run
python
assert level_sessions == 2, level_sessions
assert exit_reasons == [1, 7], exit_reasons
assert shutdowns == 1, shutdowns
assert gdb.selected_inferior().pid == 0
print('PASS actual level reload, quit routing and application shutdown')
end
