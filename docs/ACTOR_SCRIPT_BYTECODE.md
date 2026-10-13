# Scripted actors

Black Cat and the Spider-Man clone use `ActorScriptController`. Their exported
`SynthesizeAnalogueInput` methods construct a borrowed state view and select a
behavior profile. The controller adds no members or virtual methods to either
original actor class. Player scripts use a separate controller because their
input publication and command semantics differ.

| Policy | Black Cat | Spider-Man clone |
| --- | --- | --- |
| Walking speed | 3 | 8 |
| Walking start / loop / arrival animation | 2 / 3 / 4 | 1 / 21 / 13 |
| Move-to-target velocity | Normalized direction multiplied by command speed | Normalized direction |
| Animation signal | Body flags bit 0 | Input flags bit 0 |
| Opcode 16 | Store a checkpoint node | Original unsupported-opcode behavior |
| Global skip flag | Clear commands, stop velocity, restore checkpoint | Keep processing |

The input signal sources are retained exactly as recovered, including Black
Cat's use of the body-flags byte. They must not be merged into one generic flag.
The clone's original field at offset 0x328 is gravity acceleration; it is never
passed as a checkpoint. A null checkpoint pointer makes the distinction explicit.

Records contain a signed 16-bit due time, an opcode and its parameters. A due
time of -1 runs immediately. Executing an instruction resets elapsed time.
Instructions 1, 2, 4, 5, 6, 8, 15 and 255 teleport, walk, play voice audio, move,
hold animation, look at a node, wait for an animation signal and stop decoding.
Only Black Cat additionally handles checkpoint instruction 16. Walking cancels
all animation-holding commands before enqueuing its target.

The shared command list owns variable-length records through the actor's original
head and an injected allocator. Position commands suspend decoding until arrival;
completion resumes decoding on the following update. Animation and look-at timers
are active at remaining time zero and expire below zero. Look-at commands set
actor flag 4 and keep the look target populated until expiration. Walking arrival
clears linear/angular velocity and angular acceleration before playing the
profile's arrival animation.

The checkpoint skip path retains its original two position restores: preparation
also aims toward the first linked node at the actor's current height, and final
completion restores the checkpoint again. The clone retains its original lack
of that skip handling. Unsupported instructions consume no guessed parameters.

The real-actor fixture loads model/animation data, creates both actors through
their original factories, advances scripts and removes them through their actual
module cleanup functions. It checks delayed execution, concurrent timers,
signal selection, checkpoint completion/restoration, movement scaling, walking
animations and speed, arrival reset, and restoration of the prior actor list.
It does not run complete missions or validate the actors' appearance and physics.
