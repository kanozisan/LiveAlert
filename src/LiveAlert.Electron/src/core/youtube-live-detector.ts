import { AlertConfig } from './config-models';

export interface LiveCheckResult {
  isLive: boolean;
  videoId?: string;
  isError: boolean;
  errorMessage?: string;
}

const IS_LIVE_NOW_REGEX = /"isLiveNow":(true|false)/;
const INITIAL_DATA_REGEX = /var ytInitialData\s*=\s*(\{.*?\});/s;
const USER_AGENT = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36';

export class YouTubeLiveDetector {
  private abortController: AbortController | null = null;

  async checkLive(alert: AlertConfig, signal?: AbortSignal): Promise<LiveCheckResult> {
    try {
      const url = alert.url.trim();
      if (url.includes('watch?v=')) {
        const videoId = extractVideoIdFromUrl(url);
        if (!videoId) return { isLive: false, isError: false };
        return await this.checkWatchPage(videoId, signal);
      }

      const baseUrl = normalizeChannelUrl(url);
      const streamsUrl = `${baseUrl}/streams`;
      const streamsResult = await this.fetchString(streamsUrl, signal);
      if (streamsResult.error) {
        return { isLive: false, isError: true, errorMessage: streamsResult.error };
      }

      let candidateId = extractLiveVideoId(streamsResult.content);
      if (!candidateId) {
        const homeResult = await this.fetchString(baseUrl, signal);
        if (homeResult.error) {
          return { isLive: false, isError: true, errorMessage: homeResult.error };
        }
        candidateId = extractLiveVideoId(homeResult.content);
      }

      if (!candidateId) {
        return { isLive: false, isError: false };
      }

      return await this.checkWatchPage(candidateId, signal);
    } catch (e: any) {
      if (e.name === 'AbortError') throw e;
      return { isLive: false, isError: true, errorMessage: e.message };
    }
  }

  cancel(): void {
    this.abortController?.abort();
  }

  private async checkWatchPage(videoId: string, signal?: AbortSignal): Promise<LiveCheckResult> {
    const watchUrl = `https://www.youtube.com/watch?v=${videoId}`;
    const result = await this.fetchString(watchUrl, signal);
    if (result.error) {
      return { isLive: false, isError: true, errorMessage: result.error };
    }
    const match = IS_LIVE_NOW_REGEX.exec(result.content ?? '');
    if (match && match[1] === 'true') {
      return { isLive: true, videoId, isError: false };
    }
    return { isLive: false, isError: false };
  }

  private async fetchString(
    url: string,
    signal?: AbortSignal
  ): Promise<{ content?: string; error?: string }> {
    try {
      const response = await fetch(url, {
        headers: { 'User-Agent': USER_AGENT },
        signal,
      });
      if (!response.ok) {
        return { error: `HTTP ${response.status} ${response.statusText}` };
      }
      const content = await response.text();
      return { content };
    } catch (e: any) {
      if (e.name === 'AbortError') throw e;
      return { error: e.message };
    }
  }
}

function normalizeChannelUrl(url: string): string {
  url = url.replace(/\/+$/, '');
  const lowered = url.toLowerCase();
  for (const suffix of ['/streams', '/live', '/videos', '/featured']) {
    if (lowered.endsWith(suffix)) {
      return url.slice(0, -suffix.length);
    }
  }
  return url;
}

function extractVideoIdFromUrl(url: string): string | null {
  const idx = url.toLowerCase().indexOf('watch?v=');
  if (idx < 0) return null;
  let part = url.slice(idx + 'watch?v='.length);
  const amp = part.indexOf('&');
  if (amp >= 0) part = part.slice(0, amp);
  return part.length === 11 ? part : null;
}

function extractLiveVideoId(html?: string): string | null {
  if (!html) return null;
  const match = INITIAL_DATA_REGEX.exec(html);
  if (!match) return null;
  try {
    const data = JSON.parse(match[1]);
    return findLiveVideoId(data);
  } catch {
    return null;
  }
}

function findLiveVideoId(element: any): string | null {
  if (element === null || element === undefined) return null;
  if (Array.isArray(element)) {
    for (const item of element) {
      const found = findLiveVideoId(item);
      if (found) return found;
    }
    return null;
  }
  if (typeof element === 'object') {
    if (element.videoId && typeof element.videoId === 'string' && Array.isArray(element.thumbnailOverlays)) {
      if (hasLiveOverlay(element.thumbnailOverlays)) {
        return element.videoId;
      }
    }
    for (const key of Object.keys(element)) {
      const found = findLiveVideoId(element[key]);
      if (found) return found;
    }
  }
  return null;
}

function hasLiveOverlay(overlays: any[]): boolean {
  for (const overlay of overlays) {
    const renderer = overlay?.thumbnailOverlayTimeStatusRenderer;
    if (!renderer) continue;
    if (typeof renderer.style === 'string' && renderer.style.toUpperCase() === 'LIVE') {
      return true;
    }
    if (renderer.text && textContainsLiveLabel(renderer.text)) {
      return true;
    }
  }
  return false;
}

function textContainsLiveLabel(textElement: any): boolean {
  if (typeof textElement !== 'object') return false;
  if (typeof textElement.simpleText === 'string' && textElement.simpleText.includes('ライブ')) {
    return true;
  }
  if (Array.isArray(textElement.runs)) {
    for (const run of textElement.runs) {
      if (typeof run?.text === 'string' && run.text.includes('ライブ')) {
        return true;
      }
    }
  }
  return false;
}
