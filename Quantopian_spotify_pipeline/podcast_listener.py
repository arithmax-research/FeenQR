import whisper
import spotipy
from spotipy.oauth2 import SpotifyClientCredentials
from transformers import pipeline
import os
from dotenv import load_dotenv
load_dotenv('./.env')
from openai import OpenAI

client = OpenAI(api_key=os.getenv("OPEN_AI_API"))

def fetch_podcast_audio(spotify_client_id, spotify_client_secret, podcast_url):
    sp = spotipy.Spotify(auth_manager=SpotifyClientCredentials(
        client_id=spotify_client_id,
        client_secret=spotify_client_secret
    ))
    episode_id = podcast_url.split("/")[-1].split("?")[0]
    try:
        episode = sp.episode(episode_id, market="US")  
        print(f"Found episode: {episode['name']} - {episode['description']}")
        return episode['name'], episode['description']
    except spotipy.exceptions.SpotifyException as e:
        if "404" in str(e):
            print("Episode not found. Please check the URL or ensure the episode is available in the specified market.")
        else:
            print(f"Error fetching episode: {e}")
        return None, None

def extract_technical_content(transcript):
    summarizer = pipeline("summarization", model="t5-small")
    max_chunk_size = 512  
    chunks = [transcript[i:i + max_chunk_size] for i in range(0, len(transcript), max_chunk_size)]

    summaries = []
    for chunk in chunks:
        try:
            summary = summarizer(chunk, max_length=150, min_length=30, do_sample=False)
            summaries.append(summary[0]['summary_text'])
        except Exception as e:
            print(f"Error summarizing chunk: {e}")

    combined_summary = " ".join(summaries)
    return combined_summary


def generate_technical_report(technical_content):
    prompt = f"Create a technical report based on the following content:\n\n{technical_content}"
    response = client.chat.completions.create(model="gpt-4o-mini",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": prompt}
    ],
    max_tokens=500,
    temperature=0.7)
    return response.choices[0].message.content.strip()

def generate_code_from_report(technical_report):
    prompt = f"Write Python code based on the following technical report:\n\n{technical_report}"
    response = client.chat.completions.create(
        model="gpt-4o-mini",
        messages=[
            {"role": "system", "content": "You are a helpful assistant."},
            {"role": "user", "content": prompt}
        ],
        max_tokens=1000,
        temperature=0.7
    )
    return response.choices[0].message.content.strip()

def main():
    spotify_client_id = os.getenv("SPOTIFY_CLIENT_ID")
    spotify_client_secret = os.getenv("SPOTIFY_CLIENT_SECRET")
    podcast_url = "https://open.spotify.com/episode/69tcEMbTyOEcPfgEJ95xos?si=kERdy3ZTT0ePUEaQ2TGZ8Q"  # Replace with the actual episode URL

    podcast_name, podcast_description = fetch_podcast_audio(spotify_client_id, spotify_client_secret, podcast_url)
    if not podcast_name:
        return

    transcript_file_path = "sample_transcript.txt"  
    print("Reading transcript from file...")
    with open(transcript_file_path, "r") as file:
        transcript = file.read()
    print("Transcript:", transcript)

    print("Extracting technical content...")
    technical_content = extract_technical_content(transcript)
    print("Technical Content:", technical_content)

    print("Generating technical report...")
    technical_report = generate_technical_report(technical_content)
    print("Technical Report:", technical_report)

    print("Generating code from report...")
    generated_code = generate_code_from_report(technical_report)
    print("Generated Code:\n", generated_code)

if __name__ == "__main__":
    main()