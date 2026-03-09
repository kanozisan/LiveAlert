import React, { useState, useEffect, useCallback, useRef } from 'react';
import { ConfigRoot, AlertConfig, createDefaultConfig, createDefaultAlert } from '../core/config-models';
import { AlertEditor } from './components/AlertEditor';
import { SettingsPanel } from './components/SettingsPanel';

declare global {
  interface Window {
    electronAPI: {
      readConfig: () => Promise<string | null>;
      writeConfig: (json: string) => Promise<void>;
      getAssetsPath: () => Promise<string>;
      openUrl: (url: string) => Promise<void>;
      showOverlay: (data: any) => Promise<void>;
      hideOverlay: () => Promise<void>;
      stopAlert: () => Promise<void>;
      testAlert: () => Promise<void>;
      exportConfig: () => Promise<{ success: boolean; error?: string }>;
      importConfig: () => Promise<{ success: boolean; config?: string; error?: string }>;
      onTestAlert: (cb: () => void) => () => void;
      onStatusUpdate: (cb: (data: any) => void) => () => void;
      onPlayAudio: (cb: (data: any) => void) => () => void;
    };
  }
}

// Simple audio manager using Web Audio
let voiceAudio: HTMLAudioElement | null = null;
let bgmAudio: HTMLAudioElement | null = null;

function stopAudio() {
  voiceAudio?.pause();
  bgmAudio?.pause();
  voiceAudio = null;
  bgmAudio = null;
}

async function playAudio(data: { voice: string; voiceVolume: number; bgm: string; bgmVolume: number }, assetsPath: string) {
  stopAudio();

  if (data.voice) {
    const voicePath = data.voice.startsWith('/') || data.voice.startsWith('file:')
      ? data.voice
      : `file://${assetsPath}/${data.voice}`;
    voiceAudio = new Audio(voicePath);
    voiceAudio.volume = data.voiceVolume / 100;
    voiceAudio.play().catch(console.warn);
  }

  if (data.bgm) {
    const bgmPath = data.bgm.startsWith('/') || data.bgm.startsWith('file:')
      ? data.bgm
      : `file://${assetsPath}/${data.bgm}`;
    bgmAudio = new Audio(bgmPath);
    bgmAudio.volume = data.bgmVolume / 100;
    bgmAudio.loop = true;
    bgmAudio.play().catch(console.warn);
  }
}

export function App() {
  const [config, setConfig] = useState<ConfigRoot>(createDefaultConfig());
  const [statusText, setStatusText] = useState('起動中...');
  const [currentAlert, setCurrentAlert] = useState('なし');
  const [loaded, setLoaded] = useState(false);
  const assetsPathRef = useRef('');

  useEffect(() => {
    (async () => {
      const json = await window.electronAPI.readConfig();
      if (json) {
        try { setConfig(JSON.parse(json)); } catch { /* use default */ }
      }
      assetsPathRef.current = await window.electronAPI.getAssetsPath();
      setLoaded(true);
      setStatusText('監視中');
    })();
  }, []);

  // Listen for status updates from main process
  useEffect(() => {
    const cleanup = window.electronAPI.onStatusUpdate((data: any) => {
      if (data.statusText) setStatusText(data.statusText);
      if (data.currentAlert !== undefined) {
        setCurrentAlert(data.currentAlert);
        if (data.currentAlert === 'なし') stopAudio();
      }
    });
    return cleanup;
  }, []);

  // Listen for audio playback requests
  useEffect(() => {
    const cleanup = window.electronAPI.onPlayAudio((data: any) => {
      playAudio(data, assetsPathRef.current);
    });
    return cleanup;
  }, []);

  const saveConfig = useCallback(async (newConfig: ConfigRoot) => {
    setConfig(newConfig);
    await window.electronAPI.writeConfig(JSON.stringify(newConfig, null, 2));
  }, []);

  const addAlert = useCallback(() => {
    const newConfig = { ...config, alerts: [...config.alerts, createDefaultAlert()] };
    saveConfig(newConfig);
  }, [config, saveConfig]);

  const updateAlert = useCallback((index: number, alert: AlertConfig) => {
    const alerts = [...config.alerts];
    alerts[index] = alert;
    saveConfig({ ...config, alerts });
  }, [config, saveConfig]);

  const deleteAlert = useCallback((index: number) => {
    const alerts = config.alerts.filter((_, i) => i !== index);
    saveConfig({ ...config, alerts });
  }, [config, saveConfig]);

  const handleExport = useCallback(async () => {
    const result = await window.electronAPI.exportConfig();
    if (result.error) alert(`エクスポート失敗: ${result.error}`);
  }, []);

  const handleImport = useCallback(async () => {
    const result = await window.electronAPI.importConfig();
    if (result.error) {
      alert(`インポート失敗: ${result.error}`);
    } else if (result.success && result.config) {
      try { setConfig(JSON.parse(result.config)); } catch { /* ignore */ }
    }
  }, []);

  if (!loaded) {
    return <div className="app loading">読み込み中...</div>;
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>LiveAlert</h1>
        <div className="status-bar">
          <span className="status-text">{statusText}</span>
          <span className="current-alert">アラート: {currentAlert}</span>
          {currentAlert !== 'なし' && (
            <button className="btn btn-stop" onClick={() => window.electronAPI.stopAlert()}>
              停止
            </button>
          )}
          <button className="btn btn-test" onClick={() => window.electronAPI.testAlert()}>
            テスト
          </button>
        </div>
      </header>

      <main className="app-main">
        <section className="alerts-section">
          <div className="section-header">
            <h2>アラート設定</h2>
            <button className="btn btn-add" onClick={addAlert}>+ 追加</button>
          </div>
          {config.alerts.map((alert, index) => (
            <AlertEditor
              key={index}
              index={index}
              alert={alert}
              onChange={(a) => updateAlert(index, a)}
              onDelete={() => deleteAlert(index)}
            />
          ))}
          {config.alerts.length === 0 && (
            <p className="empty-message">アラートが設定されていません。「+ 追加」で追加してください。</p>
          )}
        </section>

        <section className="options-section">
          <h2>オプション</h2>
          <SettingsPanel
            options={config.options}
            onChange={(options) => saveConfig({ ...config, options })}
          />
        </section>

        <section className="io-section">
          <h2>設定の管理</h2>
          <div className="io-buttons">
            <button className="btn btn-io" onClick={handleExport}>エクスポート</button>
            <button className="btn btn-io" onClick={handleImport}>インポート</button>
          </div>
        </section>
      </main>
    </div>
  );
}
