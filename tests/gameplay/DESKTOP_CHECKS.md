# Desktop regression checks

Use the packaged app with an original PC installation or imported disc ISO.
Keep test saves separate from personal saves. Record the app commit and binary
hash, desktop resolution and scaling, actual OpenGL renderer, SDL audio driver,
video settings and game difficulty. Keep the game in the foreground for timing
checks. Loading a map alone does not verify its mission or enemies.

For diagnostic runs, enable `SPIDEY_TRACE_MOVIES=1`,
`SPIDEY_TRACE_PLAYER=1` and `SPIDEY_TRACE_CULL=1`. Real keyboard input is required
for the event-pumping regression; `SPIDEY_KEYS` alone cannot prove it fixed.

## Setup on a large desktop

1. Open Settings on a 4K desktop with 200% font scaling. Visit Game files,
   Video, Sound and Controls. Check labels, selectors, buttons and focus rings
   for clipping. On a smaller desktop, scroll to every setting and the Start
   button using both pointer and keyboard.
2. Select borderless fullscreen. The resolution selector must be disabled;
   Start must use the current desktop mode and pixel density. Return to windowed
   mode and check that the saved window size is available again.
3. Import an ISO or select an installation. Start without developer tools.
   Confirm the renderer and audio device reported in the log. The Linux app
   uses compatible native graphics libraries when available and a coherent
   bundled fallback otherwise; do not mix an old GLX library with newer drivers.

## Movies, comic cover and pause input

1. Enable movies, VSync, 4x MSAA and nonzero audio. Start a new Normal game.
   Let each opening movie advance before skipping it with Escape. Check both
   changing video and audible movie sound. Repeat with Escape held for 100,
   300 and 500 ms; release it between movies.
2. Reach the first comic cover. Press Enter while it is visible. The level movie
   should start promptly, without waiting for the original five-second deadline.
   In a separate run, leave the cover alone and check that its original timeout
   still works. Test window close as well: events must be processed, although
   game exit may await the original wait deadline.
3. Hold Escape to skip the level movie. Gameplay must not receive a fresh pause
   press from that same held key. Verify movie decoding and its audio stop.
4. During an eligible Black Cat scene, press Escape. Check the player trace for
   scripted-input cleanup (`ctl=0/0/0/`). A following script may start on the
   next frame; sampling the scene flag after key release can miss this short
   transition. Wait for any mandatory continuation to finish before testing
   movement.
5. Release Escape, press it again during ordinary gameplay, then choose Continue.
   The fresh press must open the pause menu and Continue must restore play.
   Quit with F12 and check normal exit and decoder cleanup.

The original movie function at `0x470750` calls `Pad_Update` before accepting
skip input, then stops playback and clears pad triggers. Clearing triggers
preserves the held button state. The native fast Escape path must consume the
same press before returning to gameplay.

## Kid Mode and overlay placement

1. Open New Game and highlight Kid Mode. Capture the hand and web effect at
   the same logical coordinates as the original game. Repeat at 640x480 and a
   scaled window size. The web must retain its position beside the hand.
2. Check the normal hanging pose and the animated menu scale as well. The
   original `Shell_DrawKiddy` at `0x497690` positions effect frame 4 at `x-34`;
   the erroneous `x-46` offset moved it 12 logical pixels too far left.
3. Check all four screen edges during movies, widescreen scenes and flat menu
   overlays with MSAA off and at 4x. There must be no uncovered edge pixels.

## Foreground responsiveness and gameplay

1. After the scene, keep the game visibly active for at least 15 seconds. Record
   presentation intervals and time inside buffer swaps. Separate expected
   static screens, loading and background compositor throttling from foreground
   stalls. Verify foreground ownership throughout automated input tests.
2. Move with W and D, turn the camera in both directions and check upright
   geometry. Confirm player position and camera angle changes in the trace.
3. Punch three times and fire two web attacks. Check the selected animation,
   effect allocation and visible output. Enemy damage needs a target and health
   evidence; an attack animation alone is not a successful combat test.
4. For automated mouse tests, use a private X11 display and verify the pointer
   and keyboard belong to the game before injecting buttons. XTest under a
   scaled desktop compositor can change focus; those background frames cannot
   be counted as foreground performance measurements.
5. Quit, reopen and check saved settings. Extend coverage with the per-level,
   enemy, web, save and audio profiles in [scenarios.json](scenarios.json).

A separate timer fixture checks `Pause` with zero, negative, wrapped and positive
waits against an independently advancing timer. Compare elapsed ticks, thread
CPU use and event-processing latency. This measures timer-wait efficiency;
it does not measure the whole renderer or establish campaign completion.
