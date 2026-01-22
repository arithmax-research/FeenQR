#!/usr/bin/env python3
"""
YouTube Transcript Fetcher
Extracts transcripts/captions from YouTube videos using youtube-transcript-api
"""

import sys
import json
from youtube_transcript_api import YouTubeTranscriptApi

def get_transcript(video_id):
    """
    Get transcript for a YouTube video
    
    Args:
        video_id: YouTube video ID (11 characters)
        
    Returns:
        dict with transcript data or error
    """
    try:
        api = YouTubeTranscriptApi()
        result = api.fetch(video_id)
        
        # Extract full text
        full_text = " ".join([snippet.text for snippet in result.snippets])
        
        return {
            "success": True,
            "video_id": result.video_id,
            "language": result.language,
            "language_code": result.language_code,
            "is_generated": result.is_generated,
            "transcript": full_text,
            "length": len(full_text)
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
            "error": "Usage: get_youtube_transcript.py VIDEO_ID"
        }))
        sys.exit(1)
    
    video_id = sys.argv[1]
    result = get_transcript(video_id)
    print(json.dumps(result))

if __name__ == "__main__":
    main()
