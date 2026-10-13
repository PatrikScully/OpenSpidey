# Microsoft Developer Studio Project File - Name="spider" - Package Owner=<4>
# Microsoft Developer Studio Generated Build File, Format Version 6.00
# ** DO NOT EDIT **

# TARGTYPE "Win32 (x86) Dynamic-Link Library" 0x0102

CFG=spider - Win32 Debug
!MESSAGE This is not a valid makefile. To build this project using NMAKE,
!MESSAGE use the Export Makefile command and run
!MESSAGE 
!MESSAGE NMAKE /f "spider.mak".
!MESSAGE 
!MESSAGE You can specify a configuration when running NMAKE
!MESSAGE by defining the macro CFG on the command line. For example:
!MESSAGE 
!MESSAGE NMAKE /f "spider.mak" CFG="spider - Win32 Debug"
!MESSAGE 
!MESSAGE Possible choices for configuration are:
!MESSAGE 
!MESSAGE "spider - Win32 Release" (based on "Win32 (x86) Dynamic-Link Library")
!MESSAGE "spider - Win32 Debug" (based on "Win32 (x86) Dynamic-Link Library")
!MESSAGE 

# Begin Project
# PROP AllowPerConfigDependencies 0
# PROP Scc_ProjName ""
# PROP Scc_LocalPath ""
CPP=cl.exe
MTL=midl.exe
RSC=rc.exe

!IF  "$(CFG)" == "spider - Win32 Release"

# PROP BASE Use_MFC 0
# PROP BASE Use_Debug_Libraries 0
# PROP BASE Output_Dir "Release"
# PROP BASE Intermediate_Dir "Release"
# PROP BASE Target_Dir ""
# PROP Use_MFC 0
# PROP Use_Debug_Libraries 0
# PROP Output_Dir "Release"
# PROP Intermediate_Dir "Release"
# PROP Ignore_Export_Lib 0
# PROP Target_Dir ""
# ADD BASE CPP /nologo /W3 /GX /O2 /D "WIN32" /D "NDEBUG" /D "_WINDOWS" /D "_MBCS" /YX /FD /c
# ADD CPP /nologo /MT /W3 /GX /O2 /Op /Ob2 /I "directx\include" /I "src" /I "." /D "WIN32" /D "NDEBUG" /D "_WINDOWS" /D "_MBCS" /YX /FD /c
# ADD BASE MTL /nologo /D "NDEBUG" /mktyplib203 /win32
# ADD MTL /nologo /D "NDEBUG" /mktyplib203 /win32
# ADD BASE RSC /l 0x409 /d "NDEBUG"
# ADD RSC /l 0x409 /d "NDEBUG"
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
LINK32=link.exe
# ADD BASE LINK32 kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib /nologo /dll /machine:I386
# ADD LINK32 kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib zlib.lib winmm.lib dinput8.lib dxguid.lib dsound.lib ddraw.lib /nologo /dll /machine:I386 /libpath:"directx\lib" /libpath:"lib"

!ELSEIF  "$(CFG)" == "spider - Win32 Debug"

# PROP BASE Use_MFC 0
# PROP BASE Use_Debug_Libraries 1
# PROP BASE Output_Dir "Debug"
# PROP BASE Intermediate_Dir "Debug"
# PROP BASE Target_Dir ""
# PROP Use_MFC 0
# PROP Use_Debug_Libraries 1
# PROP Output_Dir "Debug"
# PROP Intermediate_Dir "Debug"
# PROP Ignore_Export_Lib 0
# PROP Target_Dir ""
# ADD BASE CPP /nologo /W3 /Gm /GX /ZI /Od /D "WIN32" /D "_DEBUG" /D "_WINDOWS" /D "_MBCS" /YX /FD /GZ /c
# ADD CPP /nologo /MT /W3 /GX /O2 /D "WIN32" /D "_DEBUG" /D "_WINDOWS" /D "_MBCS" /YX /FD /c
# ADD BASE MTL /nologo /D "_DEBUG" /mktyplib203 /win32
# ADD MTL /nologo /D "_DEBUG" /mktyplib203 /win32
# ADD BASE RSC /l 0x409 /d "_DEBUG"
# ADD RSC /l 0x409 /d "_DEBUG"
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
LINK32=link.exe
# ADD BASE LINK32 kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib /nologo /dll /debug /machine:I386 /pdbtype:sept
# ADD LINK32 kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib /nologo /dll /incremental:no /debug /machine:I386 /pdbtype:sept

!ENDIF 

# Begin Target

# Name "spider - Win32 Release"
# Name "spider - Win32 Debug"
# Begin Group "Source Files"

# PROP Default_Filter "cpp;c;cxx;rc;def;r;odl;idl;hpj;bat"
# Begin Source File

SOURCE=.\src\engine\world\ai.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\algebra.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\backgrnd.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\baddy.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\bit.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\bit2.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\bitmap256.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\blackcat.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\directx\bmr.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\bullet.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\actor_script_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\script_command_list.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\script_command_allocator.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\camera.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\carnage.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\chain.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\chopper.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\chunk.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\cinema.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\cop.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\crate.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\render_buffers.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\dcfileio.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\dcmemcard.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\dcmodel.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\dcshellutils.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\animation_decoder.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\docock.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\DXinit.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\directx\DXsound.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\effects.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\exp.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\flash.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\FontTools.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\friction.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\front.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\hostage.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\Image.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\init.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\jonah.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\level_one_bomb.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l2a1lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a5lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a6lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a7lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a1lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a2lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a3lsc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\lizard.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\lizman.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\localtxt.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\LThread.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3d.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\collision.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3dinit.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3dutils.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\m3dzone.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\main.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\manipob.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\game_heap.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\mess.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\message.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\mj.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\music.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\my_bink.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\my_debug.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\mysterio.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\ob.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pack.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\pal.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\panel.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\save_files.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pcdcFile.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pcdcMem.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\pcdcPad.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\PCGfx.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\PCInput.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\PCMovie.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\PCMusic.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\PCShell.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\PCTex.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\PCTimer.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\physics.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pkr.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\platform.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\post.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\powerup.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\PRE.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\ps2card.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\ps2funcs.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\ps2gamefmv.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\ps2lowsfx.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\scene_renderer.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\ps2pad.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\ps2redbook.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\pshell.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\quaternion.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\reloc.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\reloc_g.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\rfront.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\rhino.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\Sbinit.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\scorpion.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\screen.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\shatter.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\shell.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\simby.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\smoke.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\spclone.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_behavior_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_frame_preparation.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_status_updates.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_lookaround_input.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_frame_finalizer.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_camera_updates.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_presentation_updates.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_action_handlers.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_locomotion_actions.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_web_actions.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_interaction_actions.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_combat_actions.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_behavior.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\spidey.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\SpideyDX.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\spool.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\stubs.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\submarin.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\superock.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\SwapTriggerData.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\switch.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\thug.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\torch.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\training.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\trig.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\pending_trigger_queue.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\command_point_registry.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\trigger_runtime_bridge.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\turret.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\tweak.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\utils.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\validate.cpp
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\fixed_vector_bridge.cpp
# End Source File
# Begin Source File

SOURCE=.\src\core\fixed_vector.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\venom.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\vram.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\weapons.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\web.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\wire.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\zrhinog.cpp
# End Source File
# Begin Source File

SOURCE=.\font_draw.obj
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\render_buffer_pool.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\game_application.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\level_session.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\game_flow_actions.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\game_flow.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\player_frame_trace.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\gameplay_renderer.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\baddy_frame_trace.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\world_simulation.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\application\input_update_trace.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\actor_motion_integrator.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\actor_list_updater.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\scripted_input_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_animation_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_physics_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\crawl_physics_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\crawl_frame_trace.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\swing_collision_resolver.cpp
# End Source File
# Begin Source File

SOURCE=.\src\game\player\zip_web_controller.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\input_devices.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\sound_channels.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\polygon_pipeline.cpp
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\polygon_queue.cpp
# End Source File
# End Group
# Begin Group "Header Files"

# PROP Default_Filter "h;hpp;hxx;hm;inl"
# Begin Source File

SOURCE=.\src\engine\world\ai.h
# End Source File
# Begin Source File

SOURCE=.\src\core\algebra.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\backgrnd.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\baddy.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\bit.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\bit2.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\bitmap256.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\blackcat.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\directx\bmr.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\bullet.h
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\camera.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\carnage.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\chain.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\chopper.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\chunk.h
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\cinema.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\cop.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\crate.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\render_buffers.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\dcfileio.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\dcmemcard.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\dcmodel.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\dcshellutils.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\animation_decoder.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\docock.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\DXinit.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\directx\DXsound.h
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\effects.h
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\exp.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\export.h
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\flash.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\FontTools.h
# End Source File
# Begin Source File

SOURCE=.\src\core\friction.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\front.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\hostage.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\Image.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\init.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\jonah.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\level_one_bomb.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l2a1lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a5lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a6lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l5a7lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a1lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a2lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\levels\l6a3lsc.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\lizard.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\lizman.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\localtxt.h
# End Source File
# Begin Source File

SOURCE=.\src\core\LThread.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3d.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\collision.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3dinit.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\m3dutils.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\m3dzone.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\main.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\manipob.h
# End Source File
# Begin Source File

SOURCE=.\src\core\game_heap.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\mess.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\message.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\mj.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\music.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\my_assert.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\my_bink.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\my_patch.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\mysterio.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\ob.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pack.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\pal.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\panel.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\save_files.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pcdcFile.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pcdcMem.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\pcdcPad.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\PCGfx.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\PCInput.h
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\PCMovie.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\PCMusic.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\PCShell.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\PCTex.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\PCTimer.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\physics.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\pkr.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\platform.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\post.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\powerup.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\PRE.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\persistence\ps2card.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\ps2funcs.h
# End Source File
# Begin Source File

SOURCE=.\src\game\cinematics\ps2gamefmv.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\ps2lowsfx.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\scene_renderer.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\input\ps2pad.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\audio\ps2redbook.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\pshell.h
# End Source File
# Begin Source File

SOURCE=.\src\core\quaternion.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\reloc.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\reloc_g.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\rfront.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\rhino.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\runtime_version.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\Sbinit.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\scorpion.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\screen.h
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\shatter.h
# End Source File
# Begin Source File

SOURCE=.\src\game\ui\shell.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\simby.h
# End Source File
# Begin Source File

SOURCE=.\src\game\effects\smoke.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\spclone.h
# End Source File
# Begin Source File

SOURCE=.\src\game\player\player_behavior.h
# End Source File
# Begin Source File

SOURCE=.\src\game\player\spidey.h
# End Source File
# Begin Source File

SOURCE=.\src\game\application\SpideyDX.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\assets\spool.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\stateflags.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\submarin.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\superock.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\texture.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\thug.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\torch.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\world\trig.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\turret.h
# End Source File
# Begin Source File

SOURCE=.\src\core\utils.h
# End Source File
# Begin Source File

SOURCE=.\src\compatibility\validate.h
# End Source File
# Begin Source File

SOURCE=.\src\core\fixed_vector.h
# End Source File
# Begin Source File

SOURCE=.\src\game\actors\venom.h
# End Source File
# Begin Source File

SOURCE=.\src\engine\rendering\vram.h
# End Source File
# Begin Source File

SOURCE=.\src\game\player\web.h
# End Source File
# Begin Source File

SOURCE=.\src\game\objects\wire.h
# End Source File
# End Group
# Begin Group "Resource Files"

# PROP Default_Filter "ico;cur;bmp;dlg;rc2;rct;bin;rgs;gif;jpg;jpeg;jpe"
# End Group
# End Target
# End Project
