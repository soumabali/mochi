# 🐱 Mochi v2 — Sprite Regeneration Guide

> **Opsi 2**: Dhar gambar sendiri → Ame auto-process  
> Dhar buat sprite di Windows 11, kirim ke server, Ame proses otomatis.

---

## 📊 Current State Audit

### Bundle Size
| Metric | Current | Target |
|--------|---------|--------|
| Total PNGs | ~5,000+ | ~200-260 |
| Bundle size | 75 MB | < 1 MB |
| Frames per animation | 240 (48fps video) | 8-12 frames |
| Width | 200px | 200px (same) |

### 22 Unique Sprite Folders Needed

| # | Folder Name | FSM States | Mode | Optimal Frames | Description |
|---|-------------|------------|------|----------------|-------------|
| 1 | `cat_blinking_left` | Idle, IdleLeft, Blink, BlinkLeft | holdFirstFrame/playOnce | 8 | Cat duduk menghadap kiri, blinking |
| 2 | `cat_blinking_right` | IdleRight, BlinkRight | holdFirstFrame/playOnce | 8 | Cat duduk menghadap kanan, blinking |
| 3 | `cat_walking_left` | WalkLeft | loop | 8-12 | Cat berjalan ke kiri |
| 4 | `cat_walking_right` | WalkRight | loop | 8-12 | Cat berjalan ke kanan |
| 5 | `cat_walking_forward` | WalkForward | loop | 8-12 | Cat berjalan menghadap viewer |
| 6 | `run_1` | RunVar1 | loop | 8-12 | Cat berlari variant 1 |
| 7 | `run_2` | RunVar2 | loop | 8-12 | Cat berlari variant 2 |
| 8 | `jump_1` | JumpVar1, FallVar1, ClimbUp | playOnce/playOnceReversed | 8-12 | Cat melompat variant 1 |
| 9 | `jump_2` | JumpVar2, FallVar2 | playOnce/playOnceReversed | 8-12 | Cat melompat variant 2 |
| 10 | `cat_sleepy_yawn` | Sleeping, WakeUp | playOnceThenHoldLast/playOnceReversed | 8-12 | Cat menguap lalu tidur |
| 11 | `cat_meowing_left` | MeowLeft | playOnce | 8-12 | Cat mengeong menghadap kiri |
| 12 | `cat_meowing_right` | MeowRight | playOnce | 8-12 | Cat mengeong menghadap kanan |
| 13 | `cat_scratching_left` | ScratchLeft | playOnce | 8-12 | Cat menggaris menghadap kiri |
| 14 | `cat_scratching_right` | ScratchRight | playOnce | 8-12 | Cat menggaris menghadap kanan |
| 15 | `cat_angry_scratch_paw` | Angry | loop | 8-12 | Cat marah, cakar terangkat |
| 16 | `cat_surpised` | Surprised | playOnce | 6-8 | Cat kaget (mata besar, telinga naik) |
| 17 | `cat_chasing_wand` | Playful | loop | 8-12 | Cat mengejar mainan tongkat |
| 18 | `begging_food` | HungryStandard, Eating | loop | 8-12 | Cat meminta makan, sedih |
| 19 | `cat_hungry_begging_food` | HungryCritical | loop | 8-12 | Cat sangat lapar, critical |
| 20 | `cat_stretching` | Stretching | playOnce | 4-6 | Cat meregang (AI-generated, sudah ada) |
| 21 | `cat_drinking` | Drinking | playOnce | 4-6 | Cat minum air |
| 22 | `cat_happy_hop` | HappyHop | playOnce | 4-6 | Cat lompat senang |

### Frame Count Recommendation
- **Loop animations** (walk, run, angry, playful, hungry): 8-12 frames → smooth loop
- **playOnce animations** (meow, scratch, surprised, jump): 6-10 frames → enough for action
- **Short actions** (stretching, drinking, happy hop): 4-6 frames → brief movement
- **Idle/blink**: 8 frames → eyes open → half → closed → half → open

---

## 🎨 Global Style Guide (MASTER PROMPT)

### Character Spec
```
Subject: A cute domestic shorthair cat, bicolor white & ginger
Art style: 2D digital illustration, semi-realistic anime style
Coat: Creamy white base with warm ginger/orange patches
  - Ginger patch on top of head and ears
  - Irregular orange patch on lower back/flank
  - White muzzle, chest, belly, paws
  - Tail: white base with orange tabby stripes, orange tip
Eyes: Large, almond-shaped, amber/golden-yellow with black vertical pupil
  - White reflection highlight in eyes
Nose: Small triangular pink
Inner ears: Soft pink
Whiskers: Thin white
Outline: Consistent medium-thick dark brown outline (NOT pure black)
Shading: Soft cel-shading, light source from upper-left
  - Shadows under chin, belly, behind legs, under tail
Background: Transparent (alpha channel) — NO background
Dimensions: 200px wide, height varies per pose (112px-437px)
  - Keep cat centered horizontally
  - Bottom of paws at bottom edge
Format: 8-bit PNG with transparency
```

### Color Palette (hex)
| Element | Color |
|--------|-------|
| White fur base | `#F8F4ED` (creamy white, not pure white) |
| Ginger/orange fur | `#E89B3D` (warm ginger) |
| Dark orange stripes | `#C77A28` |
| Eye color | `#F2B83C` (amber/gold) |
| Pupil | `#1A1A1A` (near black) |
| Nose / inner ear | `#E8A0A0` (soft pink) |
| Outline | `#3B2A1A` (dark brown) |
| Shadow | `#D4C5B0` (soft grey-brown) |

---

## 📝 Per-Sprite Drawing Instructions

### Format untuk setiap sprite:
- **Nama folder**: target folder name
- **Jumlah frames**: recommended count
- **Facing**: left / right / forward
- **Pose**: body position description
- **Action**: what happens across frames
- **Key poses**: frame-by-frame breakdown

---

### 1. cat_blinking_left (8 frames)
- **Facing**: Left (profile view, cat faces left)
- **Pose**: Sitting upright, tail curled
- **Action**: Idle blink — eyes open → close → open
- **Frames**:
  1. Eyes fully open, relaxed
  2. Eyes 75% open
  3. Eyes 50% open (half-closed)
  4. Eyes fully closed (just a line)
  5. Eyes 50% open
  6. Eyes 75% open
  7. Eyes fully open
  8. Same as frame 1 (loop point)

### 2. cat_blinking_right (8 frames)
- Same as above but **mirrored** — cat faces right
- Same frame sequence

### 3. cat_walking_left (8-12 frames)
- **Facing**: Left
- **Pose**: Standing on all fours, side profile
- **Action**: Walking cycle — legs move, body bobs slightly
- **Frames** (8 frame cycle):
  1. Front legs together, back legs apart (contact)
  2. Front legs separating, back legs closing (passing)
  3. Front legs apart, back legs together (contact)
  4. Front legs closing, back legs apart (passing)
  5-8. Repeat with slight body bob variation
- **Key**: Tail should sway gently, head stays level

### 4. cat_walking_right (8-12 frames)
- **Mirrored** version of walking_left

### 5. cat_walking_forward (8-12 frames)
- **Facing**: Forward (3/4 view or front view)
- **Pose**: Standing on all fours, walking toward viewer
- **Action**: Walking cycle from front perspective
- **Key**: Front paws alternate, body slightly bobs, tail visible behind

### 6. run_1 (8-12 frames)
- **Facing**: Left
- **Pose**: Running pose — body stretched, legs extended
- **Action**: Fast run cycle — more dynamic than walk
- **Key**: Body lower to ground, legs fully extended, tail streaming behind, ears flat back

### 7. run_2 (8-12 frames)
- **Facing**: Left
- **Pose**: Alternative run style — more upright gallop
- **Action**: Gallop cycle — body bobs more, legs tuck under
- **Key**: Different silhouette from run_1 for variety

### 8. jump_1 (8-12 frames)
- **Facing**: Left or forward
- **Action**: Crouch → leap → apex → descend → land
- **Frames**:
  1. Standing normal
  2. Crouching low, back legs bent
  3. Pushing off, body extending upward
  4. Mid-air, body fully extended, legs spread
  5. Apex of jump, legs tucked
  6. Descending, legs reaching down
  7. Landing, legs absorbing impact
  8. Recovering to standing
- **Note**: Used reversed for Fall animation too

### 9. jump_2 (8-12 frames)
- Alternative jump style — higher arc, more elegant
- Same frame structure as jump_1 but different pose style

### 10. cat_sleepy_yawn (8-12 frames)
- **Facing**: Left or forward
- **Action**: Yawn → eyes droop → head lowers → asleep
- **Frames**:
  1. Sitting, eyes half open, tired
  2. Mouth opening wide (yawn beginning)
  3. Full yawn — mouth wide open, eyes closed
  4. Mouth closing, eyes still closed
  5. Mouth closed, head lowering
  6. Head almost down, eyes closed
  7. Head down on paws, sleeping
  8. Fully asleep (hold last frame)
- **Note**: Used reversed for WakeUp animation

### 11. cat_meowing_left (8-12 frames)
- **Facing**: Left
- **Action**: Mouth opens to meow, holds, closes
- **Frames**:
  1. Mouth closed, looking left
  2. Mouth slightly open
  3. Mouth open wider (meowing)
  4. Mouth fully open (peak meow)
  5. Mouth still open
  6. Mouth closing
  7. Mouth almost closed
  8. Mouth closed, return to neutral

### 12. cat_meowing_right (8-12 frames)
- **Mirrored** version of meowing_left

### 13. cat_scratching_left (8-12 frames)
- **Facing**: Left
- **Action**: Rear leg comes up to scratch ear/neck
- **Frames**:
  1. Sitting normal
  2. Hind leg lifting
  3. Leg reaching toward ear
  4. Scratching ear (leg at ear)
  5. Scratching motion (leg rapidly moving)
  6-7. Continue scratching
  8. Leg lowering back down
  9. Return to sitting

### 14. cat_scratching_right (8-12 frames)
- **Mirrored** version of scratching_left

### 15. cat_angry_scratch_paw (8-12 frames)
- **Facing**: Forward or 3/4 view
- **Action**: Angry — paw raised, scratching motion, ears flat
- **Frames**:
  1. Standing, ears flat back, eyes narrowed
  2. Front paw lifting, claws out
  3. Paw raised high, ready to strike
  4. Paw coming down (scratch)
  5. Paw at bottom of scratch
  6. Paw lifting again
  7-8. Repeat scratch motion (loop)

### 16. cat_surpised (6-8 frames)
- **Facing**: Forward
- **Action**: Sudden surprise — ears perk up, eyes go wide, body startles
- **Frames**:
  1. Relaxed standing
  2. Slight startle beginning
  3. Ears shooting up, eyes widening
  4. Full surprise — eyes huge, ears straight up, body slightly back
  5. Holding surprised pose
  6. Starting to relax
  7. Almost back to normal
- **Key**: Exaggerated eyes, erect ears, maybe a "!" feel

### 17. cat_chasing_wand (8-12 frames)
- **Facing**: Left or forward
- **Action**: Playful — batting at a wand toy, pouncing
- **Frames**:
  1. Crouching, eyes on target (wand)
  2. Butt wiggle (preparing to pounce)
  3. Still wiggling
  4. Launch — front paws reaching out
  5. Mid-pounce, paws extended
  6. Batting at wand
  7. Paw swiping
  8. Recovering to crouch (loop)

### 18. begging_food (8-12 frames)
- **Facing**: Forward
- **Action**: Sitting, begging for food — sad eyes, paw raised
- **Frames**:
  1. Sitting, looking up with sad eyes
  2. One front paw slightly raised
  3. Paw up higher, pleading
  4. Both paws up (begging pose)
  5. Holding begging pose
  6. Paw wave
  7. Still begging
  8. Slight head tilt (loop)

### 19. cat_hungry_begging_food (8-12 frames)
- **Facing**: Forward
- **Action**: More desperate version of begging — more urgent
- **Frames**:
  1. Sitting, very sad eyes, ears slightly drooped
  2. Paw up urgently
  3. Both paws up, more desperate
  4. Paw waving frantically
  5. Leaning forward, begging harder
  6. Mouth slightly open (meow-cry)
  7. Paws still up, urgent
  8. Back to sitting (loop)
- **Key**: More distress than standard begging — drooped ears, wider eyes

### 20. cat_stretching (4-6 frames)
- **Facing**: Left
- **Action**: Stretch after waking — arch back, extend legs
- **Frames**:
  1. Standing, body low
  2. Front legs extending forward, back arching up
  3. Full stretch — back arched high, front extended
  4. Returning to normal stance
  5. Standing relaxed
- **Note**: Already has 4 frames. Can keep or expand to 6.

### 21. cat_drinking (4-6 frames)
- **Facing**: Down/forward
- **Action**: Drinking from water bowl — head down, lapping
- **Frames**:
  1. Standing near bowl, head level
  2. Head lowering toward bowl
  3. Tongue lapping water
  4. Head slightly up, swallowing
  5. Head back down for more
- **Key**: Tongue should be visible pink, bowl optional

### 22. cat_happy_hop (4-6 frames)
- **Facing**: Forward
- **Action**: Joyful little hop/bounce
- **Frames**:
  1. Standing, happy expression
  2. Crouching slightly (preparing hop)
  3. Up in the air, paws off ground, happy
  4. Landing, paws touching ground
  5. Bouncing back up (loop feel)
- **Key**: Joyful expression, tail up and wagging

---

## 🎬 How to Create Sprites on Windows 11

### Method A: Draw Each Frame Manually (Recommended)
1. Use **Krita** (free) or **Aseprite** (paid, best for pixel/sprite art)
2. Set canvas: 200px × (height varies, see table above)
3. Draw each frame on a separate layer
4. Export each layer/frame as separate PNG
5. Name files: `frame_001.png`, `frame_002.png`, etc.
6. Keep background transparent

### Method B: Video → Frames
1. Record 5-second video of cat animation (any source)
2. Use ffmpeg to extract frames:
   ```
   ffmpeg -i input.mp4 -vf "fps=8,scale=200:-1" frame_%03d.png
   ```
   (8fps = 8 frames per second, scale to 200px wide)
3. Pick the best 8-12 frames that show the full motion cycle
4. Clean up frames (remove background, adjust colors)

### Method C: AI Generation + Manual Cleanup
1. Generate base frames with AI (Stable Diffusion, etc.)
2. Use prompt template below
3. Manually clean up and ensure consistency
4. This is what we did for `cat_stretching`

### AI Prompt Template (if using AI)
```
[MASTER STYLE PROMPT from above]

Specific pose: [describe pose for this sprite]
Facing: [left/right/forward]
Frame number: X of N

[Color palette and details from guide]

Transparent background, 8-bit PNG, 200px wide, centered.
```

---

## 🔄 Auto-Process Pipeline (Ame's Job)

When Dhar sends sprite frames, Ame will:

### Step 1: Receive & Organize
```bash
# Dhar sends files to: /home/ubuntu/projects/mochi-v2/02-application/Assets/Sprite_raw/<folder_name>/
# Example: Sprite_raw/cat_blinking_left/frame_001.png ... frame_008.png
```

### Step 2: Auto-Process Script
```bash
# Ame runs: process_sprites.py
# For each folder in Sprite_raw/:
#   1. Remove white/solid background → transparent
#   2. Resize to 200px wide (maintain aspect ratio)
#   3. Center horizontally on transparent canvas
#   4. Optimize PNG (pngquant lossless compression)
#   5. Output to Sprite_optimized/<folder_name>/
#   6. Create animated GIF preview
#   7. Update manifest.json if new entry needed
```

### Step 3: Verify
```bash
# Check output:
# - All PNGs are 200px wide
# - Transparent backgrounds
# - Frame count matches expectation
# - Animated GIF preview looks correct
# - Build passes: dotnet build
# - Tests pass: dotnet test
```

### Step 4: Commit & Push
```bash
git add . && git commit -m "sprites: regenerate <folder_name> (N frames)"
git push origin main
```

---

## 📦 What Dhar Needs to Send

For each sprite folder, send:
1. **PNG files** named `frame_001.png`, `frame_002.png`, ... `frame_NNN.png`
2. All frames **same dimensions** (200px wide preferred, Ame can resize)
3. **White or solid background** (Ame will make transparent) — OR already transparent
4. **Ordered frames** — frame_001 = start of animation, frame_NNN = end
5. Send via: upload to server, or share ZIP/folder

### Folder Naming Convention
```
Sprite_raw/
├── cat_blinking_left/
│   ├── frame_001.png
│   ├── frame_002.png
│   └── ...
├── cat_walking_left/
│   ├── frame_001.png
│   └── ...
└── ...
```

---

## ✅ Priority Order

| Priority | Sprite | Why | Frames |
|----------|--------|-----|--------|
| 🔴 P0 | cat_blinking_left + right | Core idle — most visible | 8 each |
| 🔴 P0 | cat_walking_left + right | Core movement | 10 each |
| 🟡 P1 | cat_meowing_left + right | Common interaction | 8 each |
| 🟡 P1 | cat_sleepy_yawn | Sleep cycle | 10 |
| 🟡 P1 | cat_scratching_left + right | Common action | 8 each |
| 🟢 P2 | run_1 + run_2 | Movement variety | 8 each |
| 🟢 P2 | jump_1 + jump_2 | Jump action | 8 each |
| 🟢 P2 | cat_angry_scratch_paw | Drag reaction | 8 |
| 🟢 P2 | cat_surpised | Cursor reaction | 6 |
| ⚪ P3 | cat_chasing_wand | Playful | 10 |
| ⚪ P3 | begging_food + cat_hungry_begging_food | Hunger system | 10 each |
| ⚪ P3 | cat_walking_forward | Walk to viewer | 10 |
| ⚪ P4 | cat_stretching | Already done (4 frames) | 4-6 |
| ⚪ P4 | cat_drinking | Already has 1 frame | 4-6 |
| ⚪ P4 | cat_happy_hop | Already has 1 frame | 4-6 |

**Total work**: ~22 folders, ~200 PNG frames needed  
**Estimated time**: 2-4 hours drawing (depending on skill) or 1-2 hours with AI assistance

---

## 📊 Expected Result

| Metric | Before | After |
|--------|--------|-------|
| Total PNGs | ~5,000+ | ~200 |
| Bundle size | 75 MB | ~500 KB - 1 MB |
| Animations | 22 folders | 22 folders |
| Smoothness | 48fps (overkill) | 8-12fps (perfect for sprites) |
| Quality | Video-converted (lossy) | Hand-drawn (crisp) |

---

*Guide ini adalah living document. Update kalau ada perubahan.*
*Last updated: July 13, 2026*