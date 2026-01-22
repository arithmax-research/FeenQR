#!/usr/bin/env python3
"""
YouTube Transcript Fetcher
Extracts transcripts/captions from YouTube videos using youtube-transcript-api
"""

import sys
import json
import os
from youtube_transcript_api import YouTubeTranscriptApi
from youtube_transcript_api.formatters import TextFormatter

def get_transcript(video_id):
    """
    Get transcript for a YouTube video
    
    Args:
        video_id: YouTube video ID (11 characters)
        
    Returns:
        dict with transcript data or error
    """
    try:
        # Create API instance
        api = YouTubeTranscriptApi()
        
        # Get list of available transcripts
        transcript_list = api.list(video_id)
        
        # Try to get manual transcript first (more accurate)
        transcript = None
        is_generated = False
        
        try:
            transcript = transcript_list.find_manually_created_transcript(['en'])
            is_generated = False
        except:
            # Fall back to auto-generated
            try:
                transcript = transcript_list.find_generated_transcript(['en'])
                is_generated = True
            except:
                # Try any available transcript
                try:
                    transcript = transcript_list.find_transcript(['en'])
                    is_generated = transcript.is_generated
                except:
                    # Last resort - just get any transcript
                    for t in transcript_list:
                        transcript = t
                        is_generated = t.is_generated
                        break
        
        if transcript is None:
            raise Exception("No transcript found for this video")
        
        # Fetch the actual transcript data
        transcript_data = transcript.fetch()
        
        # Extract full text
        full_text = " ".join([entry['text'] for entry in transcript_data])
        
        return {
            "success": True,
            "video_id": video_id,
            "language": transcript.language,
            "language_code": transcript.language_code,
            "is_generated": is_generated,
            "transcript": full_text,
            "length": len(full_text),
            "entries": len(transcript_data)
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
