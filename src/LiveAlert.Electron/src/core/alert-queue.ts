import { AlertEvent } from './alert-monitor';
import { AlertConfig } from './config-models';

export interface AlertQueueItem {
  event: AlertEvent;
  detectedAt: number;
  videoId: string;
  alert: AlertConfig;
}

export class AlertQueue {
  private pending: AlertQueueItem[] = [];

  dequeueNext(): AlertQueueItem | null {
    if (this.pending.length === 0) return null;
    this.pending.sort((a, b) => {
      const time = a.detectedAt - b.detectedAt;
      if (time !== 0) return time;
      return a.event.alertIndex - b.event.alertIndex;
    });
    return this.pending.shift()!;
  }

  enqueue(event: AlertEvent): void {
    if (this.pending.some((x) => x.videoId === event.videoId)) return;
    this.pending.push({
      event,
      detectedAt: event.detectedAt,
      videoId: event.videoId,
      alert: event.alert,
    });
  }

  removeByVideoId(videoId: string): void {
    this.pending = this.pending.filter((x) => x.videoId !== videoId);
  }

  contains(videoId: string): boolean {
    return this.pending.some((x) => x.videoId === videoId);
  }
}
