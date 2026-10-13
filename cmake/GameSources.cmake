# Explicit system source lists. Add new translation units to their module.
set(SPIDEY_GAME_SOURCES)

set(SPIDEY_CORE_SOURCES
    src/core/LThread.cpp
    src/core/algebra.cpp
    src/compatibility/fixed_vector_bridge.cpp
    src/core/fixed_vector.cpp
    src/core/friction.cpp
    src/core/game_heap.cpp
    src/core/quaternion.cpp
    src/core/utils.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_CORE_SOURCES})

set(SPIDEY_COMPATIBILITY_SOURCES
    src/compatibility/trigger_runtime_bridge.cpp
    src/compatibility/my_bink.cpp
    src/compatibility/my_debug.cpp
    src/compatibility/non_win32.cpp
    src/compatibility/stubs.cpp
    src/compatibility/validate.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_COMPATIBILITY_SOURCES})

set(SPIDEY_ENGINE_RENDERING_SOURCES
    src/engine/rendering/polygon_queue.cpp
    src/engine/rendering/polygon_pipeline.cpp
    src/engine/rendering/render_buffer_pool.cpp
    src/engine/rendering/Image.cpp
    src/engine/rendering/PCGfx.cpp
    src/engine/rendering/PCTex.cpp
    src/engine/rendering/animation_decoder.cpp
    src/engine/rendering/backgrnd.cpp
    src/engine/rendering/bitmap256.cpp
    src/engine/rendering/dcmodel.cpp
    src/engine/rendering/m3d.cpp
    src/engine/rendering/m3dinit.cpp
    src/engine/rendering/m3dutils.cpp
    src/engine/rendering/pal.cpp
    src/engine/rendering/post.cpp
    src/engine/rendering/ps2funcs.cpp
    src/engine/rendering/render_buffers.cpp
    src/engine/rendering/scene_renderer.cpp
    src/engine/rendering/vram.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_RENDERING_SOURCES})

set(SPIDEY_ENGINE_ASSETS_SOURCES
    src/engine/assets/PRE.cpp
    src/engine/assets/chunk.cpp
    src/engine/assets/dcfileio.cpp
    src/engine/assets/pack.cpp
    src/engine/assets/pcdcFile.cpp
    src/engine/assets/pcdcMem.cpp
    src/engine/assets/pkr.cpp
    src/engine/assets/reloc.cpp
    src/engine/assets/reloc_g.cpp
    src/engine/assets/spool.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_ASSETS_SOURCES})

set(SPIDEY_ENGINE_AUDIO_SOURCES
    src/engine/audio/sound_channels.cpp
    src/engine/audio/PCMusic.cpp
    src/engine/audio/music.cpp
    src/engine/audio/ps2lowsfx.cpp
    src/engine/audio/ps2redbook.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_AUDIO_SOURCES})

set(SPIDEY_ENGINE_INPUT_SOURCES
    src/engine/input/input_devices.cpp
    src/engine/input/PCInput.cpp
    src/engine/input/pcdcPad.cpp
    src/engine/input/ps2pad.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_INPUT_SOURCES})

set(SPIDEY_ENGINE_WORLD_SOURCES
    src/engine/world/actor_motion_integrator.cpp
    src/engine/world/actor_list_updater.cpp
    src/engine/world/SwapTriggerData.cpp
    src/engine/world/ai.cpp
    src/engine/world/bit.cpp
    src/engine/world/bit2.cpp
    src/engine/world/collision.cpp
    src/engine/world/m3dzone.cpp
    src/engine/world/ob.cpp
    src/engine/world/physics.cpp
    src/engine/world/trig.cpp
    src/engine/world/pending_trigger_queue.cpp
    src/engine/world/command_point_registry.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_WORLD_SOURCES})

set(SPIDEY_GAME_APPLICATION_SOURCES
    src/game/application/gameplay_renderer.cpp
    src/game/application/baddy_frame_trace.cpp
    src/game/application/world_simulation.cpp
    src/game/application/player_frame_trace.cpp
    src/game/application/input_update_trace.cpp
    src/game/application/game_application.cpp
    src/game/application/game_flow.cpp
    src/game/application/game_flow_actions.cpp
    src/game/application/level_session.cpp
    src/game/application/DXinit.cpp
    src/game/application/PCTimer.cpp
    src/game/application/Sbinit.cpp
    src/game/application/SpideyDX.cpp
    src/game/application/init.cpp
    src/game/application/main.cpp
    src/game/application/tweak.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_APPLICATION_SOURCES})

set(SPIDEY_GAME_PLAYER_SOURCES
    src/game/player/scripted_input_controller.cpp
    src/game/player/player_animation_controller.cpp
    src/game/player/player_physics_controller.cpp
    src/game/player/crawl_physics_controller.cpp
    src/game/player/crawl_frame_trace.cpp
    src/game/player/swing_collision_resolver.cpp
    src/game/player/zip_web_controller.cpp
    src/game/player/player_behavior.cpp
    src/game/player/player_behavior_controller.cpp
    src/game/player/player_frame_preparation.cpp
    src/game/player/player_status_updates.cpp
    src/game/player/player_lookaround_input.cpp
    src/game/player/player_frame_finalizer.cpp
    src/game/player/player_camera_updates.cpp
    src/game/player/player_presentation_updates.cpp
    src/game/player/player_action_handlers.cpp
    src/game/player/player_locomotion_actions.cpp
    src/game/player/player_web_actions.cpp
    src/game/player/player_interaction_actions.cpp
    src/game/player/player_combat_actions.cpp
    src/game/player/spidey.cpp
    src/game/player/weapons.cpp
    src/game/player/web.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_PLAYER_SOURCES})

set(SPIDEY_GAME_ACTORS_SOURCES
    src/game/actors/baddy.cpp
    src/game/actors/blackcat.cpp
    src/game/actors/carnage.cpp
    src/game/actors/chopper.cpp
    src/game/actors/cop.cpp
    src/game/actors/docock.cpp
    src/game/actors/hostage.cpp
    src/game/actors/jonah.cpp
    src/game/actors/lizard.cpp
    src/game/actors/lizman.cpp
    src/game/actors/mj.cpp
    src/game/actors/mysterio.cpp
    src/game/actors/rhino.cpp
    src/game/actors/scorpion.cpp
    src/game/actors/simby.cpp
    src/game/actors/spclone.cpp
    src/game/actors/submarin.cpp
    src/game/actors/superock.cpp
    src/game/actors/thug.cpp
    src/game/actors/torch.cpp
    src/game/actors/venom.cpp
    src/game/actors/zrhinog.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_ACTORS_SOURCES})

set(SPIDEY_GAME_OBJECTS_SOURCES
    src/game/objects/bullet.cpp
    src/game/objects/chain.cpp
    src/game/objects/crate.cpp
    src/game/objects/level_one_bomb.cpp
    src/game/objects/manipob.cpp
    src/game/objects/platform.cpp
    src/game/objects/powerup.cpp
    src/game/objects/switch.cpp
    src/game/objects/turret.cpp
    src/game/objects/wire.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_OBJECTS_SOURCES})

set(SPIDEY_GAME_EFFECTS_SOURCES
    src/game/effects/effects.cpp
    src/game/effects/exp.cpp
    src/game/effects/flash.cpp
    src/game/effects/shatter.cpp
    src/game/effects/smoke.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_EFFECTS_SOURCES})

set(SPIDEY_GAME_LEVELS_SOURCES
    src/game/levels/l2a1lsc.cpp
    src/game/levels/l5a5lsc.cpp
    src/game/levels/l5a6lsc.cpp
    src/game/levels/l5a7lsc.cpp
    src/game/levels/l6a1lsc.cpp
    src/game/levels/l6a2lsc.cpp
    src/game/levels/l6a3lsc.cpp
    src/game/levels/training.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_LEVELS_SOURCES})

set(SPIDEY_GAME_UI_SOURCES
    src/game/ui/FontTools.cpp
    src/game/ui/PCShell.cpp
    src/game/ui/dcshellutils.cpp
    src/game/ui/front.cpp
    src/game/ui/localtxt.cpp
    src/game/ui/mess.cpp
    src/game/ui/message.cpp
    src/game/ui/panel.cpp
    src/game/ui/pshell.cpp
    src/game/ui/rfront.cpp
    src/game/ui/screen.cpp
    src/game/ui/shell.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_UI_SOURCES})

set(SPIDEY_GAME_CINEMATICS_SOURCES
    src/game/cinematics/PCMovie.cpp
    src/game/cinematics/actor_script_controller.cpp
    src/game/cinematics/script_command_list.cpp
    src/game/cinematics/script_command_allocator.cpp
    src/game/cinematics/camera.cpp
    src/game/cinematics/cinema.cpp
    src/game/cinematics/ps2gamefmv.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_GAME_CINEMATICS_SOURCES})

set(SPIDEY_ENGINE_PERSISTENCE_SOURCES
    src/engine/persistence/dcmemcard.cpp
    src/engine/persistence/ps2card.cpp
    src/engine/persistence/save_files.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_ENGINE_PERSISTENCE_SOURCES})

set(SPIDEY_COMPATIBILITY_DIRECTX_SOURCES
    src/compatibility/directx/DXsound.cpp
    src/compatibility/directx/bmr.cpp
)
list(APPEND SPIDEY_GAME_SOURCES ${SPIDEY_COMPATIBILITY_DIRECTX_SOURCES})
