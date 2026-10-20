# Sounds for Semblance

Drop the sounds you find into this folder. Once they're here, I'll trim them, level them, loop them and swap them in for the generated placeholders. Any slot you leave empty keeps its placeholder, so you can fill this in a few at a time.

## How to name and save the files

- **Name each file exactly as in the File column**, for example `door_creak_1.wav`. Capitals and spaces won't match.
- **WAV, OGG or MP3** all work. Unity can't read FLAC, so convert those first.
- **Any sample rate, mono or stereo.** Anything heard at a spot in the world is folded to mono. Music and ambience loops can stay stereo.
- **Loops** (Loop column: yes) can be any length from 10 seconds to 2 minutes. You don't need a clean loop point; I'll crossfade the seam.
- **Variants** (`_1`, `_2`, ...): if you only find one take, save it as `_1` and I'll make the others by shifting pitch and tone.
- **Extra length is fine.** I trim the silence and the tails.

## Licences

Only use sounds you're allowed to ship in a commercial game, and **write one line in `CREDITS.txt` for every file**: the file name, where it came from (the URL), the author and the licence. CC-BY requires that credit, and it saves you the work of tracking it down later.

- **Safe:** CC0 / public domain (Freesound with the CC0 filter, OpenGameArt CC0, Kenney, Sonniss GDC bundles), and the Pixabay licence.
- **Fine with credit:** CC-BY. The credit goes in `CREDITS.txt`, and I'll put it in the game's credits.
- **Avoid:** CC-BY-NC or "non-commercial" (this includes the BBC Sound Effects archive), CC-BY-ND, and anything ripped from a film or game.

---

## The list

### Footsteps (the detective's and the residents')

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `footstep_grass_1` … `_4` | One step on short grass | Yards, the churchyard, verges | no | footstep grass single |
| `footstep_dirt_1` … `_4` | One step on packed dirt or gravel | Farm tracks, the road shoulder; the thing walking behind you | no | footstep gravel dirt single |
| `footstep_asphalt_1` … `_4` | One step on asphalt, hard soles | The highway, Main Street; currently also the basement | no | footstep concrete asphalt shoe |
| `footstep_wood_1` … `_4` | One step on old floorboards, slightly hollow | Every interior, porches | no | footstep wooden floor creak |
| `footstep_corn_1` … `_3` | Pushing through dry corn stalks | The cornfields | no | corn rustle walking through crops |

### The detective's things

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `flashlight_click` | An old torch's slide switch | Flashlight on and off; the batteries dying | no | flashlight switch click |
| `camera_raise` | A small mechanical click, camera brought up | Raising the photo camera | no | camera handling click |
| `camera_lower` | A lower, softer click | Lowering the camera | no | camera handling |
| `camera_shutter` | A film camera's shutter and film advance (1970s/80s SLR) | Taking a photo | no | SLR shutter film advance |
| `camera_empty` | A dry click with nothing behind it | Shooting with no film left | no | camera dry fire click |
| `page_flip` | One notebook page turned | Opening the notebook, turning pages | no | notebook page turn |
| `typewriter_key` | One typewriter keystroke | The typed title lines | no | typewriter single key |
| `register` | An old cash register drawer and bell | Buying film at the store | no | vintage cash register |
| `coffee_pour` | Coffee poured from a pot into a ceramic mug | Mae's coffee at the diner | no | pouring coffee mug |
| `heartbeat` | One heartbeat (lub-dub), close and low | Fear and the chase, repeated faster as it gets worse | no | heartbeat single low |

### Doors, objects and the building

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `door_creak_1` | A wooden door opening, a long creak | Opening doors; climbing into a wardrobe | no | wooden door creak open |
| `door_creak_2` | A different, shorter creak | Opening doors; climbing into a wardrobe | no | door creak short |
| `door_close` | A wooden door shutting, latch clicks | Closing doors; a locked door's rattle (pitched up) | no | wooden door close latch |
| `knock` | Three firm knocks on a wooden door | Knocking; something knocking at night | no | knock wooden door three |
| `knock_slow` | Three slow, heavy knocks | Night events; pipes knocking in the basement | no | slow heavy knocking |
| `hatch_open` | A cellar hatch heaved up and dropped open | Going down into cellars | no | cellar door trapdoor open |
| `car_door` | A 1970s car door shutting | Getting in and out of the car | no | old car door close |
| `engine_loop` | A big old V8 idling and running | Driving the car | yes | old car engine idle loop |
| `locker_rattle` | A steel locker's door rattled by something outside | Hiding in the archive locker | no | metal locker rattle |
| `breaker_reset` | A big breaker lever thrown, a heavy clunk, then a hum starting | Resetting the breaker in the boiler room | no | circuit breaker switch clunk |
| `power_down` | Fluorescent tubes and electrics cutting out, a falling hum | The lights going out in the tutorial | no | power outage lights out |
| `lamp_die` | A bulb popping and fizzing out | Lamps dying at night; the car stalling (for now) | no | light bulb pop burn out |
| `thud` | A heavy, dull thump, a box falling on concrete | Things falling in the dark | no | heavy thud box drop |
| `bell_toll` | One toll of a big church bell, with a long ring-out | Ringing the bell; the 3 AM bell; the ending | no | church bell single toll |

### Ambience beds

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `wind_loop` | Wind across open fields, steady, no gusts that pop | Outside, all the time | yes | wind field ambience loop |
| `crickets_loop` | Crickets at night in a field | Outside at night | yes | crickets night ambience |
| `rain_loop` | Steady rain, heard through a window or on a roof | The tutorial's rain | yes | rain on window loop |
| `room_tone_loop` | The quiet of an old room: faint air, nothing else | Indoors everywhere; the archive | yes | room tone quiet interior |
| `lamp_buzz_loop` | A fluorescent tube's hum and buzz | Under ceiling lights | yes | fluorescent light hum |
| `static_loop` | Radio static, soft hiss | The radio in the intro, TVs, the dispatch radio | yes | radio static hiss |
| `clock_tick_loop` | A wall clock ticking | The office clock in the tutorial | yes | wall clock ticking |
| `drip_loop` | Slow water drips in a concrete space | The basement and cellars | yes | water drip cave concrete |
| `drone_loop` | A low, uneasy tonal drone | Tension under the night | yes | dark ambient drone horror |
| `drone_deep_loop` | A deeper, heavier drone, almost felt | The basement, cellars, the intro drive | yes | deep sub drone horror |

### Animals

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `crow_1` | One crow caw | Over the fields; where the body is | no | crow caw single |
| `crow_2` | A different crow call, two caws | Over the fields | no | crow call |
| `owl_1` | A barn owl or great horned owl at night | Night, in the distance | no | owl hoot night |
| `dog_bark` | One happy bark from an old dog | Biscuit, when the lights come back | no | dog bark single friendly |
| `dog_pant_loop` | A dog panting | Biscuit trotting after you | yes | dog panting loop |
| `claws_loop` | Dog claws clicking on concrete, trotting | Biscuit in the dark | yes | dog claws walking hard floor |
| `growl` | A low dog growl, two or three seconds | Biscuit by the locker and on the chase | no | dog growl low |

### People

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `voice_blip_1` … `_5` | Very short muttered syllables, no real words | Under every line of dialogue, one per word or two | no | gibberish voice syllable mumble |
| `hum_hymn` | Someone humming a slow hymn, a few bars | Residents humming to themselves | no | humming hymn old |
| `knuckle_crack` | Knuckles cracking | A resident's nervous habit | no | knuckle crack |
| `whisper_loop` | Several whispers, no words you can make out | Night events, the lamplit whispers | yes | whispering voices creepy |
| `name_whisper` | One breathy whisper, like a name said behind you; keep it unintelligible, since the detective could be anyone | A night event | no | whisper single breathy |

### The thing in town

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `creature_step_1` … `_3` | A heavy, wrong footstep: weight, a little wet | The hunt | no | monster footstep heavy |
| `creature_shriek` | A distant, inhuman shriek | Before a hunt; when it spots you | no | creature scream distant |
| `breathing` | Ragged, wet breathing, close | The hunter's breath as it gets near | yes | monster breathing heavy |

### Music and stingers

| File | What it is | Where you hear it | Loop | Search terms |
|---|---|---|---|---|
| `music_theme` | A slow, sad, slightly wrong piece (music box, solo piano or detuned organ), 1–2 minutes | The title screen | yes | music box sad loop |
| `jukebox_loop` | A 1950s–60s song or instrumental, heard through a jukebox; it must be public domain or CC0 | The diner | yes | 1950s instrumental public domain |
| `chase_loop` | Driving, pulsing tension music, low drums | The hunt; the tutorial chase | yes | horror chase music loop |
| `sting_soft` | A short, quiet "something's wrong" sting, 1–2 seconds | Noticing a tell; small scares | no | horror sting subtle |
| `stinger_1` | A sharp scare sting | Seeing something you shouldn't | no | horror jump scare sting |
| `stinger_2` | A bigger, lower sting | The hunt starting | no | horror stinger impact |
| `stinger_3` | A dread sting, slower swell | Bad news: the lights going out, someone missing | no | horror swell stinger |
| `stinger_death` | A violent, final hit | Being caught | no | horror death sting |

---

## Worth adding

The game doesn't have slots for these yet; it reuses other sounds where they'd play. If you find them, I'll wire them in.

| File | What it is | What it replaces |
|---|---|---|
| `door_metal_open`, `door_metal_close` | A steel fire door: push bar clunk, heavy swing, closer hiss and latch | The wooden creak on the basement and stairwell doors |
| `locker_open`, `locker_close` | A steel locker door opened and shut | The wardrobe creak when you hide in the locker |
| `door_locked` | A handle tried against a lock, a short rattle | The pitched-up door close |
| `footstep_concrete_1` … `_4` | One step on bare concrete, a little echo | The asphalt steps in the stairwell, basement and cellars |
| `car_start`, `car_stall` | An old engine cranking and catching; sputtering and dying | Nothing on start; the bulb pop when the car dies |
| `thunder_1`, `thunder_2` | Distant thunder rolls | Nothing yet: the tutorial's storm is rain only |
| `radio_chatter_loop` | Police radio: indistinct voices through static | The plain static on the dispatch radio |
