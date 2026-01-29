#!/usr/bin/env python3
import re
import sys
import urllib.request

VIDEO_ID_RE = re.compile(r'"videoId":"([A-Za-z0-9_-]{11})"')
IS_LIVE_RE = re.compile(r'"isLiveNow":(true|false)')


def fetch(url: str, verbose: bool) -> str:
    if verbose:
        print(f"[fetch] GET {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=20) as resp:
        data = resp.read().decode('utf-8', errors='ignore')
        if verbose:
            print(f"[fetch] status={resp.status} bytes={len(data)}")
        return data


def extract_video_id_from_url(url: str) -> str | None:
    idx = url.lower().find("watch?v=")
    if idx < 0:
        return None
    part = url[idx + len("watch?v=") :]
    amp = part.find('&')
    if amp >= 0:
        part = part[:amp]
    return part if len(part) == 11 else None


def build_live_url(url: str) -> str:
    if "/live" in url.lower():
        return url
    return url.rstrip('/') + "/live"


def check_watch(video_id: str, verbose: bool) -> bool:
    html = fetch(f"https://www.youtube.com/watch?v={video_id}", verbose)
    m = IS_LIVE_RE.search(html)
    if verbose:
        print(f"[watch] isLiveNow match={m.group(1) if m else 'none'}")
    return bool(m and m.group(1).lower() == "true")


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: youtubelivedetector.py <channel_or_watch_url> [--verbose]")
        return 2
    url = sys.argv[1].strip()
    verbose = "--verbose" in sys.argv[2:] or "-v" in sys.argv[2:]
    try:
        if "watch?v=" in url.lower():
            video_id = extract_video_id_from_url(url)
            if verbose:
                print(f"[input] watch url videoId={video_id or 'none'}")
            if not video_id:
                print("NOT_LIVE")
                return 0
            print("LIVE" if check_watch(video_id, verbose) else "NOT_LIVE")
            return 0

        live_url = build_live_url(url)
        if verbose:
            print(f"[input] channel url live={live_url}")
        html = fetch(live_url, verbose)
        m = VIDEO_ID_RE.search(html)
        if verbose:
            print(f"[live] videoId match={m.group(1) if m else 'none'}")
        if not m:
            print("NOT_LIVE")
            return 0
        video_id = m.group(1)
        print("LIVE" if check_watch(video_id, verbose) else "NOT_LIVE")
        return 0
    except Exception as e:
        print(f"ERROR: {e}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
