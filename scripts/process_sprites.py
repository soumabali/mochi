#!/usr/bin/env python3
"""
Mochi v2 Sprite Auto-Processor
================================
Processes raw sprite frames from Dhar into optimized sprites.

Pipeline:
  1. Remove white/solid background → transparent
  2. Resize to 200px wide (maintain aspect ratio)
  3. Center horizontally on transparent canvas
  4. Optimize PNG (lossless compression via PIL)
  5. Output to Sprite_optimized/<folder_name>/
  6. Create animated GIF preview
  7. Verify dimensions

Usage:
  python3 process_sprites.py                    # Process all in Sprite_raw/
  python3 process_sprites.py <folder_name>      # Process specific folder
  python3 process_sprites.py --preview-only     # Just regenerate GIF previews
"""

import os
import sys
import json
import shutil
from pathlib import Path
from PIL import Image, ImageSequence
import io

# Paths
BASE = Path(__file__).parent.parent / "02-application" / "Assets"
RAW_DIR = BASE / "Sprite_raw"
OPT_DIR = BASE / "Sprite_optimized"
MANIFEST = BASE / "manifest.json"
PREVIEW_DIR = BASE / "Sprite_preview"

# Target width for all sprites
TARGET_WIDTH = 200

# Background removal threshold (white-ish pixels)
BG_THRESHOLD = 240  # pixels above this in ALL channels → transparent
BG_TOLERANCE = 15   # flood fill tolerance


def remove_white_bg(img: Image.Image) -> Image.Image:
    """Remove white/solid background and make it transparent."""
    if img.mode != 'RGBA':
        img = img.convert('RGBA')
    
    # Use numpy for reliable pixel manipulation
    import numpy as np
    arr = np.array(img)
    # Where all RGB channels are above threshold → set alpha to 0
    mask = (arr[:,:,0] >= BG_THRESHOLD) & (arr[:,:,1] >= BG_THRESHOLD) & (arr[:,:,2] >= BG_THRESHOLD)
    arr[mask, 3] = 0
    img = Image.fromarray(arr, 'RGBA')
    
    # Auto-crop transparent borders
    bbox = img.getbbox()
    if bbox:
        img = img.crop(bbox)
    
    return img


def resize_to_width(img: Image.Image, target_width: int = TARGET_WIDTH) -> Image.Image:
    """Resize image to target width, maintaining aspect ratio."""
    if img.width == target_width:
        return img
    
    ratio = target_width / img.width
    new_height = int(img.height * ratio)
    return img.resize((target_width, new_height), Image.Resampling.LANCZOS)


def optimize_png(img: Image.Image) -> Image.Image:
    """Optimize PNG for smaller file size."""
    # Convert to palette mode if possible (smaller files for sprites)
    if img.mode == 'RGBA':
        # Split alpha, quantize RGB, recombine
        r, g, b, a = img.split()
        rgb = Image.merge('RGB', (r, g, b))
        rgb = rgb.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        rgb = rgb.convert('RGB')
        img = Image.merge('RGBA', (*rgb.split(), a))
    return img


def create_animated_gif(frames: list[Image.Image], output_path: Path, duration_ms: int = 100):
    """Create animated GIF from list of frames."""
    if not frames:
        return
    
    # All frames same size for GIF
    max_width = max(f.width for f in frames)
    max_height = max(f.height for f in frames)
    
    normalized = []
    for frame in frames:
        canvas = Image.new('RGBA', (max_width, max_height), (0, 0, 0, 0))
        # Center frame
        x = (max_width - frame.width) // 2
        y = (max_height - frame.height) // 2
        canvas.paste(frame, (x, y), frame)
        normalized.append(canvas.convert('RGB'))
    
    normalized[0].save(
        output_path,
        save_all=True,
        append_images=normalized[1:],
        duration=duration_ms,
        loop=0,
        optimize=True
    )


def process_folder(folder_name: str, raw_path: Path, opt_path: Path) -> dict:
    """Process a single folder of raw sprites."""
    print(f"\n{'='*60}")
    print(f"  Processing: {folder_name}")
    print(f"{'='*60}")
    
    # Find PNG files
    pngs = sorted([f for f in raw_path.iterdir() if f.suffix.lower() == '.png'])
    if not pngs:
        print(f"  ⚠️  No PNG files found in {raw_path}")
        return {'success': False, 'error': 'no PNGs'}
    
    print(f"  Found {len(pngs)} frames")
    
    # Create output directory
    opt_path.mkdir(parents=True, exist_ok=True)
    
    # Clear old optimized files
    for old in opt_path.glob('frame_*.png'):
        old.unlink()
    
    processed_frames = []
    results = []
    
    for i, png_file in enumerate(pngs, 1):
        try:
            img = Image.open(png_file)
            print(f"  Frame {i:03d}: {png_file.name} → {img.size[0]}x{img.size[1]}", end='')
            
            # Step 1: Remove background
            img = remove_white_bg(img)
            
            # Step 2: Resize to target width
            img = resize_to_width(img, TARGET_WIDTH)
            
            # Step 3: Optimize
            img = optimize_png(img)
            
            # Step 4: Save
            out_name = f"frame_{i:03d}.png"
            out_path = opt_path / out_name
            img.save(out_path, 'PNG', optimize=True)
            
            size_kb = out_path.stat().st_size / 1024
            print(f" → {img.size[0]}x{img.size[1]}, {size_kb:.1f} KB ✓")
            
            processed_frames.append(img)
            results.append({
                'frame': out_name,
                'width': img.size[0],
                'height': img.size[1],
                'size_kb': round(size_kb, 1)
            })
            
        except Exception as e:
            print(f" ✗ ERROR: {e}")
            results.append({'frame': png_file.name, 'error': str(e)})
    
    # Create animated GIF preview
    if processed_frames:
        PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
        gif_path = PREVIEW_DIR / f"{folder_name}.gif"
        create_animated_gif(processed_frames, gif_path)
        print(f"  📦 GIF preview: {gif_path}")
    
    # Summary
    total_size = sum(r.get('size_kb', 0) for r in results if 'size_kb' in r)
    print(f"\n  ✅ Done: {len(processed_frames)} frames, {total_size:.1f} KB total")
    
    return {
        'success': True,
        'folder': folder_name,
        'frames': len(processed_frames),
        'total_size_kb': round(total_size, 1),
        'details': results
    }


def process_all():
    """Process all folders in Sprite_raw/."""
    if not RAW_DIR.exists():
        print(f"❌ Raw directory not found: {RAW_DIR}")
        print(f"   Create it and put your sprite frames there.")
        return
    
    folders = [d for d in RAW_DIR.iterdir() if d.is_dir()]
    if not folders:
        print(f"❌ No folders found in {RAW_DIR}")
        return
    
    print(f"🚀 Processing {len(folders)} sprite folders...")
    print(f"   Raw: {RAW_DIR}")
    print(f"   Output: {OPT_DIR}")
    
    all_results = []
    for folder in sorted(folders):
        folder_name = folder.name
        opt_path = OPT_DIR / folder_name
        result = process_folder(folder_name, folder, opt_path)
        all_results.append(result)
    
    # Summary
    print(f"\n{'='*60}")
    print(f"  SUMMARY")
    print(f"{'='*60}")
    
    success = sum(1 for r in all_results if r.get('success'))
    total_frames = sum(r.get('frames', 0) for r in all_results)
    total_size = sum(r.get('total_size_kb', 0) for r in all_results)
    
    print(f"  Folders processed: {success}/{len(all_results)}")
    print(f"  Total frames: {total_frames}")
    print(f"  Total size: {total_size:.1f} KB ({total_size/1024:.2f} MB)")
    print(f"\n  ✅ All done! Run 'dotnet build' to verify.")


def generate_previews_only():
    """Regenerate GIF previews from existing optimized sprites."""
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    
    folders = [d for d in OPT_DIR.iterdir() if d.is_dir()]
    print(f"🎬 Generating GIF previews for {len(folders)} folders...")
    
    for folder in sorted(folders):
        pngs = sorted([f for f in folder.iterdir() if f.suffix.lower() == '.png'])
        if not pngs:
            continue
        
        frames = [Image.open(p).convert('RGBA') for p in pngs]
        gif_path = PREVIEW_DIR / f"{folder.name}.gif"
        create_animated_gif(frames, gif_path)
        print(f"  {folder.name}: {len(pngs)} frames → {gif_path.name}")
    
    print(f"\n✅ Previews in {PREVIEW_DIR}")


def main():
    if '--preview-only' in sys.argv:
        generate_previews_only()
        return
    
    if len(sys.argv) > 1 and not sys.argv[1].startswith('-'):
        # Process specific folder
        folder_name = sys.argv[1]
        raw_path = RAW_DIR / folder_name
        opt_path = OPT_DIR / folder_name
        if not raw_path.exists():
            print(f"❌ Folder not found: {raw_path}")
            return
        process_folder(folder_name, raw_path, opt_path)
    else:
        process_all()


if __name__ == '__main__':
    main()