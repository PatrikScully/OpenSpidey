# Godot migration

The active target is Godot 4.7.2 .NET with C# and .NET 10.0.401, running in a
64-bit process on Linux, Windows and macOS. The original game's DirectX runtime,
MSVC6 DLL injection, fixed-address globals and 32-bit object ABI are retired
targets. Recovered C++ remains reference material during the port.

## Boundaries

`modern/OpenSpidey.Assets` reads proprietary files into managed records. It has
no Godot dependency, native pointers, executable loading or platform APIs.
Archive reads validate ranges, output size and CRC. The importer never writes
to the original game folder.

`PsxAssetLibrary` groups a scene, its decoded textures and either animation
format. `PsxLibraryRepository` caches these records for a session using canonical
archive names, while the session retains ownership of the archive handle.
Model checksums survive import and resolve the first matching index, as in the
recovered model lookup. Missing model names fail with the checksum and library
path instead of falling back to an unrelated mesh.

`modern/OpenSpidey.Game` owns scenes, rendering, input, animation, audio and
physics adapters. Godot resources replace the original render packets and
fixed memory pools. Current rendering uses Godot's cross-platform OpenGL
compatibility backend, including Windows; no DirectX backend is selected.
Vulkan can be evaluated once the visual baseline is established.

`modern/OpenSpidey.Tests` exercises parsers and gameplay contracts without
launching the engine. Asset validation additionally reads a user's own archives.
Original assets must not be committed or included in release packages.

`modern/OpenSpidey.MediaImporter` converts owner-supplied movie and cover assets
into a private cache using FFmpeg. It reads the checked archive API, never writes
to the original folder, and commits completed conversions with per-file recipe,
source CRC and output SHA-256 stamps. Temporary conversion files are removed on
failure; damaged or outdated cache entries are rebuilt.

Gameplay rules and the mission interpreter live in the engine-independent
`OpenSpidey.Core` module. Godot nodes implement narrow world interfaces. Story
commands retain their original numeric IDs at the import boundary and receive
named command implementations. Unknown commands must report their source node
and offset rather than silently skipping story progression.

## Current checkpoint

The latest complete asset-inclusive managed run passes 1,701,432 assertions. See
[the 2026-10-14 checkpoint](CHECKPOINT_2026-10-14.md) for the saved build,
verification and remaining work, and [the progress record](RESUME_2026-10-14.md)
for integration details. The developing
play scene includes original player health/web meters and cartridge lettering.
HUD groups share the original 512×240 pixel aspect with height-based sizing,
preserving connected pieces at 4:3 and 16:9. A separate centered 4:3 menu frame is
available; the full frontend still needs its application flow. Gameplay keeps
the full viewport and original vertical framing.

Native distance-attack preparation now passes 29 checks over actual carried OBJ
actors, items503/504/505, source sound26, swing/web/dome ownership, four smoke
trails and camera cleanup. The existing 48 scene-destruction and 27 start-orientation
checks also pass. The story shutdown check retires playback, waits for the audio
thread to release its PCM references, then verifies all 18 tracked resources are
deleted before engine exit. These checks do not establish a complete first-level
playthrough or campaign completion.

The story scene now binds original196, shared process water/post state, genuine
distance-attack preparation and explicit player-effect phases. Native checks pass
20 water tint/sky cases, six flaming-fist cases and 31 live Story effect cases:
water reset before damage timers, actual504/505 models, hand-before-foot trails,
flaming ember scheduling, natural expiry and true teardown hook removal. The
complete L1A1 startup run exits cleanly after verifying23 tracked audio resources.

Original linked-body commands, signed timed motion, canonical platform physics
and environment preparation now pass their managed PC comparisons. The complete
native component regression exits cleanly. Environment destruction covers all51
authored Chnk records:49 static pieces,159 flying pieces,eight checksum contacts
and50 real ruin collision rays, including the unchanged L3A1 node15442B5 producer.
The live Story registry now owns that lazy source-generation-aware chunk service.
Claimed switch items now share prepared source models with the chunk service.
The native L3A1 proof covers the unchanged building62 and hidden ruins108/109,
authored15442B5, reveal through the same meshes, exact flying pose and collision,
constructor failure cleanup, reparenting and deterministic private resource
destruction. Eleven native camera checks also verify shared script/front mode,
saved Push/Pop pose and independently retained fixed/zoom timers.

L1A2 passes55 actual Story startup/skip checks: its15 no-player constructors, original
death restart, Black Cat ordinal17, checkpoint124, three original backdrops and
live player/NPC/lighting updates. Native scene destruction releases14 tracked
audio resources with a clean engine exit. The checkpoint124 physical skip runs
genuine Prior cleanup and original nested constructor/death-pulse events through
the real source end-frame boundary.

The authored end224 transition probe passes305/110/162, real movie12,181, the
genuine completion counter and actual SceneTree navigation into L1A2 startup and
physical skip. It exits cleanly after verifying13 retained audio resources.
Queued185/186 commands now retain the original missing-camera guard after source
teardown. Camera command mode and native lookaround share one canonical mode owner.
The probe queues the actual end point after startup; it does not prove that an
ordinary gameplay traversal reaches it.

`tools/modern.py story-smoke --assets /path/to/game --rendered` verifies L1A1
startup. Add `--scene data/L1A2_G.psx` for the second mission probe. Normal `play`
and these Story checks prepare the selected mission's direct cinema commands
into the private media cache, including intro and transition movies.
`story-transition-smoke --assets /path/to/game --rendered` runs the unchanged
first-mission end list and verifies actual navigation and the second mission's
checkpoint skip without writing a progress save.

HUD sprite rectangles match 520 actual-PC cases. The player drawing block adds
352 compiled-source frames for health/armor/web masks, cartridge flare and
submission order. Six original bitmap fonts pass 4,320 compiled-source layout
comparisons plus per-glyph original pixel hashes. Native captures verify the
real Sp/Webcart/font uploads, dimensions, lettering, transparency and teardown.
The first capture exposed missing `Pal_ProcessPalette` preprocessing. Its 522
actual-PC cases now prove magenta-key priority, opaque black, transparent zero
and RGB preservation; decoded indexed textures use that policy. Independent
effect pixel hashes are regenerated after the same PC palette processing.
No source image pixels are committed.

The mission timer now uses the application's shared countdown and persistent
scan-bar state. Its complete display matches 336 original-PC calls; native
4:3 and 16:9 captures verify the original Misc19 border, sp_fnt01 glyphs,
seconds sound, font presets and per-render scan progression. It draws before
the ordinary HUD/script-control gate, as in the original panel.

Shared body timing matches 880 executions of the original CBody::EveryFrame,
including the first-update flag, signed clock wrap, six-tick clamp,
accumulation and pre-update animation caches. The bomb uses this shared timing
owner. Thug, hostage and Black Cat adapters now use the same timing owner;
native checks pass their real animation, AI, physics and cleanup. Animation preserves the
original distinction between accumulated ticks and its zero-to-two elapsed correction.
Environmental handles and switch resources now survive native reparenting;
actual destruction or explicit source removal invalidates their owners.

The combat foundation includes578 melee sequences/8,326 snapshots over original
fight records and explicitly labelled crafted branches,373 kick-selection
cases,490 target selectors,257 eight-update threat lists and448 torso cases.
The play scene now composes the native melee adapter, original fight data,
world contacts, source sound words, shared encounter history and particle phases.
Native checks verify actual punch/kick input, standing recovery and effect cleanup.
Full enemy and mission activation still requires the remaining session services.
The live combat composition now uses one typed source particle scheduler for
combat rings, rocks, lines, shatter and glow effects. Its distinct movement and
cleanup orders match 414 executions of the original PC list functions, including
head insertion, cross-list births and removal during callbacks. Pixel bits move
before flat bits: newly emitted fire sparks wait until the next source update.
Protected companion ownership remains with the concrete parent destructor.

The web composition joins genuine posed hands, inventory, actor contacts,
projectiles, paired strands, surface blobs and burning embers to those same lists.
Original `firebits.psx` supplies the flaming projectile; it is a separate resource
from `bits.psx`. Native checks pass fire projectile/trail/spark and ember lifetimes,
texture uploads, cleanup and 4:3/16:9 geometry. Windup/firing and directional input
have source controllers, now joined to the playable lookaround and swing owners. Verified
action IDs are windup `0x4000`, firing `0x8000`, attach `0x10000` and tug `0x20000`.
The shared release latch, aim flag and loop-sound handle use their actual player
owners instead of separate adapter copies.
Native ordinary/fire dome checks pass actual input, the 3072 supply charge,
recoverable ownership, original model/piece geometry, actor suspension and death,
same-frame ember/fragment/shock phases, ring deformation and widescreen geometry.
The source dome release action restores the original player radius on idle.
Native integration also passes ordinary/fire FireWeb from the real posed player,
shared consumption, original roof contacts and complete mixed-list retirement.
The genuine L1A3 thug proof passes source `0x20000` release, the real height gate
and native launch/rejection, paired chain motion and age-31 fragment cleanup.
The actor registry now mixes script and effect bodies through one source list per
family and one suspended list. A 240-case original `Ob_AI` oracle verifies
captured-next traversal, same-family/later-family construction and two-stage death;
its separate `EveryFrame` leaf is isolated. Native manual input checks verify Web
slot 17 before simultaneous Jump slot 16, physical forward input selecting attach,
real animation recovery, particle retirement and posed lookaround torso/head aim.
The lookaround scalar controller matches 4,964 original-PC input and angle slices,
including the original exit fade calculation that differs from recovered C++.

The decompiled C++ in `src/` remains a primary behavioral reference, including
`src/engine/world/trig.cpp` and the player action files. Original executable
comparisons resolve uncertain branches and discrepancies. The reference code is
retained while full gameplay and story compatibility are still being verified.
Named environment visibility command 191 matches 3,150 original-PC cases and is
connected to authored render preparation and mission dispatch. It changes the
static G-region item independently of a moving platform; this is required to hide
the closed vault-door duplicate before the original platform script opens it.
Native vault checks pass all twelve authored visibility commands, both open/close
signals, original sounds and waits, persistent moving geometry and following
collision. Complete first-level progression remains in progress. Mission transition
commands match 261 original-PC cases; native retry handoff and carried save
state now have their live application owners. Authored end-of-level navigation
still needs verification. A shared checkpoint session adds
129 original-PC selection sequences and retains the original save-name-only
behavior. The real message subsystem and persistent application owner are now
connected; native checks press the actual retry button, reload the real level
scene, preserve its saved checkpoint and verify the authored restart position.

The playable scene now composes switches407, carryable objects401 and the bomb600
through one level object runtime. The bomb remains one actual401 body at runtime,
with the genuine adjacent trigger word, OBJ model, collision, posed pickup196,
carry and throw201. Native checks pass landing audio819C, source camera shake,
shared timer pause/rate/expiry, one-shot signals to the original controls65/70,
their downstream pulses and full cleanup. Big explosions join the source Bullet
list per model, including immediate native destruction and exact count release;
smoke joins the shared Flat particle list. The HUD advances and presents the shared
countdown at the original panel display phase.

Black Cat's native checks pass all four untouched L1A1 actor records with their
real models, ground, timed cinematic bytecode, waypoints, limb-hook footprint and
constructor/destructor ownership. Module189 supplies real factory functions and
immediate source module cleanup. Normal story startup now constructs its genuine
initial actors before creating and binding the real player.
The reusable production enemy runtime passes 752 native checks with authored189
module loading, central initial312 construction, genuine patrol, posed contacts,
webs, grenade/blast services and319 module destruction. The normal playable scene
now selects the authored startup. The complete native component regression
passes after its staged ownership and resource cleanup changes.
Original vertex lighting passes native upload and restoration checks. Native lookaround also displays
the original world reticle and follows animated hook2 with source tripod easing
and timed PutCameraBehind16. Ordinary and fire swinging pass the complete native
100/200/400 sequence on real L1A1 faces, including supply consumption, direct
arc timing, posed hooks, paired strands, widescreen geometry and release cleanup.
The playable scene now shares those owners with landing recovery, camera facing,
walking, jumping and falling. Native ordinary/fire checks pass actual Jump16
input through actions 64/2/4/8/idle on the original roof, including source torso timing,
apex physics, animation selection and shared fall references. The real node 218
proof verifies held-jump rejection, ground-contact grace and actual carried-item
drop. Native corner checks use a genuine map face and preserve the last-frame
hook pose across playback-only animation selection until the original pose phase.
Other player actions and the complete world-contact finalizer remain in progress.

The original trigger grid reader validates all 68 supplied map grids. Native checks
cross all 19 real L1A1 trigger models and verify source checksum queues, repeated
contact suppression, exit rearming and death suppression. The new player sweep
and contact-frame hooks await complete story startup before production activation.
Live reparenting also preserves the composed web runtime's genuine swing,
protected bit identities, native mesh RIDs and source clock; actual destruction
releases their resources.

The bounded original level catalog and completion rules now pass 1,216 original-PC
cases plus native checks against all 34 real records. Carried-save constructor,
destructor and no-player retry rules pass 2,884 original-PC cases. Application
mission181 retry now passes actual gameplay teardown, duplicate native retry
button presses and full SceneTree replacement while retaining the application,
authored checkpoint and raw carried history. Source302 retains its unsigned node
and resets to FFFF at level load. Native save checks pass held-input release,
accept/decline, flushed atomic private JSON, new-owner reload, corruption
rejection and cancellation on actual owner destruction. The sepia snapshot and
packed fade clock match original-PC vectors and complete exactly 120 native
source vblanks. Full startup, save loading through the frontend and completed
level navigation still need integration; these checks do not yet prove a
playable campaign.

The new story scene prepares real enemy factories and shared body/particle
owners before allocating a player. Native startup now plays the real intro,
constructs all 23 original actors with no player, binds the actual player/camera
and reaches Playing through the complete authored restart. Genuine source
compass and positional sound-loop services close the constructor450C/450B paths.
Source222/223 now share the actual player input VM and footstep bank. Native
startup passes through live222 and genuine physical-key skip, including first-AI
camera initialization, abort before private webs, VM completion, post-command
speech/stand/camera cleanup and once-only scene destruction. The successful run
also verifies live actor movement and the original player lighting uploads.
Normal level launches now select this authored story scene; the five component
verification routes retain their isolated fixture scene. The complete native
component regression passes; a whole first-level playthrough remains pending. The
scene queues the complete authored death restart257, including
its linked85/242 programs, rather than substituting the shorter roof checkpoint.
Typed native502 backgrounds now join the canonical Background head and detach
on actual destruction. Source start orientation passes 9,576 managed comparisons
and 27 native visits, including authored yaw, repeated matrix multiplication,
normal-cache thresholds and the stand fallback. Native source teardown passes
48 checks with real L1A3 actors, protected player/dome companions, all nine
explosion bodies, queued deletion, whole-tree exit, partial-startup native
destruction, messages/audio and retained retry controls.
Control effects pass 686 original-PC command/update/destructor cases. Player
brightness and render-depth state pass 7,227 original-PC comparisons, including
the first-level166/169 signed OT slots and184 dark-surface target. Source TTime
and attack phases now advance once per world update; EveryFrame/grenade age
retains its independent elapsed-tick clock. Camera setup retains the original
application-owned look/distance cache; its AI now runs after the actual command
and skip phase. Source187 reads and writes the same player angle-lock field used
by facing and PutCameraBehind. Hidden hand models still compute their original
lighting/stitch colors before visible-model submission. The complete native
regression passes, including actual pickup models/effects, difficulty gates,
training textures, costarm/HUD and protected-companion cleanup. The original
L7A3 pickup's392/393 links create live fire pickups; the bounded proof validates
those genuine source births before retiring every body it constructed.

The authored end-of-level305/110/162 commands now have separate body, protected
library and code-module cleanup owners. Source305 keeps a running command
continuation alive for subsequent resource cleanup/movie/mission selection.
Core checks cover protection by bank generation, region-order unloading,
clear-before-unlink code modules and unchanged status/name at305. Native checks
verify source110's protected common banks and immediate G/O GPU/collision
destruction, plus source162's actual reloaded312 destruction and module clear.
Authored end navigation is still unproven. Static ENV contacts2/9 now execute the original
5580C0 SendPulse program with explicit executable provenance;84 original-PC
boundary cases and101 unchanged records across five banks pass managed checks.
Native checks verify all101 actual ENV identities, queue/contact lifecycle and
the original L2A1 crate destruction producer. Chunk-based producers remain in
development.

Lookaround setup adds 265 original-PC camera matrix, position, collision-grid and
transition slices, plus 144 quaternion entry paths and 36 mode snapshot sequences.
The native frame now clears button edges and player mirrors after finalization,
preserving only source slots 14/15; 64 original-PC slices verify all forty bytes.
Native checks verify real world targeting and proportional screen-target triangles.

Crate explosions use real CFrag sprites and source glow radii rather than a 3D
replacement. Native checks verify shared Flat/Glow lifetime and protected cleanup.
Direct crate command-point dispatch has its own first-unexecuted checksum policy,
with 20,264 original-PC state transitions; contact notification retains its
separate semantics. Pending story commands run after actor and particle cleanup.
The shared scene actor directory now passes native checks for genuine thug/hostage
messages, body and damage routing, ordered signals/death pulses and handle lifetime
through death, pending deletion and removal.
Targeting audits
corrected recovered switch-type and signed/cone conditions against PC code.
Partial player pose packets now pass567 PC poses, including64 twelve-link/
eighteen-part cases. Native checks pass all original combat clips, nine hooks,
exact skin storage and independent frame-clock ownership. Named hooks, pose
links, fight records and initial animation sounds import as bounded PE data;
the modern game never maps or executes original machine code.

Native hostage checks pass24 assertions in an isolated original L1A3 world,
including startup, rescue/route, messages, speech ownership, tint/shadow, death
pulses and two-pass weak identity. Both actual model families pass all21 clips.
Signals are checked against genuine target identities; unrelated mission
programs are outside that fixture. Original switches add327 source/PC cases
and42 native checks for owned ENV meshes/collision/face words, actual aim
positions, visibility/color, counters, timeout/fade and ordered pulses.
Player combat impacts add570 source/PC comparisons and native red rings/lines;
ordinary emissions retain actual RNG order, projected geometry and lifetimes.
The bouncing-rock adapter now uses the original texture slots, source lifetime
and floor response. Native player services pass 32 checks using genuine thug,
hostage, switch, reticle, sound and posed contact resources in an isolated world.

Player pickup/carry/throw/switch actions match 665 compiled-source cases, with
the hard-throw calculation additionally checked against PC instructions.
Native checks use genuine L1A3 objects and vault models, original hand hooks,
source audio/frame timing, ballistic release and expired weak handles. Standing
selection matches 5,056 PC cases, including held objects. Manipulation construction,
AI, debris and chunk emission retain original RNG ordering; a native breakage
check exercises six real OBJ chunks, 24 smoke particles, sound and mission pulse.
The nine-model big explosion adds 128 source/PC cases and native lifetime/count
checks. Camera shake matches 416 source cases over 9,984 updates and a native
fixed-camera check. ZipDive actions add 550 source cases, including original PC
Grab checks. Native checks verify the actual punch/web chord, held-target
placement at the original hand hook, source grab-state gates and expired handles.
The original level's authored encounters still need production activation.

Two visible importer bugs were reproduced and corrected during manual testing.
Original face preparation hides nineteen large L1A1 map planes; 449 source cases
and a native check preserve collision data while retaining 115 visible G-library
meshes. Original hand-part selection hides the alternate open/fist model pair;
1,280 source cases and real idle/run/jump checks retain all eighteen raw poses
and hooks while drawing sixteen parts. Original first-level skyline models and
ground fog now follow the restart commands, camera-centered background placement
and recovered fog transition. Native captures show 357,886 changed background
pixels while every sampled Spider-Man suit pixel remains unchanged. This proves
background placement and foreground depth separation, not complete renderer parity.

Original web impact rings, surface splats and paired web fragments now have
engine-independent source timing and dedicated Godot renderers. Their 152 fixture
cases include direct PC instruction checks for ring and splat updates.
Native checks verify original texture uploads, particle lifetimes and consistent
proportions at both 4:3 and 16:9. Platform
damage adds 235 PC comparisons for byte-counter wrap, signed-short health and the
ordered death, sound and shatter calls. The native actor exposes its genuine body
metadata and damage override. Dedicated shatter now passes 200 original-PC
constructor/motion sequences and 96 complete item/face/split comparisons; native
checks render a real platform model and exercise lethal contact through the
production shatter adapter. Direct checks of 1,622 PC normalization cases corrected
an x87 rounding difference that the stronger shard comparisons exposed.
These effect leaves do not yet make the full web action or mission encounter playable.

Source allocation ownership now retains the actual signed CBaddy construction
count and constructor phase, including nested creation, failed construction,
suspension, source deletion and native destruction. Its 270 PC-checked sequences
and 22 native checks cover genuine control/platform objects; thug and hostage
constructors obtain their phase from the same scene owner. Region allocation
observes real library load/unload events and preserves the first-free forty-slot
policy with generation checks. Ordinary CWeb/CKnottedWeb and projectile/surface
web rendering pass native proportional-screen checks. Native trap checks now
follow 28 real animated thug hooks and eight original textured triangles,
including message delivery, rejected-yank fragments and cleanup. Full tug,
flaming webs and authored encounter activation remain in progress.

Implemented: checked PKR3 stored/zlib reads, CRC verification, PSX geometry
versions 0x20003/0x20004/0x20006, texture checksum references and raw UVs, trigger
node tables and command-list decoding, a mission event runtime, and a Godot
inspection scene using original textures and vertex colors. Texture decoding
covers 4-bit/8-bit PSX palettes and embedded PVR RGB565, ARGB1555 and ARGB4444
pixels in linear, twiddled, VQ and mipmapped layouts. Materials share image
resources across models. The level's `_L.psx` texture library is combined with
its `_G.psx` geometry. Indexed vertex colors resolve through the `RGBs` packet.
The inspection camera preserves vertical field of view on widescreen displays.

The initial asset check decoded all 2,473 archive entries and all 353 PSX files
(10,866 models, 219,870 faces), plus 68 trigger files containing 12,437 nodes
and 11,785 instructions in autoexec, command-point and restart command lists.
Godot loaded the first level's 137 geometry models/instances in a 64-bit process.
These are parser and engine-import checks, not complete gameplay validation.
All 9,916 embedded texture records across the supplied archives decode:
3,327 four-bit palettes, 1,748 eight-bit palettes and 4,841 PVR records.
An early managed checkpoint passed 26,822 assertions without original assets, including pulse order,
contact rearming, duplicate checksums, conditional execution, pulse limits,
byte-counter wrap, queues beyond 16 events, cyclic event budgets and failures
for missing handlers, plus hand-authored pixel patterns checking palette
channel/nibble order, padded rows, rectangular twiddling, VQ block orientation,
mip offsets and alpha expansion. The first level's geometry and combined 230
texture records were rendered under OpenGL at 1280×720. These checks do not yet
prove original-renderer pixel parity: gamma, fog, lighting, blended face modes,
texture animation and LTI replacement textures still require comparison.
Compressed type-44 animation packets now import into managed clips and checked
parent hierarchies. Clips decode lazily into frame-major poses, preserving
original clip IDs, signed deltas, interpolation truncation and 16-bit wrapping.
All 40 compressed skeletons, 4,269 clips and 73,388 frames in the supplied
archives decode within their stream boundaries, with at most three alignment
bytes remaining. This includes both supplied Spider-Man models, each containing
18 parts and 300 clips. The channel decoder matches 272 fixtures generated
from the recovered C++ decoder across all sixteen modes and block sizes.
Run `python tools/reference_animation_vectors.py` to regenerate these fixtures;
it requires a C++ compiler and uses synthetic inputs, not original game assets.
That animation checkpoint passed 72,870 assertions with assets. Hierarchy checks reject
missing parents, multiple roots and disconnected cycles. These checks prove
channel decoding against the recovered reference. Pose evaluation also matches
65 synthetic three-part hierarchies using recovered C++ rotation code and its
fixed-point translation rules, including signed overflow. Each part's rotation
is already in model space; only its pivot inherits parent rotation. Stitch
bindings resolve the file's Y index to an earlier attachment's position and
bone, preserving exact joins rather than blending separate pivots.

Type-42 matrix packets now import as well: all 68 libraries, 1,736 clips and
41,370 frames sample successfully in both looping and one-shot modes. The
sampler preserves the recovered interval behavior, including blending to key
zero when looping and extrapolating the preceding interval in one-shot mode.
Its results match 96 synthetic fixtures generated from the recovered C++
interpolation routine and accumulator helpers. These fixtures cover interval
one, sparse intervals, short clips, partial terminal intervals, multiple parts
and signed values. `python tools/reference_matrix_animation_vectors.py`
regenerates them without original game assets. Matrix key arrays are bounded
by their clip streams; shared streams reuse immutable decoded arrays.

Both formats implement the engine-independent `IAnimationPoseSource` and
`IAnimationPoseClip` contracts. Godot's `ImportedAnimatedModel` uses shared
single-bone vertex bindings and uploads the full affine matrices through
`RenderingServer`. This avoids pose drift caused by rotation/scale decomposition;
the broader check found that drift on Mysterio. The original compressed
hierarchy remains available as a `Skeleton3D` inspector rig; `ExactPart` nodes
retain the full model-space transforms. Rendering uses those exact matrices,
not the inspector rig's decomposed poses. The original player's two part-pose
swaps are retained. Skin RIDs are released when models leave the scene tree,
and partial model imports release their nodes on failure.

An OpenGL render-storage check imports all 108 animated assets and all 6,005
clips, samples 156,128 poses (both modes for matrix clips), and compares every
bound vertex against the uploaded bone transforms. The measured difference
was zero (threshold 0.00001 Godot meters). This verifies pose storage and model
import, not rendered pixel parity for every animation. Spider-Man and henchman
previews were separately rendered at 1280×720 and inspected visually. The
workbench frames the whole clip's bounds and supports
clip selection and pause. It now advances the recovered actor animation clock
on 60 Hz Godot physics updates. Actor controllers can disable automatic playback
and supply their own source elapsed ticks. These checks do not establish original-renderer pixel
parity or prove ceiling transitions in gameplay. LOD switching, lighting and
animated effects still need their gameplay adapters.

`ActorAnimationPlayback` ports RunAnim, CycleAnim and UpdateFrame as independent
managed state transitions. It preserves signed frame storage, Q16 fractions,
Q16 speed, reverse playback, one-shot target completion, the two-tick fallback
for zero elapsed ticks and the original single loop-wrap correction. Cycling
the current clip preserves its existing frame, direction and frame count.
All **1,536 synthetic playback cases** match the recovered C++ methods; all
**512 frame-advance cases** also match original x86 instructions at 0x460DA0.
Regenerate with `python tools/reference_actor_animation_vectors.py`; optionally
add `--original-exe /path/to/SpideyPC.exe` for the Linux/i386 instruction oracle.
The optional oracle maps a private copy of the executable and never starts the
Windows game. Original assets are not included in generated fixtures.

`ImportedAnimatedModel` now exposes RunClip, CycleClip, playback speed and
explicit tick advancement. The pose sampler receives one-shot versus loop
mode, including the matrix format's terminal extrapolation. Clip bounds are
cached separately for those modes. The original renderers sample whole frames;
fractional clock bits are retained without adding visual interpolation.
Invalid clock frames report a diagnostic instead of silently reducing them
modulo the clip length. Render-storage checks now also exercise clock-to-pose
sampling across all 108 animated assets, with reverse playback, half speed,
one-shot endings, loop mode changes, pause and actor-owned advancement on
nontrivial clips. Single/two-frame assets exercise their shorter playback paths.
Animation follow-ons, blending, sound events, actor update scheduling and
action-specific playback policy still require their gameplay controllers.

Level collision imports now retain face collision words and original Q12
normals. `OpenSpidey.Core.World` implements the recovered query masks and
category gate independently of rendering. All 125 `_G.psx` world and `_O.psx`
object geometry files decode into 187,188 collision triangles, including trigger surfaces, before
instance filtering. As in the original coarse collision code, render vertex
flags do not change the collision coordinates. Instance flags `0x21` disable
collision independently of render visibility. Model render-hidden flags alone
do not disable collision.

Godot creates one-sided concave shapes in separate player, camera, line-of-sight
and enemy query layers. Trigger surfaces use `Area3D` on a separate layer and
do not block a moving capsule. Imported bodies preserve instance/model IDs,
source face IDs and packed metadata; ray face indices map back to those records.
Render and collision imports share the recovered instance rotation and
coordinate conversion. Collision vertices bake that basis into the shape so
physics does not decompose the quantized transform.

On-disk face normal words contain indices. The original mutable asset loader
multiplies them by eight before its collision query shifts them back; the modern
immutable reader uses the original index directly. Source archive paths identify
hits across the separate world and object instance tables.

The first level imports 116 solid surface groups and 19 trigger groups. Native
Godot physics checks verify a front-face ray hit on each solid group, plus
hand-authored floor/ceiling/wall directions, back-face rejection, query-layer
exclusions, trigger area identity, original face mapping and a capsule landing
with its feet at the floor. Crawl checks also verify wall and ceiling support,
original clearance, travel along a wall and ground-distance attachment gates.
These total 545 checks with first-level assets. Asset checks additionally schedule
two original player models through wall/ceiling attachment, movement and stopping
for 75 source updates with cached-pose body hooks and exact skin verification.
Additional actors execute both E zip directions and wait for impact effects to
retire in the native fixture (105 source updates) and beneath an original
first-level overhang (109 source updates).
The imported overhang check selects a crawlable ceiling/floor pair through native
queries; the supplied first-level pair is instance 118, face 68 in `L1A1_G.psx`.
Both directions verify clearance, complete orthogonal body axes and exact skin
storage. Every source update also verifies the current skin pose against the
original clip sample: 105 fixture samples and 109 overhang samples include the
firing, active pull and landing frames. Optional rendered captures show intact
ceiling and floor poses.
Headless and OpenGL runs both pass. The engine CI job now includes
the fixture checks. This proves static shape imports and representative native
physics behavior, not complete equivalence to the old integer collision solver.
Moving objects, trigger-to-mission routing and surface transitions remain to be
connected. Ordinary controlled walking, stop recovery and ledge falling are now
also checked through these native queries.

All 94 restart records in the supplied trigger files now import typed spawn
positions, orientation, pulse links and checkpoint names. Positions retain
signed integer world units; they are not the Q12 coordinates stored in model
instances. Alignment uses absolute file offsets. Spawn names preserve their
original bytes, and truncated metadata and unterminated strings are rejected.
The command reader shares this metadata decoder when locating restart programs.

`MissionCheckpoints` and handlers for SetRestart (140) and its silent variant
(176) preserve ASCII case comparison, first-match duplicate names, initial
selection without saving, saves on checkpoint changes and the silent variant's
message suppression. The recovered non-death restart flag retains its latch
behavior. Mission tests exercise these commands through the event interpreter.
Persistence and HUD callbacks still need their game adapters; selecting a
checkpoint does not itself teleport the player.

The workbench can place the original animated player at a named restart within
a level. It converts whole-unit positions and applies only restart yaw, as
`SetStartOrientation` does. The first level's `Re_Restart01` (node 14) imports at
Godot position (-174.91, 17.23, 144.2), and its assembled model was rendered in
the level at 1280×720. This is a spawn inspection, not movement or floor-response
validation; original gravity, actor timing and locomotion remain to be ported.

The engine-independent player movement arithmetic is now ported to C#.
`FixedVector3` retains wrapping 32-bit Q12 components and arithmetic shifts.
`PlayerMotion` implements acceleration, per-axis damping, the inclusive velocity
dead zone, ordinary and collision-free tick displacement, velocity recovery,
fall displacement and horizontal wall sliding. `PlayerSweepGeometry` retains
the original body and shoulder ray formulas, separate horizontal/full lengths
and low-word products before signed division. The collision-free integrator
returns both position and velocity without mutating unrelated actor state.

These operations match 611 synthetic fixtures compiled from the recovered C++
headers and vector implementation. The same fixture inputs' complete
collision-free position/velocity steps were checked against original DoPhysics
instructions at 0x466CE0. The existing original-binary oracle also passes its
10,000 full-range movement samples. This verifies the collision-free branch
and recovered arithmetic, not the complete world collision solver or player
controls. The sections below describe the connected ground/crawl adapters and
actor timing. Moving supports, swing modes and full action coverage remain pending.

`GroundProbeFan` preserves the eight surrounding probes and final center repeat,
including float-coordinate quantization. `GameWord.TruncateToInt32` reproduces
the original float-to-signed-64-bit conversion followed by low-word retention;
invalid conversions return zero. Fifty synthetic probe fixtures match recovered
C++, and 530 float bit patterns match the original x87 _ftol instructions at
0x529C7C, including infinities, NaNs and large finite values.

Regenerate these synthetic fixtures with
`python tools/reference_player_motion_vectors.py` and
`python tools/reference_ground_probe_vectors.py`. Each optionally accepts
`--original-exe /path/to/SpideyPC.exe` for additional original-instruction checks
on Linux with an i386 C++ toolchain. Only the development oracles map original
executable code; the managed game remains 64-bit and does not load the executable.
Neither generator writes into the original game folder or copies original
assets into fixtures.

`PlayerGroundResponder` now ports the recovered floor and slope response:
signed whole-unit floor quantization, surface clearance, per-axis slope damping,
carried-object restrictions that restore the initial position, conditional fall
limiting and retained collision bits. Its result includes ground-contact grace,
surface normal, shadow position and requests for support/brightness updates.
All 472 synthetic cases match the actual recovered `respondToGround` method
compiled with actor stubs, covering the -2600 normal threshold, distance signs,
policy settings, held objects, flags and signed arithmetic boundaries. Run
`python tools/reference_ground_response_vectors.py` to regenerate them. These
fixtures do not execute original world queries or platform callbacks.

`PlayerGroundSolver` connects the original fall displacement, initial probe,
optional wide-probe order and response through `IPlayerWorldQuery`. A miss
applies fall and restores saved vertical velocity; the first hit terminates
probing. `GodotPlayerWorldQuery` implements that interface over native one-sided
player-terrain rays. Results preserve instance/model/face identity and packed
metadata. Collision triangles retain raw Q12 normals alongside normalized shape
normals; player hits rotate those raw normals with the original fixed-point
matrix and signed-halfword storage rules. Native rotated-fixture checks confirm
this distinction and the support/brightness flags.

Native checks now also exercise the managed fall solver landing on imported
terrain, coordinate conversion, floor/ceiling normals, source metadata,
zero-length queries, back-face misses and enemy-only exclusions. Godot supplies
the intersection point and distance, converted back to source coordinates and
whole units. Exact equality with the original integer intersector, particularly
near face edges and coordinate limits, remains unproven. Moving supports,
platform callbacks and brightness application still require their adapters.
The ordinary terrain controller now connects input and horizontal sweeps, but
the complete player action set remains unfinished.

`PlayerMovementSweep` now ports carried-object, body and shoulder collision
queries, their original offsets/order, normalized body normals versus raw
shoulder normals, selective body-contact retention and the two-attempt limit.
It preserves the separate carried-object collision bit and clears it on a
body/shoulder collision. Vertical-only motion begins with an empty hit rather
than undefined state. Contract tests check exact ray endpoints, unobstructed
motion, slides, carried-object probes, repeated hits and upward shoulder origins.
`FixedVectorMath` reproduces the recovered normalization and square-root
boundary rules; both match the 611 compiled movement reference cases.

`PlayerTerrainMotion` combines velocity integration, downward-velocity
separation, sweeps, permitted displacement, velocity recovery, ceiling clipping
and ground probing. Native Godot fixtures run ten grounded horizontal steps,
stop at an imported wall and cancel upward travel at an imported ceiling.
These checks exercise the combined managed terrain calculation through actual
physics queries. Actor input and animation now use this calculation in the
developing level session. Crawl probes and basic action dispatch are connected
separately; surface transition actions, arena constraints, swing controllers and
moving-platform behavior remain required.

`PlayerMovementInput` ports the movement input reader independently of Godot.
It accepts physical analogue/digital samples or a story-script axis override,
preserves signed-byte correction and inversion, and reproduces the movement
ramp, reference heading and one-time centering/button events. Digital fallback
applies only when both analogue axes are zero. Ignoring physical input still
allows scripted movement. A nonzero correction with a zero denominator reports
malformed configuration rather than dividing by zero. All 768 synthetic cases
generated from the recovered input method match; focused contracts also check
conflicting directions, ignored input, event ordering and signed-byte limits.
Run `python tools/reference_player_input_vectors.py` to regenerate those cases.
Godot InputMap bindings now provide WASD, arrow keys and the left stick to this
input-processing boundary in the developing level session.

`PlayerFlatMovement` ports free-plane velocity finalization and the sixteen-tick
speed ramp. `PlayerHeadingTurning` ports target selection, capped turn steps and
terminal snapping. Together they match **1,024 movement / 1,024 turn fixtures**
compiled from the recovered C++ methods, including full-range wrapping values.
Regenerate them with `python tools/reference_flat_movement_vectors.py`. These
fixtures cover the ordinary horizontal plane, not surface-local or boss-arena
movement; the comparison does not execute the original binary.

`PlayerTerrainController` connects input, previous-frame physics, orientation,
friction, ramping, finished-animation follow-ons, ordinary idle/walk/reverse/fall
actions and final velocity in source order. `GodotPlayer` advances it once every
two 60 Hz physics ticks: the original normal player loop is 30 updates per
second with elapsed ticks of two. Its model disables automatic advancement and
receives the same source tick count. Native fixture checks verify actual floor
support, walking, stop clips, idle damping and falling beyond a ledge. Those
fixtures use synthetic clip durations; asset session checks use the original
player model and real clip durations.

`PlayerAnimationFollowOns` stores all 110 nonempty records recovered from the
PC table at 0x555C6C by their clip keys. Original startup sorting moves records
to clip-indexed slots, including destinations beyond the 200 input records.
The modern dictionary avoids pointer-based sorting and preserves explicit
follow-ons to clip zero. Random wall-walk branches remain injectable.

`PlayerOrientation` builds quantized body axes from the support normal and
previous forward axis, preserving the recovered rotation factor and
degenerate-basis fallback. The normal cache is now actor-owned and updates
after target-based orientation too: the source's target branch could leave a
cached floor normal while applying a ceiling basis, then skip a later floor
rebuild. Tests cover floor-to-ceiling-to-floor orientation and parallel targets.
This is a deliberate correction. The connected E path now passes a rendered
round trip beneath an imported first-level overhang. This does not yet verify
every overhang or the original report's precise location.

`PlayerCrawlMotion` now ports the recovered forward, side, corner and support
probes. It preserves clearance, collision bits, raw normals, quantized idle
correction and the early return after missing-support recovery. Its result
exposes brightness, fence, hard-surface, support notification and ceiling-control
effects for the future action controller. Recovery's partial forward-contact
update remains separate from a complete hit, preserving its distinct semantics.
Managed cases cover these branches; native Godot cases cover wall support,
wall travel and ceiling support. The terrain controller now connects crawl
physics to attachment, idle, walking, reverse and stop actions. The standalone
adapter can retain a caller-supplied forward contact when no travel probe runs.
The source's main physics entry clears active body/ceiling contacts and exterior
corner detection before dispatching crawl physics; the connected controller
now follows that reset, confirmed in the original instructions at 0x466CEE–0x466D02.
Surface loss consumes the recovered grace updates before restoring free
fall orientation and gravity. Interior/exterior/fence transition action dispatch,
surface-control centering and support/brightness effects remain to be connected.

`PlayerWallMovement` computes wall-relative velocity through the recovered Q12
body basis and transpose. It uses effective surface heading independently of
camera yaw and shares magnitude, playback speed and movement scales with the
free-plane finalizer. `FixedRotation` retains wrapping 32-bit arithmetic rather
than float transforms. `PlayerBodyHook` preserves signed local translation,
cached root quantization, mirroring and the original one-sixteenth positional
smoothing. `GodotPlayer` snapshots the previous part-zero pose, root position and
body basis before advancing the animation and supplies the evaluated hook after
physics integration. Input
sampling in the terrain controller now follows physics and orientation as in
the recovered frame order. `PlayerWallAttachmentRules` now ports falling-action,
body-hit, face-normal, non-crawlable and nearby-ground gates, selecting the original
attachment position and clip. Managed threshold cases and native near/far-ground
cases verify these rules. Falling actions now apply wall attachment in the
developing level session. `PlayerLocomotionAnimations`
selects soft/hard crawl start and stop clips, including reversing an unfinished
start from its current frame. Ceiling attachment is exercised directly by native
actor checks and by holding jump during a native jumping cycle.

`PlayerJumpImpulseRules` preserves the recovered Q16 phase/hold timers,
ceiling cancellation, fractional impulse tail and final velocity override.
The terrain controller connects standing and running windups, their airborne
clips, button-release fall, held-jump ceiling attachment and release from crawl
support. Running jumps retain body-relative air heading; wall release suppresses
air movement until the recovered fall-reference timeout. Managed checks cover
windup frames, impulse timing, floor landing and ceiling release. A scheduled
original actor reaches a native ceiling, releases it and lands in 37 source
updates, checking its imported skin pose and complete body basis throughout.
Jump also interrupts active and completed rolls, preserving their recovered
orientation branches. Moving support, practice exceptions and combat
interruptions remain pending.

`PlayerZipWebRules` ports the E/RightOne straight-zip target checks, the signed
short clearance projection, arrival tests, landing orientation and eight landing
clearance probes. `PlayerTerrainController` now connects standing/crawl fire,
pull and landing clips, button cancellation and floor recovery. It preserves the
original collision-free pull window for clip 270 from frame 13 and clip 271,
confirmed in the binary at 0x466D90. `PlayerFallAnimations` retains the subsequent
fall/web-release decisions rather than prematurely replacing the pull clip.
`FixedAim` matches 519 compiled independent aiming, velocity and distance cases;
regenerate them with `python tools/reference_aim_vectors.py` (g++ required, no
original assets or retired reference files required).

`GodotPlayer` accumulates triggered buttons across its two engine ticks so a
short press reaches the 30 Hz source update. E/right shoulder starts straight
zip. Space/A jumps, holds for ceiling attachment, releases crawl support and
cancels an active pull. Moving-platform jump impulses, swing/practice controls,
damage interruption, carried-object cleanup, web resource
consumption and zip audio/camera effects remain to be ported.

`ZipWebStrand` now computes the recovered paired polylines, one segment per
80 units clamped to 1–40, random knot fractions, signed side offsets and
camera-facing inner geometry. The release contracts the cached strand over
16 elapsed ticks, preserving the strict retirement boundary. `GodotZipWebStrand`
draws both polylines in world space with the source tint. The player connects
them to the sampled hand hook, follows the wrist while firing and retains the
released effect until contraction finishes. Native zip checks read mesh vertices
back, compare them with the recovered points and assert retirement. The optional
overhang capture now also records the firing strand. The special
swinging/fire-web behaviors remain pending. The paired line
component is joined by the impact quad described below.

`WebImpactSplat` now preserves the original surface-oriented quad, ten-unit
normal offset, random roll, constructor update and integer growth toward scale
32 (settling at 31). Its neutral tint stays at 128 through age 30, decreases by
five thereafter and retires at age 56. `PlayerEffectTextures` loads the original
32×32 texture with checksum `0x3AF6DFF3` from `data/bits.psx`; original assets stay
outside the repository. `GodotWebImpactSplat` renders the textured transparent
quad and its tint fade, with scene-owned texture lifetime and actor-owned effect
cleanup. The source world loop moves bits after actors, so newly spawned splats
receive their constructor update and first world update before rendering.
Native checks compare each live quad's mesh vertices and material tint against
the source geometry, then wait for retirement. Rendered floor/ceiling captures
show the original impact decal. Fire tint variations and the
remaining general web effects are still pending.

`SpriteAnimationReader` imports type-69 sequencer packets with eight-byte names,
signed frame offsets, byte dimensions and texture table indices resolved to
checksums. It leaves asset bytes immutable and checks packet/frame boundaries,
counts, duplicate names and texture references. Original menu packets use zero
rectangle bytes, which remain valid metadata. All 11 sequencer libraries in the
supplied archives decode into 39 animations and 176 frames with valid texture
references. The common `webknot` animation has one protected frame: offsets
−12,−12 and a 24×24 texture.

`FlatSpriteProjection` ports the flat-bit integer perspective sizing, raw depth
floor of 200, projected scale gate and minimum 2×1 pixel rectangle. The strand
now renders the original knot texture at each recovered knot position. Its
camera-facing quads update with the active camera, use source scale 176 and
0.01/0.99 texture UV insets, and map logical pixels through the camera's vertical
field of view with a 480-pixel reference height. Native checks read back visible
knot meshes, verify texture binding and retirement, and require visible knots
on the original overhang actor. Off-camera fixture actors retain the source
visibility gates. General animated flat sprites, rotated/post-scaled variants
and complete legacy camera/projection parity remain pending.

`LevelSession.tscn` imports original level textures, static collision, a named
restart and Spider-Man, then connects the terrain controller and orbit camera.
The camera keeps vertical field of view for widescreen and clips against the
camera terrain layer; original camera behaviors remain to be ported. The level
loads both `_G.psx` world geometry and `_O.psx` object geometry. The original
first restart's supporting roof belongs to the object library. Headless and
OpenGL checks complete 70 source updates: landing on that roof at the canonical
spawn, walking, stopping and returning to idle with valid exact skin playback.
They assert the supporting asset and original clearance rather than substituting
a floor or moving the spawn. These prove ordinary terrain movement, not mission
playability or wall locomotion. Imported object geometry remains static until
the corresponding gameplay actor selects its model. Live model selection now
claims that model's static object-library prototypes, removing their old mesh
and collision nodes before the actor owns its placement and lifetime. This
prevents duplicate closed/motionless objects and prototype collisions from
occluding the live actor. Unclaimed object geometry remains available to the
developing session, including its original starting roof.

`PlayerScriptProgram` now decodes the signed-word player-control stream embedded
in mission command 199. All **167 streams / 838 instructions** in the supplied
archives decode through their Stop boundaries, including uppercase `.TRG`
entries. Partial words, truncated operands, unknown layouts and data after
Stop report source errors. These checks do not execute the archived scripts.

`PlayerScriptRuntime` replaces the original command blocks and encoded pointers
with managed, typed concurrent commands. Instruction timing and command updates
remain separate phases; completing a blocking command resumes decoding on the
following update. Commands remain active at remaining time zero and expire below
zero. It preserves digital register overrides, physical Start/Select states,
animation signals, checkpoint restoration, interruption and the level-1795 boss
area exception. The recovered move-to-target duration remains stored and is
not decremented. The opaque actor action value formerly at E00 is preserved
through an explicit host property; its gameplay consumers still need migration.
Malformed input register IDs fault rather than touching unrelated memory.

The runtime matches **96 synthetic sequences / 3,456 updates** from the compiled
recovered C++ VM, checking emitted button bytes/axes, cursor, elapsed time,
concurrent command count, actor state and callback counts. The **4,096-entry Q12
heading table** also matches the recovered initializer. Regenerate both with
`python tools/reference_player_script_vectors.py`; this reference generator
requires a C++ compiler with 32-bit support and uses synthetic actor/world
callbacks, not proprietary assets. The callbacks isolate scheduling and input
behavior; they do not verify original teleport physics, sound playback or
animation sound policy implementation.

`TriggerActorDefinition` reads type-one/type-seven factory metadata with
file-relative alignment, signed subtype/link counts, ordered links and marker
bytes, signed whole-unit positions and original angles. Marker one identifies
type-one startup creation; marker two bypasses initial suspension, and marker
four clears the recovered body flag two. Actor-specific constructor/script bytes
remain bounded source data. All 5,281 supplied factory records decode, including
1,169 startup creations. Most actor-specific behavior remains to be ported;
the common script interpreter and control actors are described below.

`TriggerPlacement` centralizes the recovered position layout for all fourteen
position-bearing node kinds: actors, waypoints, checkpoints, camera nodes,
powerups and lights. It preserves absolute file alignment, including records
that begin two bytes past a four-byte boundary. All 8,346 supplied placements
decode. Actor factories and checkpoints use the same reader. Actor script
fragment changes can now resolve other position-bearing nodes through the
shared position/angle layout instead of requiring actor-factory metadata.
Coordinates, optional angles and script fragments are independently bounded;
position-only records do not require a script tail. Synthetic checks exercise
every layout at both file alignments, varying link counts, signed coordinate
extremes, absent angles/scripts and mismatched headers. This reader does not
implement the gameplay behavior associated with each node kind.

`ActorScriptRuntime` now ports the recovered common actor flow and control-actor
scheduler. It supports label discovery/jumps, script-fragment changes, signed
comparisons, nested raw-word conditional skipping, arithmetic with getter-consumed
selector relaying, signal waits, tick/frame waits, death fast-forward and deferred
retirement. A wait that reaches zero resumes on the following update. The
interpreter retains bounded, absolute file-relative cursors instead of pointers,
yields cyclic programs at an instruction budget, and prevents replay after a
command or scheduling callback fails. Diagnostics include actor node, opcode and
source offset. Malformed labels/blocks/reads fail explicitly instead of reading
unrelated memory.

`ActorScriptVariables` retains common getter/setter widths, low-byte register
selectors, signed literal guards, aligned model/dword values, coordinate shifts,
health/damage/radius values, six registers/attributes, capped horizontal player
distance, visibility queries, random and link callbacks. Parameter roles not yet
identified remain typed values keyed by their source IDs. Actor-specific
commands and world effects belong to explicit host adapters. In particular,
0x4280's ordinary-level timer is connected; the recorded-physics branches for
levels 258/2051 require the physical actor adapter.

The interpreter matches 84 synthetic sequences / 2,772 snapshots from compiled
recovered C++ functions, including cursor, active/retired state, timers, signal
bytes, death behavior, health and registers. `python
tools/reference_actor_script_vectors.py` regenerates the fixtures using a C++
compiler with 32-bit support and the retained reference source. Synthetic world
callbacks isolate these flow/variable contracts; they do not verify graphics,
sound, camera, physical actor motion or complete AI. The asset suite also runs
the actual first-level control node 36: its 128-tick wait consumes 64 two-tick
updates, then stops and retires on successive updates. These are domain checks.
A Godot control-actor host is now connected as described below. Remaining
effect commands, actor modules, physical/platform controllers and full mission
command execution are still required.

`GodotActorRegistry` now owns live control-actor nodes and connects actor
creation to `MissionRuntime.ReceivePulse`. It updates them at the original
two-tick cadence after the player, maintains separate active/suspended lists,
resumes nearby radial actors after active updates and preserves the original
head-insertion order. `ActorLifecycle` separates suspension from death and
retains the dead/pending-deletion cleanup phases. `ActorSignalCommandHandler`
routes signals, suspension and activation through source links, including
repeated links and the type-one/type-seven target gate. Activation retains the
recovered active-list search; suspended actors resume through the distance pass.
`MissionLevelId` preserves numeric, extended-letter and demo level codes.

The Godot host connects common variables to player position, terrain visibility,
link lookup and source coordinate updates. Ordinary invisible control actors can
run the supported flow, move to the player and emit their linked death pulses.
Missing rendering, sound, compass and other effect handlers fail with source
diagnostics. Actor types 203 and 402 have registered factories; other actor
modules, general waypoint script fragments and look-around/corner ceiling checks
remain pending. `GameRandom` reproduces the original three-word signed stream;
128 sequences / 4,096 updates match the actual PC seed/Rnd functions, including
zero-bound state consumption. The level session shares one explicitly seeded
stream between player sounds, damage reactions and actor scripts.

The canonical first-level `play-smoke` now spawns original node 36 through the
actual mission factory. It waits 128 ticks, stops at update 65, emits exactly one
pulse to each of nodes 35 and 49 at update 66, enters pending deletion at 67 and
removes its Godot node at 68. Native checks observe ordered queue additions, completed command points
and verify the node has been freed. The original player still completes
its 70-update movement/skin check, followed by the library unloading check.
The queued camera commands now execute through the event pump described below;
this verifies that original actor/event sequence. The normal developing session
also launches the authored intro, initial actor census and restart sequence;
the complete first-level playthrough remains unverified.

`MissionEventPump` executes pending command lists after actors at the same
two-tick cadence. `CameraCommandHandler` now handles mode, timed zoom, profile parameter,
angle-lock and widescreen-value instructions. The first-level control actor's
nodes 35 and 49 finish their programs without leaving queued events: demo-mode
camera settings, the source lock polarity, profile updates, initial pulse limit
and widescreen request all execute in order.

`CameraParameterProfiles` preserves floor/wall/ceiling/swing/falling defaults and
the exact immediate-application gates, including falling action exactly four.
Offsets interpolate using a Q12 rate with per-update arithmetic truncation;
distances use whole-unit rates and their separate interpolation-enable flag.
Both snap to the target on the final update. The parameter implementation matches
325 synthetic sequences / 6,500 updates from compiled recovered setters and the
camera AI scalar blocks, including 75 zoom sequences / 1,500 updates.
`python tools/reference_camera_parameter_vectors.py`
regenerates these fixtures with a C++ compiler and retained reference source.

`ScriptedCameraRig` computes demo-mode desired positions and axes using the
recovered angle table, fixed rotation, distance calculation and yaw-rotated
offsets. `PlayerFollowCamera` uses those desired positions once a script selects
mode three, preserving vertical FOV in the current viewport. The legacy
widescreen value is retained as script state and does not force 4:3 gameplay.
Native first-level checks verify camera event execution, selected profiles,
finite camera position, complete basis and viewport aspect behavior. A real
1280×720 capture shows the assembled player after those events.

`CameraProjection` translates the original PC projection slope into a vertical
field of view. The original initial zoom is 2,365; `M3d_RenderSetup` uses
`(viewport width / 2) * 4096 / zoom` for both pixel axes, with a projection
constant of one verified in the supplied executable. At the original 4:3 aspect,
this corresponds to approximately 60 degrees horizontally and 46.83 vertically.
Godot preserves that vertical angle and expands horizontal coverage on wider
displays. This replaces the developing camera's temporary 60-degree vertical
angle. Script opcode 167 interpolates zoom with the original whole-unit rate
and final snap, including while the orbit camera is active; it does not switch
camera modes. Native checks compare both projection matrix scales against the
viewport dimensions before and after a four-tick zoom. Zero zoom is rejected
because it would divide by zero in the original renderer. No supplied command
list contains opcode 167; its scheduling checks use a synthetic mission list
and recovered-code fixtures rather than claiming a verified story use.

`FixedCameraCommandHandler` implements opcode 185 by selecting the first linked
type-twelve camera node in source order. Missing camera links leave the camera
unchanged, matching the original branch; malformed references fail explicitly.
All 57 supplied fixed-position instructions resolve. `FixedCameraPosition`
matches 160 sequences / 3,200 updates from the compiled recovered setter and
movement block, including wrapping coordinates, truncated signed division,
zero-duration changes and final target snaps. Regenerate with
`python tools/reference_fixed_camera_vectors.py`. The developing Godot camera
uses mode four's scripted position without terrain pull-in, tracks the player
as its focus and returns to demo following when mode three is selected.
New offset setters are ignored during fixed mode while the player profile
still stores the requested value. The native smoke check executes the original
64-tick opcode from `L1A2_T` node 22, verifies every observed native position
against the fixed movement state, reaches the exact final target and returns
to demo mode. It isolates that camera instruction rather than executing the
whole `L1A2` story. Mode four's rotation currently uses Godot's direct look-at;
its quaternion damping and separately tracked focus hooks remain pending.

`FixedQuaternion` preserves recovered Q12 quaternion-to-matrix and
matrix-to-quaternion conversions, signed arithmetic, trace/axis selection,
sine-table interpolation, near-identical blending and the starting quaternion's
sign mutation. The cyclic axis order was verified as `{1, 2, 0}` in the supplied
executable. The original zero-sum guards do not return; the later interpolation
overwrites their result, and the port retains that behavior. All 1,040 cases
from the compiled recovered functions match matrix terms, reconstructed
quaternions, interpolation results and mutated starting values. Regenerate with
`python tools/reference_quaternion_vectors.py`.

`FixedCameraCommandHandler` also handles opcode 186, reading four signed
quaternion words after a linked camera node's position. All 314 supplied
quaternion-oriented camera instructions resolve. `FixedCameraRotation` ports
mode five's timed target interpolation, first-update behavior and 1,023/4,096
view damping. It preserves the original negative countdown after an overshoot;
the desired rotation stays at its target while view damping continues.
The port matches 225 sequences / 4,500 updates from the compiled setter,
`CM_FixedPosAngles` and the recovered view-damping call. Regenerate with
`python tools/reference_camera_rotation_vectors.py`. Checks also cover immediate
position/rotation changes and rejection of truncated fourth quaternion words
before the live camera changes.

Godot receives mode five's converted view matrix rather than a look-at
rotation. The core keeps the recovered integer quaternion and matrix unchanged;
the rendering adapter orthonormalizes the matrix because Godot camera transforms
remove scale, including small fixed-point interpolation errors. The native
check verifies that normalized view, exact inverse coordinate conversion,
position and finite determinant throughout the original 48-tick `L4A1_T` node
96 instruction, then returns to demo following. This isolates one original
instruction; it does not establish complete `L4A1` story or pixel parity.

The demo camera still uses the developing native ray avoidance: recovered collision
pull-in/easing, quaternion smoothing, obstruction transparency, tripod/focus
hook smoothing, automatic surface-profile transitions, look-around and the
remaining camera modes are pending. Desired-pose/parameter checks do not prove
full original camera behavior or rendered pixel parity.

`SceneLibraryCommandHandler` dispatches load, unload, environment load, library
synchronization and object-library selection to a scene host. The modern adapter
loads synchronously, satisfying the source synchronization command immediately.
The developing level session prepares the original autoexec's resource requests
through `GodotMissionLibraries`, shares those decoded records with the world and
player importers, and retains the archive until scene exit. In the first level,
this loads the world and object libraries, shared textures, henchman, Black Cat
and explosion resources before the player is built. This resource preparation does not execute
the still-unported movie/module/actor/story commands in the level.

Native collision bodies and trigger surfaces retain their source library and
model checksum. The player query also returns that model identity for future
contact-to-story routing. A rendered first-level smoke check verifies the selected
object library, shared scene/animation records and the roof hit's model checksum.
After ordinary player movement, it unloads only the object library: its native
roof collision disappears while the original 137-instance world remains. This
check happens after any requested screenshot. Environment replacement/unloading
is currently exercised at initialization and the controlled smoke boundary;
loading during gameplay physics, live actor ownership and streaming failure
recovery remain to be integrated.

`PlayerScriptCommandHandler` transfers a decoded script from the mission runtime
to the player controller callback, preserving absolute source coordinates.
`GodotPlayerScripts` now owns that runtime and supplies live position, velocity,
camera heading, animation and trigger-node data. `PlayerControlFrame` selects
scripted axes/action buttons inside the terrain controller's input phase, after
physics and animation advance and before movement input/follow-ons/action
dispatch. The script overrides physical movement and action input for its final
update too, releasing control on the following update. Source slots four, six,
seven and sixteen through nineteen map to targeting, straight zip, swing,
jump, web, kick and punch; unported action consumers still need implementation.
Other raw script pad state remains available from the VM.

The live host connects standing and the original `0x40000000` delayed-stand
action. Script teleport uses the original 256-unit ground probe, clears velocity
and crawling, reorients surface attachment and enters idle or falling according
to that probe. The source's direct checkpoint-position restore remains separate
from teleport. A new bytecode stream resets cursor, checkpoint and synthesized
pad bytes while preserving the elapsed script clock and previously queued
concurrent commands, matching `SwitchToSynthesizedInput`. The Godot session
supplies the current player position for the level-1795 arena exception.
Player-script/physics faults stop the actor, mission pump and session rather
than continuing partial input execution.

The rendered smoke check runs original first-level command point 77 through a
mission runtime. It teleports to node 75, plays clip 176 and finishes after
55 source updates, while injected physical movement/jump remain overridden.
It then restores the canonical player position and runs the existing resource
and camera checks. This isolates an original story command point; full startup
and first-level progression are still pending. The original script-entry
cleanup of carried objects, smoke trails and all attachments still requires
their gameplay owners. Voice playback now uses the speech adapter below;
menu navigation still requires its adapter and fails explicitly when absent.
Animation sound policy now controls the player frame-sound driver below;
story animation-wait signal delivery remains separate and pending.
The scripted look-at target is retained but its head-tracking
consumer remains to be ported. These checks do not establish complete story
playability or execute every imported player-control stream.

`PlayerInputSuppression` ports mission opcode 163's physical-input countdown.
It captures animation speed through the original signed-short field and exits
active lookaround through an explicit adapter. The countdown runs after actor
animation advance and before behavior/physics preparation, clears pressed and
triggered action/direction bytes on every locked update, and leaves auxiliary
bytes and physical menu controls alone. Reaching exactly zero still swallows
action buttons on that update, while raw analog movement becomes available;
crossing below zero also restores the captured speed before normal preparation
resets it. Script-driven input suspends the countdown and continues controlling
the player. Setting zero unlocks without a speed restore or lookaround exit.
All 336 sequences / 4,032 updates match the compiled recovered setter and the
`CPlayer::AI` countdown/pad-clearing block, including odd/even durations,
negative values, saved-speed truncation and scripted suspension. Regenerate
with `python tools/reference_input_suppression_vectors.py`. The native smoke
check routes an unsigned mission duration to the live player, verifies blocked
physical movement/jump, exact-zero analog release and a wait script that leaves
the timer unchanged. This is input suppression, not behavior suspension or a
complete cinematic input/navigation implementation; original lookaround remains
unported in the developing session.

`PlatformMotion` ports the recovered `CPlatform` translation, angular integration,
destination seeking and scale state into the managed core. It preserves inclusive
velocity dead zones, signed short angular wrapping, the special level `0x301`
integration stride, physics modes zero and three, and the independent half-tick
scale counters. Scale commands apply their initial increment immediately and retain
the source signed-rate/unsigned-duration behavior. Destination arrival uses the
whole-unit vector length and crossing test. Recorded body motion explicitly raises
an unsupported error until its separate physics adapter is implemented.
All 157 sequences / 1,570 updates match compiled recovered `CPlatform::DoPhysics`,
the scale block of `CPlatform::AI`, and the three axis scale commands, including
overflow and dead-zone boundaries. Regenerate with
`python tools/reference_platform_motion_vectors.py`.

`GodotPlatformActor` now connects this motion to the mission actor factory for
type 402. `GodotScriptActor` holds identity and lifetime shared with the existing
control actor. Platforms defer parsing until their first AI update, consume waits
by update count, and keep moving/rendering after their script completes. The
adapter resolves model checksums from the selected object library, builds the
source mesh and local collision faces, retains collision metadata while moving,
and handles destination, velocity, angular motion, scale, trodden waits and death
commands. Native checks execute original first-level actor 55 through its linked
route and 30-update wait; its non-colliding spinning model remains non-colliding.
A shortened script retaining actor 54's actual model selection verifies that a
completed script keeps a visible body and that native rays hit its translated,
rotated solid faces. Control-actor lifecycle checks still pass after sharing the
registry. The gravity variable enables the original default vector `(0, 0x8000, 0)`
after acceleration reset and destination processing; its two-tick integration
and disable behavior are checked natively. Remaining platform work includes
damage/healing, other effects, recorded special motion and complete original
door sequences. The vault opening bug is not yet claimed fixed.

`PlayerSupportRules` ports the source finalizer's one-velocity platform travel,
airborne vertical exclusion, collision rollback and support-dependent jump
impulse. All 216 vectors match compiled `PlayerFrameFinalizer::rideSupportBody`
and the standing-jump impulse block, including signed overflow and the negative
velocity threshold. Regenerate with `python tools/reference_player_support_vectors.py`.
The controller refreshes the previous support's live velocity before ceiling
clipping, clears its contact identity for physics, reacquires it from floor or
crawl responses, notifies it before action dispatch, and rides it before vertical
jump impulse finalization. The source airborne counter retains unsigned 16-bit
wrapping; script teleport clears stale support identity.

`IPlayerSupportActor` and `IPlayerSupportWorld` keep actor identity, velocity and
contact notification separate from the player rules and native ray queries.
Each imported live collision body retains its owner's 64-bit instance ID, so
multiple actors created from the same trigger node remain distinct. Native
coverage uses original first-level platform 170 at its actual placement: player
contact resets its trodden counter, resumes its `0x4300` wait, plays voice
bank/slot/priority `1/9/50` through `0x42A3`, and leaves the model visible after
the script finishes. Repeated player/platform updates retain source riding
and collision ownership; dead support is rejected even when its collider can
still be queried. Full vehicle behavior, support health effects and complete
world orchestration remain work for the remaining actor systems.

`ActorUpdateGroup` now preserves the original post-player list order: power-ups,
bullets, miscellaneous objects, environment, background, enemies and control
actors. The two implemented actor types declare their family rather than relying
on global birth order. Each family retains source head insertion order, and its
snapshot is taken when that family's update starts, allowing earlier families
to create actors for later families during the same frame. Suspension cleanup
still follows all family updates. Control and platform speech commands share
the same signed-word request decoder and session voice callback.
The native ordering fixture interleaves two platform/control pairs using
original factory metadata and controlled wait/voice scripts. Their requests
become ready on the same update; it verifies environment-before-control ordering,
the first equal-priority request winning, a new control actor running in that
same frame, and its higher-priority request preempting the earlier one. The
remaining five actor families require their actual actor adapters before full
world simulation is playable.

`ActorBodyMotion` ports the complete shared `CBaddy::DoPhysics` movement kernel
used by enemies. It retains local velocity rotation using the pre-update Euler
basis, its 64-unit quantization, separate world velocity, acceleration and
angular update order. The alternate world-velocity path applies per-axis shift
damping, inclusive small-value removal and elapsed-tick angular travel. Limited
motion timers, local-motion suppression, recorded interpolation, hold periods,
unsigned timer comparisons and exact final position/angle snaps are preserved.
All 540 sequences / 3,240 updates match compiled recovered physics with its
original vector operations and rotation helper; regenerate with
`python tools/reference_actor_body_vectors.py`.
`ActorAngles` and `AxisDamping` now name the shared motion primitives rather than
tying them to a platform or player class. This kernel is ready for actor adapters;
live enemy orchestration, chase/combat behavior, hit processing and
animation/event integration remain required. Enemy actors are not yet enabled
in the playable session, and the kernel alone does not provide enemy AI or
terrain collision. Recorded platform commands still require their script adapter.

`ActorWaypointRoute` ports `CBaddy::GetNextWaypoint`: it follows the first link,
resets waypoint flags on a successful transition, and parses scripts only for
node kinds 1000/1002 while resetting their parameter. An endpoint or disabled
route retains its last state. Waypoint commands preserve run/pause flags, the
additional source bit settings and the unsigned parameter. Thirty sequences /
180 advances match compiled traversal callbacks; regenerate with
`python tools/reference_actor_waypoint_vectors.py`.
`ActorScriptRuntime.ParseAt` lets an actor execute a new waypoint cursor without
resetting its labels, variables or active flag. Asset checks run all four
first-level thug constructor scripts (health 90, damage 10) and their actual
waypoint pause scripts, including the terminal route.

`ActorProcedureList` schedules facing, movement, acceleration and falling in
source head order, with category replacement, completion signals and deferred
retirement. `Neutralize` clears motion while preserving protected procedures.
Facing precedes the movement it creates, allowing alignment and travel in one
update. Point, yaw and recoverable-body targets retain the original signed-short
turning rule; 642 sequences / 3,210 updates and 560 direct yaw cases match
compiled source. Waypoint movement preserves normalization, facing waits,
wrapping arithmetic and the source's asymmetric endpoint comparison; 513
constructor states / 4,104 updates match compiled source. Falling and local
forward acceleration retain their original landing, delay, clamp and signal
behavior across 1,000 sequences / 7,000 updates. Regenerate these fixtures with
`tools/reference_actor_{steering,move,kinetic}_vectors.py`.

Thug configuration, type selection, state flags, guard behavior, animation mode
tables and every patrol substate now have separate Core classes. The 17 state
flag pairs and the thug/henchman animation tables were read from the original
executable because their recovered declarations lack initializers. Patrol
uses the shared movement procedures and original waypoint scripts, including
run/walk selection, terminal routes, chase transitions and idle/spit pauses.
Configuration, guard, animation selection and patrol have 4,698 compiled-source
reference cases; initial floor placement and guard/patrol selection add 160
sequences / 960 updates. Ground queries, fight decisions, attack ownership and
spit effects remain explicit adapter dependencies. These classes alone do not
make enemies or complete missions playable.

Thug perception and fight decisions retain the original distance cache, clock
wrap, line-of-sight memory, sight cone, strict range/height limits, path-query
reuse, ownership effects and signal/early-return behavior. Their 2,486 cases
match compiled source. Melee attack substates, turning, frame-window selection,
recovery timers and retreat have 2,405 source cases. The attack monitor samples
animated part hooks, caches the first sample, extends later swept segments,
applies hit effects/sounds/damage and preserves non-damaging completion masks;
300 sequences / 2,100 updates match the recovered monitor and `SmackSpidey`.
The managed previous-point buffer begins at zero, making the legacy allocator's
uninitialized first-sweep case deterministic when a dome suppresses sampling.

`ThugCombatCoordinator` owns the primary attacker, ranged fight owner and eight
surrounding slots as managed actor references. Assignment and clearing preserve
quadrant selection, byte cursor wrap, occupied masks, closer-attacker replacement
and callbacks across 2,304 source cases. Removing an actor releases its ownership;
a fight callback that already removed the previous primary actor is handled
without dereferencing a stale reference.

Chase dispatch, attack-slot targets and lost-player search now have 4,880
compiled-source cases covering recursive advancement, path retries, byte cursor
wrap, timers and real movement arrival. `ActorPathController` implements the
original three-ray capsule, ground-gap subdivision, blocked-point interpolation,
bounded point insertion and waypoint offsets. Its 964 cases check both result
codes and complete ray/ground callback traces. `GodotActorWorldQuery` now supplies
the original collision-mode and environmental-object policies using imported
face metadata. It retries rejected groups to find the nearest valid face and
refreshes stable live platform support snapshots. Native checks pass all four
original first-destination capsules, a genuine roof boundary with absent ground
and wall blockage, and platform 170's inclusion/exclusion, source vertical support
velocity, height convergence and dead-owner rejection. Suspended environmental
owners are also excluded; full live combat orchestration remains integration work.

Awareness behavior covers caution, shooting eligibility, confusion, contact and
the six source AI timers. Hit callbacks precede the later timer/contact tests,
close-player queries use the original distance cache and height/clearance rules,
and delayed gun sounds/conversation retain their original cadence. The 2,292
compiled-source cases pass in the full asset suite. Native enemy conversation
playback and ranged projectile adapters remain required.

Ranged behavior preserves shooting restart/continuation substates, the four
thug/two henchman shot frame schedules, hook geometry, positional sound, tracer
and flash parameters, grenade launch parameters and persistent state-exit
messages. Its 2,788 compiled-source cases pass. Enemy damage and directional hit
reactions, recovery, sliding and uppercut spin have 914 cases; a zero-frame slide
now retains the hit reaction without dividing by zero. Fall, flight-hit recovery,
death animation sequences and staged fade/zombie cleanup add 2,620 cases.
Grab, yank, backpedal, web throw, web trapping and acknowledgement add 2,236
compiled-source cases. Hostage shooting adds 650 cases, including target-handle
replacement, its distinct shot geometry and actual facing/message procedures.
`ThugBrain` brings these source AI phases and state handlers together, with
named `ThugBehaviorState` values at the compatibility boundary; 640 source
updates verify phase order, masks, shadow-width wrapping and selected dispatch
paths. `GodotThugActor` now compiles as a typed native adapter with actual source
models, animation, hooks, message/procedure phases and player combat. Its effect
services and native integration checks remain prerequisites for registration.

`ThugConversationBehavior` retains source actor order, candidate type priority,
distance/cache limits, reciprocal partner references and all seven dialogue
choices. Its 815 source cases are separate from native speech integration.
`ThugFrameSounds` follows the original frame-equality cues and sprite-ring
requests, including repeated-frame events and all random sound choices across
1,504 source cases.

`GodotActorSpriteRing` imports all eight original SMOKE frames and retains the
source ground query, 24 particles, shrinking and eight-update color lifetime.
Rendered checks use actual 640x480 and 1280x720 viewports, uploaded pixel hashes,
source quad geometry and a genuine side-frustum test. Both sprite dimensions
scale with viewport height while widescreen expands horizontal view.
`ThugBulletTracerState`, `ThugBarrelLineState` and `ThugRibbonProjection` add 625
source/actual-PC cases for textureless ribbons, persistent barrel endpoints and
screen normals. The original executable corrected a recovered X/Y subtraction
error in the ribbon normal helper. Native ribbon and impact checks pass in real
4:3/16:9 viewports with the original HitPing texture and animated hook attachment.
Impact motion and lifetime add 911 cases; combat rings add 770 cases with exact
clock growth, anisotropic scaling and delayed equality death. Their native ring
checks verify the original HITSPRIT pixels, both color banks and proportions.

`ActorShadowState` adds 409 source cases; all 256 original PC blend selectors
also pass direct machine-code checks. The source API's subtraction flag still
uses the PC renderer's actual mix mode. `GodotThugAppearance` owns mesh/material
copies for exact byte-quantized tint and corpse transparency. Its actor-level
checks remain part of the upcoming live enemy service integration.
`ActorRecoilCollision`, `ThugFallBackBehavior` and `ActorYawRotationProcedure`
add 771 source/PC cases for target-list priority, inherited environmental masks,
active elastic-hit fields and per-invocation rotation. The rotation procedure
retains the original behavior of stopping further procedure traversal.

`ThugMessageProcessor` retains newest-first insertion, persistent message ages,
deferred replies/cleanup and source state/procedure effects across 858 cases.
Hostage readiness and action-path reactions add 784 cases.
`ActorLaserCollision` uses the original player sphere and environmental
fallback, separately from melee part boxes; 1,049 compiled-source cases retain
direction scaling, endpoint/pointer exclusions and query ordering. The native
environmental traversal preserves the last zoned face masks even when the LOS
flag changes, verified with real collision shapes and an active mechanism owner.

`ThugTorsoAim` preserves unsigned distance division, local joint roll and its
asymmetric easing. Its 764 cases / 4,584 updates match the original PC function;
the ArcCos helper's 8,193 finite inputs and NaN integer conversion were also
checked directly. Source sound offsets remain explicit event parameters, while
the PC `playSFX` routine at `0x472490` ignores them in its final pitch calculation.

`GrenadeProjectile` covers ballistic travel, bounce, settling, fuse, shadow,
glow and ordered explosion requests across 585 source cases.
`ProjectileBlastDamage` adds 1,159 cases for falloff, signed/unsigned narrowing,
object/environment bounds and linked-list mutation during hits. A deliberate
crash fix applies full damage without directional impulse to coincident or
subunit targets, where the original divides by zero. Native projectile checks
now pass with the original first-level object model and a verified G-library
roof: real collision normal/bounce, positional sound, protected glow, fuse,
actual player damage, both explosion models, flash and two-pass cleanup.
The default projectile checksum is absent from the supplied object libraries;
the importer reports it rather than choosing unrelated geometry.

`GlowState` construction and all radial/fringe setters pass 71 sequences against
the actual PC constructors and setters. This proof corrected a recovered fringe
color stride: the executable indexes rows by section count. Glow flashes,
expgrnd model effects and their weak-handle carriers add 333 compiled-source
comparisons for fade, radius selection, growth, delays and ownership counters.
`GodotGrenadeExplosions` keeps bullet model updates separate from carrier bit
updates and retains two-pass body deletion. Source-dead models remain visible
and recoverable until deletion; the secondary model survives its carrier.
Native render and ownership checks pass, including 190 independently compiled
DisplayGlowList/PC submission vectors, genuine expgrnd model polygons/textures
and the separate model/carrier lifetimes. `GodotThugVisualEffects` now composes
these owners; its native check also verifies all three original actor model and
collision families and actual Shadow2 resources. Gameplay integration remains
separate from these effect and resource checks.

The shared session random stream now uses `GameRandom`, checked against actual
PC seed/Rnd instructions for 128 sequences and 4,096 state transitions. Source
zero-bound draws still consume state. Player sounds and actor randomness share
this stream rather than independently seeded platform generators.

`HostageState`, `HostageInitialization` and `HostageBrain` preserve the male305
and female315 factories, script-before-model order, all seven behavior states,
persistent messages, routes, animation banks and accepted speech attachment.
The 232 constructor/AI sequences match compiled recovered source across explicit
world/procedure/speech boundaries; the hostage death leaf also matches PC code.
Their native actor integration is being validated separately.

Player combat uses the shared `ActorHit` fields for eligibility, armor/health
damage, interrupted effects, pain sounds, reaction physics, death entry and
animation recovery. The 665 source cases pass, including the ordinary enemy
melee hit reducing health 100 to 90 and returning from its hit animation.
Damage timers add 304 cases for temporary protection, hit-feedback and
electrification countdowns. A fixture-only ABI correction binds the recovered
death sound's raw `this+8` position to the instrumented host's named position;
the production damage logic did not change. Death progression adds 205 cases
for animation completion, the 120-tick delay and ordered application handoff.
`GodotPlayerCombat` connects the controller to original posed collision boxes,
shared audio channels and conditional actor-owned speech stopping. The level
session supplies camera reactions, source flash reset and a checkpoint retry
screen; complete game-wide death/menu/save flow remains integration work.
The rendered native test passes 149 checks for unswapped posed contacts, repeated
100-to-90-to-80 damage/recovery, ground death and airborne corpse landing.
`PlayerActorState` keeps body death, minor radius and costume invisibility
independent from health and locomotion.

`PlayerKillQuips` adds 499 eight-call source/PC sequences for first contact,
eligibility, early/late windows, source history, retries and XA requests.
`GodotPlayerKillQuips` uses the actual native player health and shared speech
channel. Its native check passes original WAV playback, priority rejection
with history advancement, encounter completion and unchanged actor XA ownership.
`PlayerGrabUpdate` adds 382 four-update source/PC sequences for action gates,
hook placement, yaw and caller-versus-player position ownership. Combat action
selection and target tracking remain separate integration work.

`ScreenFlashState` preserves the source flash/fade counter, priority, unsigned
color and reset behavior across 29 compiled-source snapshots.
`GodotScreenFlash` draws a viewport-sized overlay with the original PC alpha
blend and explicit source-clock updates.

Player melee collision uses original posed part boxes, two transposed rotations,
signed short narrowing, Q12 scale, hidden-part masks and closest-part selection.
It preserves strict face crossings; tangent, endpoint and entirely-inside
segments are not treated as hits. Four hundred box clips and 400 posed-part
sweeps match the supplied PC executable's routines at 0x4F7680 and 0x4F7AE0.
The PSX box reader selects near models and keeps the original upper/lower header
ordering, including the original player's accepted model flag8 and all eighteen
independently extracted header boxes. `GetSourceAnimationPartPose` supplies raw
unswapped matrices for melee collision; `GetSourcePartPose` supplies active
corrected poses for hooks while rendering keeps its player part swaps. Web-hand and body
hooks now use that accessor, checked against raw animation samples and in the
rendered player/overhang regressions.

`ActorPoseLinkPacket` and `ActorJointPoseBuilder` implement original local joint
corrections and the bounded ancestor stack. All 503 vectors match PC `BuildPose`;
the native thug/henchman/henchngt checks compare corrected hook poses and actual
shader skin storage against original-asset gold, retain raw collision matrices,
then disable corrections and verify baseline restoration. The correction
builder preserves x87 final-expression precision without changing established
base-animation rotation behavior; combined body-angle precision remains a
separate audit.

`smoke --enemy-patrol --rendered --assets /path/to/original/game` runs the four
actual starting L1A1 thugs (9/10/16/19) for 128 source updates. Deferred thug 11
is excluded using the original `InitializeOnLoad` marker. Animation advances
before AI, matching `ActorListUpdater`, and each update carries two source ticks.
The rendered check passes startup scripts, real floor hits checked independently
against original face planes, changing model poses, persistent visible meshes
and exact first-waypoint X/Z arrivals. Nodes 19, 9, 10 and 16 reach those points
on updates 23, 31, 38 and 59 respectively. This fixture exercises unalerted patrol;
it does not register unfinished combat actors or prove complete mission behavior.

The native service composition also passes 11 checks using those four actual
actors: source constructor/patrol/cached transforms and a real melee contact
reducing native player health100 to90 followed by animation recovery. Each
actor passes unchanged skin, exact byte tint, corpse blend and Shadow2 ownership
checks. These actors still run in isolated verification rather than the gameplay
factory while remaining interaction contracts are connected.

`ActorHeightController` retains the shared ground query gates, byte timer
wrapping, moving-support adjustment, cached ground height and quarter-step
convergence with separate static/support snap thresholds. Failed queries retain
their original support-out side effects. Its 510 sequences / 4,080 updates
match compiled `CBaddy::SetHeight`; regenerate with
`python tools/reference_actor_height_vectors.py`.

`ActorHookPacket` bounds the original signed hook records and their part indices.
`ActorHookPosition` places effects in the original animated part pose and actor
world transform, retaining short local-coordinate stores, model/world scaling
and cached transform translation. The two original thug/henchman packets and
400 affine/dynamic position cases were also checked against the supplied PC
executable's actual routines at 0x46F820 and 0x453EE0, including overflow and
fractional positions. Dynamic hooks do not use the static/render mirror flag.
Regenerate with `python tools/reference_actor_hook_vectors.py --original-exe
/path/to/SpideyPC.exe`; the binary remains private and is not linked into the
modern game.

`smoke --enemy-models --rendered --assets /path/to/original/game` validates actual
enemy mesh corners, bone influences, weld references, near LOD selection and
every animation frame. The native check passes for `thug` (16 parts, 44 clips,
70 weld references) and `henchman`/`henchngt` (15 parts, 41 clips, 74 weld
references each). It reconstructs attachment bindings independently from raw
vertices and rejects applying the player's part swaps to enemies. The rendered
pose storage checks report zero vertex transform error for all three models.

Thug spit uses the source frame-30 threshold, raw item-flag/distance gate, mouth
hook, positional sound and six particles with separate X/Z random draws. Four
hundred cases match the recovered spit routine. `LineParticleMotion` preserves
clock differences, strict lifetime expiry, one-update trails, wrapping motion
and once-per-call vertical acceleration across 600 sequences / 4,800 updates.
Godot draws these world-space lines with their endpoint colors. The enemy model
smoke check also verifies actual rendering-server vertices, parent-transform
independence, exact lifetime boundaries and expired geometry removal. This proves
motion and stored geometry; rasterized line width and legacy blend parity remain
renderer acceptance work.

`SpeechCatalog` preserves all 1,264 PC voice slots and the separate XA
availability mask. Both tables were checked against the supplied executable;
the filename table matches the recovered source, and 934 slots are available,
including twenty slots whose filename is `blank.bik`. The third player-script
speech operand is priority, not duration. `SpeechController` ports the original
priority gate, file-I/O/movie gates, pause, stop, first-pending-request policy
and thirty-vblank cooldown. The direct play routine does not test the caller's
XA-allow flag, matching the recovered PC behavior. Bounded slot values can
address neighboring banks through the source's linear index. Sixty synthetic sequences /
4,740 operations match compiled `Redbook_XAPlay`, reset/stop/pause and
`Utils_VblankProcessing`, with a synthetic decoder backend. Regenerate with
`python tools/reference_speech_vectors.py`. Mission opcodes 136, 137 and 139
now route allow/disallow/play requests; player-script voices use the same channel.

The PC's `speech.str` partition call is a stub: speech actually uses Bink files
under `data/Voice`. `OpenSpidey.MediaImporter` converts the 903 present catalog
files to signed PCM16 WAV in the private cache, preserving source audio rate
and channels. Twelve listed files are absent from the supplied archive; their
paths are recorded in `voice/missing.json`, and accepted requests complete as
failed opens, matching the original control path. The catalog contains 915
unique nonblank filenames; availability and file presence are distinct.
All present files pass archive CRC validation during preparation and retain
converted-file SHA256 stamps. `tools/modern.py voices --assets ...` prepares
speech explicitly; `play` and `play-smoke` also prepare/verify it automatically.
No proprietary audio is copied into the repository.

`GodotSpeechPlayer` owns a single native `AudioStreamPlayer`, caches decoded WAV
resources and routes mixer completion into the source speech controller. It
preserves the PC's mute/maximum gain conversion. Native checks run a player
script requesting tutorial track 67/15 (`stan_00`), verify mono 44.1-kHz PCM and
source gain, pause/resume, priority suppression/preemption, natural completion,
queued playback after cooldown and an absent source file. Audio smoke checks
use Godot's dummy output driver. The mixer clock is independent of physics
catch-up; the pause check lets its fade finish before requiring a stationary
cursor. Backend EOF is the decoded PCM end rather than the legacy dummy-video
frame marker. Deferred cache/decode faults stop speech and the developing
session. This does not implement positional speech, subtitles, settings UI,
music management or general animation sound scripts. Shared SFX channels are
described below.

`SoundDefinitionBank` decodes the original four-word `.sfx` records into checked
flags, asset index, pitch, volume modifier, falloff and aliases. It preserves
the recovered loop-bit/shift packing and first-match alias lookup, while
rejecting truncated records and unterminated/oversized banks. All 63 supplied
definition files decode into 2,218 entries. The 58 PC WAV groups and the
256-entry attenuation curve are retained as executable-verified metadata;
original PCM WAV bytes remain in the owner's archive. Regenerate the group
table with `python tools/reference_sound_catalog.py /path/to/SpideyPC.exe`.
The extraction also checks the independently retained player table. Group
selection preserves the demo and chapter-four exceptions; unknown selectors
fail explicitly. The retired DirectX/KAT upload path is not used.

`PcmWaveSample` reads bounded PCM format/data chunks, skips optional metadata,
and preserves mono/stereo 8-bit or 16-bit samples and their rate. It reproduces
the original loader's four-byte zero-filled buffer padding. Reading these chunks
directly removes the short-read warnings that Godot's generic WAV importer
produced for these files. Native coverage decodes all 599 distinct catalog WAVs.

`SpatialSound` retains the original approximate distance, near/far gates and Q12
stereo attenuation. The PC executable's `gte_rtir` at `0x46DA40` writes the
rotated vector to `0x610BA0`, while `gsub_46D9B0` reads `0x610BE0`. The spatial
helper at `0x4E6D90` calls both and therefore pans using the unchanged world-axis
vector. This source behavior is explicit in the modern core; 324 compiled-source
vectors include varied view matrices and boundary distances. Regenerate with
`python tools/reference_spatial_sound_vectors.py`.

`SoundChannelController` owns the 31 available SFX voices, source bit-mask
handles, cursor-based reclamation followed by first-free allocation, exhaustion,
looping, alias resolution, stored positional falloff, stop-mask early return,
master attenuation, mono mixing and converted pitch bytes. Sixteen sequences /
1,600 operations match compiled play, allocation, stop and positional-update
routines, including completed voices and reused channels. Regenerate with
`python tools/reference_sound_channel_vectors.py`. Channel zero remains reserved.
The source's initial play path ignores the volume modifier and pitch-offset
argument; the modern initial path retains that behavior.

`PlayerFrameSounds` ports the nine built-in player trigger lists and
`ProcessSFXArray`: at most one frame event advances per source update, the
sentinel clears the script on the following update, walking selects a
nonrepeated random variant, and crawl/landing clips retain their original
sound IDs and fixed volume. Fifty-four sequences / 1,080 updates match the
compiled recovered function with deterministic random/sound callbacks;
regenerate with `python tools/reference_player_sound_vectors.py`.
`PlayerAnimationSoundDriver` selects/reset triggers for normal player animation
commands and preserves the existing cursor for raw scripted animation changes.
The current animation determines which sound the preserved cursor emits,
matching the source routine.

`GodotSoundChannels` splits PCM into synchronized left/right streams so native
playback can preserve the independent PC channel gains without altering stereo
sample content. It converts unsigned PCM8 to Godot's signed representation,
loops the full padded source buffer, applies the original pan attenuation and
pitch/frequency ratio, and releases voices on completion or explicit stop. The
frequency is truncated to the original integer before conversion to Godot's
playback ratio; 1,536 vectors match compiled `DXSOUND_SetPitch`. Regenerate with
`python tools/reference_sound_frequency_vectors.py`. The
default master setting remains `0x2FFF`; a zero converted pitch uses the original
sample frequency. The native player frame-event facade now shares this pool with
platform sounds, including level-specific aliases. Its rendered check observes
walking footsteps and verifies sample clock, stereo gains, pitch and cleanup.
The player alias-selection flag remains explicit and is not inferred from
unrelated collision flags. These frame events do not generate the separate story
wait signal.

Platform commands `0x4507/08/09/0B` now play, update and stop sounds through this
pool. The rendered check executes original actor 54's level alias 1 (`heli1`),
verifies the three-world-update position interval, native synchronized gains,
looping beyond the sample duration and explicit stop cleanup. Remaining work
includes other actor/event sound commands, full animation-linked sound data,
pause/resume and settings integration, Doppler updates, music and bank lifecycle.

Linux execution is verified locally; the desktop CI matrix has not
yet run on Windows or macOS hardware.

The event runtime implements pulse routing, initial pulse limits and pulse
conditions. Gameplay-specific instructions are dispatched to explicit command
handlers; unported handlers stop execution with a source diagnostic. It has
been connected to the Godot gameplay world. Native first-level startup and live
events pass, while the complete campaign still requires remaining command and
actor families. A partly executed program cannot be retried accidentally after
a handler failure. Cyclic event chains yield after a configurable frame budget.
Blocking scene commands now retain the current program cursor and conditional
state, so frame polling does not repeat side effects or execute later events
behind a movie.

`CinemaCatalog` retains the recovered 25 available PC movie tracks and all
21 story dispatches, including six comic covers. The two zero-sized SOFDEC/LEGAL
placeholders in the original 27-entry table are absent from the supplied archive.
`RunCinemaCommandHandler` skips the first autoexec cinema after a death restart
and otherwise holds the mission interpreter until the Godot cinema player
finishes. Unknown cinema IDs retain the recovered dispatch's empty branch.
The cinema adapter shows the cover for up to five seconds, advances it on input,
then plays the converted movie with embedded Vorbis audio. The original skip
counter counts playback polling iterations, including BinkWait, rather than
decoded frames; the Godot adapter uses sixty process polls before permitting a
movie skip. Its controls fit the prerecorded video's aspect into the current
viewport; 3D in-game cinematics and the main menu remain separate pending systems.

All 25 movies and six covers converted successfully outside the repository.
FFprobe comparison of original Bink files and converted Ogg files confirmed
matching image dimensions, frame rates, duration (within 0.1 seconds), audio
sample rates and channel counts for every movie. The story movies are 320×192
at 15 fps; the logo files have differing dimensions/rates. Runtime playback
currently uses the story catalog's display dimensions. Theora conversion is
lossy, so these checks do not prove decoded pixel or audio sample parity.
A second preparation pass verified every source CRC and cached output hash.
Native OpenGL checks played the first opening sequence, verified a decoded
texture and widescreen layout, rejected an early skip and resumed the mission
once after a permitted skip. Cinema 52 additionally reached its natural finished
signal. The capture is a real 1280×720 Godot viewport, with the native 5:3 video
centered at 1200×720. The cinema preview runs a standalone mission command;
full level autoexec/restart execution still requires the remaining handlers,
actor loading and input cleanup integration. A native pause check now runs the
actual mission cinema with gameplay physics paused, verifies physics resumes
after completion, and retains the same video/stream through live reparenting.
Actual destruction restores the pause state held before playback, including an
already paused application.

Pending: remaining visual effects and renderer parity, animation blending, sound events and LOD selection, dynamic collisions and physics parity,
player movement/combat/webs, camera/HUD/menu migration, gameplay audio conversion/playback,
the remaining mission command handlers, enemy and boss behavior, level-specific
events, save compatibility, launcher integration and desktop exports.

The story scene now executes decoded startup and live TRG programs. Stitched
character vertices are excluded from the static geometry path; both animated
formats use the new model adapter. The workbench is not the finished game.

## Required gameplay acceptance checks

The port retains the user's display requirements: gameplay and in-game
cutscenes use widescreen; the main menu stays 4:3. HUD groups must remain
assembled and proportionate while their anchors follow the screen edges.

Before the first playable checkpoint, verify E transitions beneath an
overhang and back to the ground without stretched or separated character
parts, bomb pickup at the end of the first level without a crash, and a vault
door that visibly animates open rather than disappearing after a delay.
These earlier fixes are reference behavior to carry into the new runtime,
not already validated Godot features.

## Final repository and delivery requirements

The finished project is to be branded **OpenSpidey**. After the entire rewrite
is buildable and playable, the repository must contain only the modern game,
its necessary development tools, tests and documentation. Retired engine code
and decompilation scaffolding must be removed from the delivered repository;
they remain reference material until the port and fidelity checks are complete.
Reference-dependent fixture generators must be retired or made independent
before that cleanup. Original proprietary assets stay outside the repository.

On 2026-10-13 the user additionally requested a development checkpoint committed
and pushed on a new branch, then cleared the automatic rewrite goal. That request
authorizes publishing this incomplete checkpoint; it does not establish full
rewrite completion. Development agents have stopped. The checkpoint document
records the precise verified state and remaining work.

The user authorized committing and pushing the finished modern-only project
after full completion, then shutting down the computer after the successful
push. These are final delivery steps, not authorization to publish an incomplete
rewrite. Keep sleep/idle inhibition active until the goal is finished; do not
shut down while implementation, verification, repository cleanup or publishing
remains outstanding. Verify completion, the final commit and the successful
remote push before executing the requested shutdown.

## Build and inspect

Run from the repository root with Python 3.12 or later:

```text
python tools/modern.py bootstrap
python tools/modern.py build
python tools/modern.py test
python tools/modern.py test --assets /path/to/original/game
python tools/modern.py smoke --assets /path/to/original/game
python tools/modern.py run --assets /path/to/original/game
python tools/modern.py play --assets /path/to/original/game
python tools/modern.py play-smoke --assets /path/to/original/game
python tools/modern.py editor --assets /path/to/original/game
python tools/modern.py media --assets /path/to/original/game
python tools/modern.py cinema --assets /path/to/original/game --cinema 11
python tools/modern.py cinema-smoke --rendered --assets /path/to/original/game
python tools/modern.py cinema-smoke --rendered --cinema 52 --finish-cinema --assets /path/to/original/game
```

Bootstrap installs verified official Godot/.NET archives into the user's private
toolchain folder. Existing tools can be selected with `OPENSPIDEY_GODOT` and
`OPENSPIDEY_DOTNET`. `OPENSPIDEY_TOOLCHAINS` overrides the installation folder.
The editor must be the **.NET-enabled** Godot edition.

`--scene data/L1A2_G.psx` selects another original geometry file. In the
inspection window, click to capture the mouse; WASD moves, Q/E descends/ascends,
Shift moves faster and Escape releases the mouse.

Use `--scene data/spidey.psx --animation 20` to inspect an original character
clip. `--scene data/henchman.psx` selects a matrix-animated model.
Left/Right selects neighboring clip IDs and Space pauses playback.
Use `--restart Re_Restart01` with the default first-level scene to inspect
Spider-Man at that restart position. `--animation` also selects the clip for
the placed model. The camera continues to use inspection controls.
`smoke --assets /path/to/original/game --scene data/spidey.psx` additionally
checks every clip/frame against Godot's pose nodes. Headless rendering has no
skin storage, so it cannot prove the uploaded matrices. Use
`smoke --rendered --all-animations --assets /path/to/original/game` with a display
to check actual rendering-server skin storage for every animated archive entry.
The preview's free camera
and clip controls are inspection tools, not the finished player controls.

`play` opens the separate developing level session. WASD, arrow keys or the left
stick move; click captures the mouse for orbit and Escape releases it. The
first-level mission is still incomplete until story and remaining actions are
connected. E zips along surface up. Space jumps, holds for ceiling attachment,
releases crawl support and cancels a pull. `play-smoke` runs the original actor and terrain adapter for 70 source
updates, then checks roof movement and playback before exiting. Add `--rendered`
to check rendering server skin storage with OpenGL, and `--capture /path/to/image.png`
to save a frame after rendering. `--scene` and `--restart` also select the level
and restart for these commands. The default main scene remains the inspection
workbench until the gameplay sequence is ready.

`smoke --physics-checks` exercises the native physics fixtures without game
assets. Add `--assets /path/to/original/game` to also check the selected level's
imported collision bodies after the physics space has synchronized.
With original assets, it also schedules crawl and zip actors. Use
`smoke --rendered --physics-checks --assets /path/to/original/game --capture-dir /path/to/captures`
to save the original overhang's firing, ceiling and floor frames after rendering.

`media` requires FFmpeg with the `libtheora` and `libvorbis` encoders. Select its
executable with `OPENSPIDEY_FFMPEG` if it is not on PATH. By default, converted
media is stored in `~/.local/share/openspidey/media`; use `--media-cache` to select
a different private folder. `cinema` prepares just the selected sequence;
`media` prepares the complete catalog. `cinema-smoke` additionally verifies
native playback and mission resumption, with `--finish-cinema` selecting natural
completion and `--capture` saving a rendered frame. Space, Enter, Escape and
gamepad A/Start advance or skip the sequence, subject to the movie's input guard.

Godot runtime video documentation: https://docs.godotengine.org/en/4.7/tutorials/io/runtime_file_loading_and_saving.html
FFmpeg encoder documentation: https://ffmpeg.org/ffmpeg-codecs.html#libtheora
Godot version documentation: https://docs.godotengine.org/en/4.7/
.NET release metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
