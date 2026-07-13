#!/usr/bin/env python3
"""
Mochi v2 — Video to Sprite Pipeline
=====================================
Processes raw video files into optimized transparent sprite frames.

Pipeline per video:
  1. Extract frames at sampled intervals (24 frames from 240)
  2. Remove background with rembg (AI-based, handles any bg)
  3. Auto-crop transparent borders
  4. Resize to 200px wide (maintain aspect ratio)
  5. Quantize to 256 colors (smaller files)
  6. Save as frame_001.png ... frame_024.png
  7. For left-facing videos: also create flipped right-facing variant

Usage:
  python3 video_to_sprites.py                    # Process all videos
  python3 video_to_sprites.py <video_name>       # Process specific video
  python3 video_to_sprites.py --frames 12       # Use 12 frames instead of 24
"""

import os
import sys
import subprocess
import tempfile
import shutil
from pathlib import Path
from PIL import Image
import io

# ─── Configuration ───
VIDEO_DIR = Path("/home/ubuntu/workspace/mochi_v2_assets_new/video")
OUTPUT_DIR = Path("/home/ubuntu/projects/mochi-v2/02-application/Assets/Sprite_optimized")
PREVIEW_DIR = Path("/home/ubuntu/projects/mochi-v2/02-application/Assets/Sprite_preview")
AUDIO_DIR = Path("/home/ubuntu/projects/mochi-v2/02-application/Assets/Audio")

TARGET_WIDTH = 200
DEFAULT_FRAMES = 24  # Sample 24 frames from 240 (every 10th frame)

# Video → manifest folder mapping
VIDEO_MAP = {
    "begging_food": "begging_food",
    "cat_angry_scratch_paw": "cat_angry_scratch_paw",
    "cat_blinking_left": "cat_blinking_left",
    "cat_drinking": "cat_drinking",
    "cat_happy_hop": "cat_happy_hop",
    "cat_running_left": "run_1",
    "cat_scratching_left": "cat_scratching_left",
    "cat_sleepy_yawn": "cat_sleepy_yawn",
    "cat_stretching_left": "cat_stretching",
    "cat_surpised": "cat_surpised",
    "cat_walking_forward_right": "cat_walking_forward",
    "cat_walking_left": "cat_walking_left",
    "jump_left": "jump_1",
}

# Videos to flip for right-facing variants
FLIP_MAP = {
    "cat_blinking_left": "cat_blinking_right",
    "cat_scratching_left": "cat_scratching_right",
    "cat_walking_left": "cat_walking_right",
    "run_1": "run_2",
    "jump_1": "jump_2",
}


def extract_frames(video_path: Path, output_dir: Path, num_frames: int) -> list[Path]:
    """Extract frames from video at evenly spaced intervals."""
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Get total frames
    result = subprocess.run(
        ["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
         "-show_entries", "stream=nb_read_frames", "-of", "csv=p=0", str(video_path)],
        capture_output=True, text=True
    )
    total_frames = int(result.stdout.strip()) if result.stdout.strip().isdigit() else 240
    
    # Calculate frame indices to extract (evenly spaced)
    indices = [int(i * total_frames / num_frames) for i in range(num_frames)]
    
    extracted = []
    for i, frame_idx in enumerate(indices):
        out_path = output_dir / f"raw_{i:03d}.png"
        subprocess.run(
            ["ffmpeg", "-v", "error", "-i", str(video_path),
             "-vf", f"select=eq(n\\,{frame_idx})", "-vframes", "1",
             "-y", str(out_path)],
            capture_output=True
        )
        if out_path.exists():
            extracted.append(out_path)
    
    return extracted


def remove_bg(img: Image.Image) -> Image.Image:
    """Remove background using rembg."""
    from rembg import remove
    output = remove(img)
    if output.mode != 'RGBA':
        output = output.convert('RGBA')
    
    # Auto-crop transparent borders
    bbox = output.getbbox()
    if bbox:
        output = output.crop(bbox)
    
    return output


def resize_to_width(img: Image.Image, target_width: int = TARGET_WIDTH) -> Image.Image:
    """Resize to target width, maintain aspect ratio."""
    if img.width == target_width:
        return img
    ratio = target_width / img.width
    new_height = max(1, int(img.height * ratio))
    return img.resize((target_width, new_height), Image.Resampling.LANCZOS)


def quantize_png(img: Image.Image) -> Image.Image:
    """Quantize to 256 colors for smaller file size, preserving alpha."""
    if img.mode != 'RGBA':
        img = img.convert('RGBA')
    
    r, g, b, a = img.split()
    rgb = Image.merge('RGB', (r, g, b))
    rgb = rgb.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    rgb = rgb.convert('RGB')
    img = Image.merge('RGBA', (*rgb.split(), a))
    return img


def create_gif_preview(frames: list[Image.Image], output_path: Path, duration_ms: int = 80):
    """Create animated GIF from processed frames."""
    if not frames:
        return
    
    max_w = max(f.width for f in frames)
    max_h = max(f.height for f in frames)
    
    normalized = []
    for frame in frames:
        canvas = Image.new('RGBA', (max_w, max_h), (0, 0, 0, 0))
        x = (max_w - frame.width) // 2
        y = (max_h - frame.height) // 2
        canvas.paste(frame, (x, y), frame)
        normalized.append(canvas.convert('RGB'))
    
    normalized[0].save(
        output_path, save_all=True, append_images=normalized[1:],
        duration=duration_ms, loop=0, optimize=True
    )


def flip_horizontal(img: Image.Image) -> Image.Image:
    """Flip image horizontally for right-facing variant."""
    return img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)


def process_video(video_name: str, num_frames: int) -> dict:
    """Process a single video into sprite frames."""
    # Find the actual video file (handle spaces in filenames)
    video_path = None
    for f in VIDEO_DIR.iterdir():
        if f.stem.strip() == video_name or f.stem == video_name:
            video_path = f
            break
    
    if not video_path:
        return {'success': False, 'error': f'Video not found: {video_name}'}
    
    target_folder = VIDEO_MAP.get(video_name, video_name)
    output_path = OUTPUT_DIR / target_folder
    
    print(f"\n{'='*60}")
    print(f"  🎬 {video_name} → {target_folder}")
    print(f"  📁 {video_path.name}")
    print(f"{'='*60}")
    
    # Step 1: Extract frames
    with tempfile.TemporaryDirectory() as tmpdir:
        tmp = Path(tmpdir)
        print(f"  Extracting {num_frames} frames...")
        raw_frames = extract_frames(video_path, tmp, num_frames)
        print(f"  ✓ Extracted {len(raw_frames)} frames")
        
        if not raw_frames:
            return {'success': False, 'error': 'No frames extracted'}
        
        # Step 2: Process each frame
        output_path.mkdir(parents=True, exist_ok=True)
        
        # Clear old frames
        for old in output_path.glob('frame_*.png'):
            old.unlink()
        for old in output_path.glob('*.webp'):
            old.unlink()
        
        processed = []
        for i, raw_path in enumerate(raw_frames, 1):
            try:
                img = Image.open(raw_path)
                print(f"  Frame {i:03d}: {img.size[0]}x{img.size[1]}", end='')
                
                # Remove background
                img = remove_bg(img)
                print(f" → bg removed {img.size[0]}x{img.size[1]}", end='')
                
                # Resize to target width
                img = resize_to_width(img, TARGET_WIDTH)
                print(f" → resized {img.size[0]}x{img.size[1]}", end='')
                
                # Quantize
                img = quantize_png(img)
                
                # Save
                out_name = f"frame_{i:03d}.png"
                img.save(output_path / out_name, 'PNG', optimize=True)
                size_kb = (output_path / out_name).stat().st_size / 1024
                print(f" → {size_kb:.1f}KB ✓")
                
                processed.append(img)
            except Exception as e:
                print(f" ✗ ERROR: {e}")
    
    # Step 3: Create GIF preview
    if processed:
        PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
        gif_path = PREVIEW_DIR / f"{target_folder}.gif"
        create_gif_preview(processed, gif_path)
        print(f"  📦 GIF: {gif_path.name}")
    
    total_kb = sum((output_path / f).stat().st_size for f in os.listdir(output_path) if f.endswith('.png')) / 1024
    print(f"\n  ✅ {target_folder}: {len(processed)} frames, {total_kb:.1f} KB total")
    
    return {
        'success': True,
        'folder': target_folder,
        'frames': len(processed),
        'total_kb': round(total_kb, 1)
    }


def create_flip_variant(src_folder: str, dst_folder: str, num_frames: int) -> dict:
    """Create right-facing variant by flipping left-facing sprites."""
    src_path = OUTPUT_DIR / src_folder
    dst_path = OUTPUT_DIR / dst_folder
    
    if not src_path.exists():
        return {'success': False, 'error': f'Source not found: {src_folder}'}
    
    print(f"\n  🔄 Flipping {src_folder} → {dst_folder}")
    
    dst_path.mkdir(parents=True, exist_ok=True)
    for old in dst_path.glob('frame_*.png'):
        old.unlink()
    
    pngs = sorted(src_path.glob('frame_*.png'))
    processed = []
    for png in pngs:
        img = Image.open(png).convert('RGBA')
        img = flip_horizontal(img)
        out_name = png.name
        img.save(dst_path / out_name, 'PNG', optimize=True)
        processed.append(img)
    
    # GIF preview
    if processed:
        PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
        create_gif_preview(processed, PREVIEW_DIR / f"{dst_folder}.gif")
    
    total_kb = sum(f.stat().st_size for f in dst_path.glob('frame_*.png')) / 1024
    print(f"  ✅ {dst_folder}: {len(processed)} frames, {total_kb:.1f} KB")
    
    return {'success': True, 'folder': dst_folder, 'frames': len(processed), 'total_kb': round(total_kb, 1)}


def extract_audio(video_name: str) -> dict:
    """Extract audio from video as WAV."""
    video_path = None
    for f in VIDEO_DIR.iterdir():
        if f.stem.strip() == video_name or f.stem == video_name:
            video_path = f
            break
    
    if not video_path:
        return {'success': False, 'error': 'Video not found'}
    
    AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    target_folder = VIDEO_MAP.get(video_name, video_name)
    wav_path = AUDIO_DIR / f"{target_folder}.wav"
    
    result = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(video_path),
         "-vn", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "1",
         "-y", str(wav_path)],
        capture_output=True, text=True
    )
    
    if wav_path.exists() and wav_path.stat().st_size > 0:
        size_kb = wav_path.stat().st_size / 1024
        print(f"  🎵 {target_folder}.wav: {size_kb:.1f} KB")
        return {'success': True, 'file': str(wav_path), 'size_kb': round(size_kb, 1)}
    return {'success': False, 'error': result.stderr[:200] if result.stderr else 'unknown'}


def main():
    num_frames = DEFAULT_FRAMES
    if '--frames' in sys.argv:
        idx = sys.argv.index('--frames')
        if idx + 1 < len(sys.argv):
            num_frames = int(sys.argv[idx + 1])
    
    if len(sys.argv) > 1 and not sys.argv[1].startswith('-'):
        # Process specific video
        video_name = sys.argv[1]
        result = process_video(video_name, num_frames)
        
        # Also create flip variant if needed
        target = VIDEO_MAP.get(video_name, video_name)
        if target in FLIP_MAP:
            create_flip_variant(target, FLIP_MAP[target], num_frames)
        
        # Extract audio
        extract_audio(video_name)
    else:
        # Process all videos
        print(f"🚀 Processing {len(VIDEO_MAP)} videos → {num_frames} frames each")
        print(f"   Videos: {VIDEO_DIR}")
        print(f"   Output: {OUTPUT_DIR}")
        print()
        
        all_results = []
        for video_name in sorted(VIDEO_MAP.keys()):
            result = process_video(video_name, num_frames)
            all_results.append(result)
            
            # Create flip variant
            target = VIDEO_MAP.get(video_name, video_name)
            if target in FLIP_MAP:
                flip_result = create_flip_variant(target, FLIP_MAP[target], num_frames)
                all_results.append(flip_result)
        
        # Extract audio from all videos
        print(f"\n{'='*60}")
        print(f"  🎵 EXTRACTING AUDIO")
        print(f"{'='*60}")
        audio_results = []
        for video_name in sorted(VIDEO_MAP.keys()):
            audio = extract_audio(video_name)
            audio_results.append(audio)
        
        # Summary
        print(f"\n{'='*60}")
        print(f"  📊 SUMMARY")
        print(f"{'='*60}")
        
        success = sum(1 for r in all_results if r.get('success'))
        total_frames = sum(r.get('frames', 0) for r in all_results if r.get('success'))
        total_kb = sum(r.get('total_kb', 0) for r in all_results if r.get('success'))
        audio_ok = sum(1 for r in audio_results if r.get('success'))
        
        print(f"  Sprite folders: {success}/{len(all_results)}")
        print(f"  Total frames: {total_frames}")
        print(f"  Total sprite size: {total_kb:.1f} KB ({total_kb/1024:.2f} MB)")
        print(f"  Audio files: {audio_ok}/{len(audio_results)}")
        print(f"\n  ✅ Pipeline complete!")


if __name__ == '__main__':
    main()