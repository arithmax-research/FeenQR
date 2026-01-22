#!/usr/bin/env python3
"""
YouTube Transcript Fetcher using yt-dlp
More reliable for bypassing YouTube restrictions
"""

import sys
import json
import subprocess
import tempfile
import os

def get_transcript(video_id):
    """
    Get transcript for a YouTube video using yt-dlp
    
    Args:
        video_id: YouTube video ID (11 characters)
        
    Returns:
        dict with transcript data or error
    """
    try:
        video_url = f"https://www.youtube.com/watch?v={video_id}"
        
        # Use yt-dlp to get subtitles
        cmd = [
            'yt-dlp',
            '--skip-download',
            '--write-auto-sub',
            '--sub-lang', 'en',
            '--sub-format', 'json3',
            '--output', '%(id)s',
            video_url
        ]
        
        # Try to get subtitles
        result = subprocess.run(cmd, capture_output=True, text=True, cwd=tempfile.gettempdir())
        
        if result.returncode != 0:
            # Try with cookies if available
            cookies_file = os.path.expanduser("~/.config/yt-dlp/cookies.txt")
            if os.path.exists(cookies_file):
                cmd.extend(['--cookies', cookies_file])
                result = subprocess.run(cmd, capture_output=True, text=True, cwd=tempfile.gettempdir())
        
        # Look for the subtitle file
        subtitle_file = os.path.join(tempfile.gettempdir(), f"{video_id}.en.json3")
        
        if os.path.exists(subtitle_file):
            with open(subtitle_file, 'r', encoding='utf-8') as f:
                subtitle_data = json.load(f)
            
            # Extract text from subtitle events
            transcript_text = []
            if 'events' in subtitle_data:
                for event in subtitle_data['events']:
                    if 'segs' in event:
                        for seg in event['segs']:
                            if 'utf8' in seg:
                                transcript_text.append(seg['utf8'].strip())
            
            full_text = " ".join(transcript_text)
            
            # Clean up the file
            os.remove(subtitle_file)
            
            return {
                "success": True,
                "video_id": video_id,
                "language": "English (auto-generated)",
                "language_code": "en",
                "is_generated": True,
                "transcript": full_text,
                "length": len(full_text)
            }
        else:
            return {
                "success": False,
                "error": "Could not download subtitles with yt-dlp",
                "video_id": video_id
            }
            
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "video_id": video_id
        }

def main():
    if len(sys.argv) != 2:
        print(json.dumps({
            "success": False,
            "error": "Usage: get_youtube_transcript_ytdlp.py VIDEO_ID"
        }))
        sys.exit(1)
    
    video_id = sys.argv[1]
    result = get_transcript(video_id)
    print(json.dumps(result))

if __name__ == "__main__":
    main()
