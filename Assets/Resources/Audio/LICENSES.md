# Audio licenses

Recorded sounds downloaded from OpenGameArt.org, all **CC0 1.0 (public domain dedication)**,
https://creativecommons.org/publicdomain/zero/1.0/ . Each one was trimmed, converted to mono WAV
and loudness-normalized with ffmpeg; the originals are not in the project.

| File in this folder | Source | Author | License | Changes |
|---|---|---|---|---|
| AngelScreamRec.wav | https://opengameart.org/content/horror-scream1 (scream_horror1_0.mp3) | vinrax | CC0 | first 1.7 s, fade out |
| ChandelierCreak_0.wav, ChandelierCreak_1.wav | https://opengameart.org/content/tree-creaking (tree_creak_0.ogg) | antumdeluge | CC0 | two short segments, high-pass |
| Step_Wood_0..2.wav | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud ([kdd]DifferentSteps_0.zip: wood01–03.ogg) | tinyworlds | CC0 | normalized |
| Step_Carpet_0..2.wav | same as Step_Wood (wood01–03.ogg) | tinyworlds | CC0 | low-passed and quieter, to read as carpet |
| PaintingScream.wav (layered with synthesis) | https://opengameart.org/content/female-high-pitched-scream-sfx (female_scream_1.ogg) + https://opengameart.org/content/female-scream-1 (screams_0.ogg) | WuxiaScrub; Nocturnal_Vanguard | CC0 | segments layered with ±pitch-shifted copies, distortion, synthesized frame-crack transient and reverb (Tools/gen_chase_sfx.py) |
| Step_Stone_0..2.wav | same zip (stone01.ogg) | tinyworlds | CC0 | three pitch variants |

Synthesized in code (no third-party material):

| File | Generator |
|---|---|
| PhoneRing, LineHiss, PowerClunk, KeyClick, LockRattle, StoneScrape, AngelScream (.wav) | Tools/gen_sfx.py |
| MenuLullaby.wav (Brahms, Wiegenlied Op. 49 No. 4 — composition in the public domain) | Tools/gen_lullaby.py |
| MainDoorOpen, EndingStinger (.wav) — bad-ending cutscene | Tools/gen_ending_sfx.py |
| ChaseHeartbeat.wav — chase heartbeat loop, 60 BPM | Tools/gen_chase_sfx.py |
| CrowdMurmur.wav — crowd murmur, 24 s seamless loop（15 条合成人声 + 脚步 + 轻咳） | Tools/gen_ambient_sfx.py |
| AmbientRoomTone.wav — gallery room tone, 40 s seamless loop（空调低频 / 50 Hz 电流声 / 气流） | Tools/gen_ambient_sfx.py |
| AmbientRoomTone_PowerOut.wav — cold power-out room tone, 40 s seamless loop | Tools/gen_ambient_sfx.py |
| ToiletNoise.wav — 二楼厕所金属/瓷器撞击 + 闷响 + 水管咕噜，3.2 s | Tools/gen_ambient_sfx.py |
| PowerRoomDoorOpen.wav — 配电室铁门锁舌 + 门轴吱呀 + 限位闷响，2.2 s（纯合成） | Tools/gen_power_room_door.py |

BGM_Museum.mp3: see BGM_Museum.LICENSE.txt.
