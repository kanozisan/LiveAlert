import { AlertConfig, AlertOptions, ConfigRoot } from './config-models';
import { YouTubeLiveDetector, LiveCheckResult } from './youtube-live-detector';
import { EventEmitter } from 'events';

export interface AlertEvent {
  alert: AlertConfig;
  alertIndex: number;
  videoId: string;
  detectedAt: number; // timestamp ms
}

export interface MonitoringSummary {
  anyError: boolean;
  liveLabels: string[];
}

export interface MonitoringFailure {
  label: string;
  url: string;
  reason: string;
  failureCount: number;
}

interface AlertRuntimeState {
  failureCount: number;
  nextAllowed: number;
  currentLiveVideoId: string | null;
}

export class AlertMonitor extends EventEmitter {
  private youtube = new YouTubeLiveDetector();
  private state = new Map<number, AlertRuntimeState>();
  private notifiedVideoIds = new Map<string, number>(); // videoId -> timestamp
  private running = false;
  private timeoutId: ReturnType<typeof setTimeout> | null = null;

  getConfig: () => ConfigRoot;

  constructor(getConfig: () => ConfigRoot) {
    super();
    this.getConfig = getConfig;
  }

  start(): void {
    if (this.running) return;
    this.running = true;
    this.poll();
  }

  stop(): void {
    this.running = false;
    if (this.timeoutId) {
      clearTimeout(this.timeoutId);
      this.timeoutId = null;
    }
  }

  private async poll(): Promise<void> {
    if (!this.running) return;

    const config = this.getConfig();
    // Cleanup old notified IDs based on dedupeMinutes setting
    const dedupeMs = Math.max(1, config.options.dedupeMinutes || 5) * 60 * 1000;
    const cutoff = Date.now() - dedupeMs;
    for (const [id, ts] of this.notifiedVideoIds) {
      if (ts < cutoff) this.notifiedVideoIds.delete(id);
    }

    const results: { isError: boolean; isLive: boolean; label: string }[] = [];

    const promises = config.alerts.map(async (alert, index) => {
      const result = await this.pollAlert(alert, index, config.options);
      results.push(result);
    });

    await Promise.all(promises);

    const anyError = results.some((r) => r.isError);
    const liveLabels = anyError
      ? []
      : results.filter((r) => r.isLive).map((r) => r.label).filter(Boolean);

    this.emit('summary', { anyError, liveLabels } as MonitoringSummary);

    const delay = Math.max(5, config.options.pollIntervalSec) * 1000;
    this.timeoutId = setTimeout(() => this.poll(), delay);
  }

  private async pollAlert(
    alert: AlertConfig,
    index: number,
    options: AlertOptions
  ): Promise<{ isError: boolean; isLive: boolean; label: string }> {
    if (!isYouTube(alert)) {
      return { isError: false, isLive: false, label: alert.label };
    }

    const state = this.getState(index);
    const now = Date.now();
    if (state.nextAllowed > now) {
      return { isError: false, isLive: !!state.currentLiveVideoId, label: alert.label };
    }

    const result = await this.youtube.checkLive(alert);
    if (result.isError) {
      state.failureCount++;
      state.nextAllowed = now + Math.max(5, options.pollIntervalSec) * 1000;
      this.emit('failure', {
        label: alert.label,
        url: alert.url,
        reason: result.errorMessage ?? '',
        failureCount: state.failureCount,
      } as MonitoringFailure);
      return { isError: true, isLive: false, label: alert.label };
    }

    state.failureCount = 0;
    state.nextAllowed = now + Math.max(5, options.pollIntervalSec) * 1000;

    let liveResult = result;
    if (options.debugMode && !result.isLive) {
      const debugId = `debug:${alert.url.trim()}`;
      this.emit('debug', `DebugMode forcing live label=${alert.label} videoId=${debugId}`);
      liveResult = { isLive: true, videoId: debugId, isError: false };
    }

    if (liveResult.isLive && liveResult.videoId) {
      const videoId = liveResult.videoId;
      if (!this.notifiedVideoIds.has(videoId)) {
        if (!liveResult.videoId.startsWith('debug:')) {
          this.notifiedVideoIds.set(videoId, Date.now());
        }
        state.currentLiveVideoId = videoId;
        this.emit('alert', {
          alert,
          alertIndex: index,
          videoId,
          detectedAt: Date.now(),
        } as AlertEvent);
        return { isError: false, isLive: true, label: alert.label };
      }
      state.currentLiveVideoId = videoId;
    } else {
      if (state.currentLiveVideoId) {
        this.emit('ended', state.currentLiveVideoId);
        state.currentLiveVideoId = null;
      }
    }

    return { isError: false, isLive: !!state.currentLiveVideoId, label: alert.label };
  }

  private getState(index: number): AlertRuntimeState {
    let state = this.state.get(index);
    if (!state) {
      state = { failureCount: 0, nextAllowed: 0, currentLiveVideoId: null };
      this.state.set(index, state);
    }
    return state;
  }
}

function isYouTube(alert: AlertConfig): boolean {
  return !alert.service || alert.service === 'youtube';
}
