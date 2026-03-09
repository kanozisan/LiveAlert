export interface ConfigRoot {
  alerts: AlertConfig[];
  options: AlertOptions;
}

export interface AlertConfig {
  service?: string;
  url: string;
  titleContains: string;
  label: string;
  voice: string;
  voiceVolume: number;
  bgm: string;
  bgmVolume: number;
  message: string;
  colors: AlertColors;
}

export interface AlertColors {
  background: string;
  text: string;
}

export interface AlertOptions {
  pollIntervalSec: number;
  maxAlarmDurationSec: number;
  bandPosition: 'top' | 'center' | 'bottom';
  bandHeightPx: number;
  hotReload: boolean;
  notificationMode: 'alarm' | 'manner' | 'off';
  displayMode: 'alarm' | 'notification' | 'manner' | 'off';
  audioMode: 'alarm' | 'manner' | 'off';
  loopIntervalSec: number;
  dedupeMinutes: number;
  expandedAlertIndex: number;
  debugMode: boolean;
  windowsAutoStart: boolean;
}

export function createDefaultConfig(): ConfigRoot {
  return {
    alerts: [
      {
        service: 'youtube',
        url: 'https://www.youtube.com/channel/XXXX',
        titleContains: '',
        label: 'SAMPLE',
        voice: '',
        voiceVolume: 100,
        bgm: '',
        bgmVolume: 50,
        message: '警告　{label} がライブ開始',
        colors: { background: '#FF0000', text: '#000000' },
      },
    ],
    options: createDefaultOptions(),
  };
}

export function createDefaultOptions(): AlertOptions {
  return {
    pollIntervalSec: 60,
    maxAlarmDurationSec: 30,
    bandPosition: 'top',
    bandHeightPx: 340,
    hotReload: true,
    notificationMode: 'alarm',
    displayMode: 'alarm',
    audioMode: 'alarm',
    loopIntervalSec: 5,
    dedupeMinutes: 5,
    expandedAlertIndex: -1,
    debugMode: false,
    windowsAutoStart: false,
  };
}

export function createDefaultAlert(): AlertConfig {
  return {
    service: 'youtube',
    url: '',
    titleContains: '',
    label: '',
    voice: '',
    voiceVolume: 100,
    bgm: '',
    bgmVolume: 50,
    message: '警告　{label} がライブ開始',
    colors: { background: '#FF0000', text: '#000000' },
  };
}
