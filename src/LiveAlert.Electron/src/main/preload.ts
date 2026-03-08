import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('electronAPI', {
  readConfig: () => ipcRenderer.invoke('read-config'),
  writeConfig: (json: string) => ipcRenderer.invoke('write-config', json),
  getConfigPath: () => ipcRenderer.invoke('get-config-path'),
  getAssetsPath: () => ipcRenderer.invoke('get-assets-path'),
  getConfigDir: () => ipcRenderer.invoke('get-config-dir'),
  openUrl: (url: string) => ipcRenderer.invoke('open-url', url),
  showOverlay: (alertData: any) => ipcRenderer.invoke('show-overlay', alertData),
  hideOverlay: () => ipcRenderer.invoke('hide-overlay'),
  stopAlert: () => ipcRenderer.invoke('stop-alert'),
  testAlert: () => ipcRenderer.invoke('test-alert'),

  onTestAlert: (callback: () => void) => {
    ipcRenderer.on('test-alert', () => callback());
    return () => { ipcRenderer.removeAllListeners('test-alert'); };
  },
  onShowAlert: (callback: (data: any) => void) => {
    ipcRenderer.on('show-alert', (_event, data) => callback(data));
    return () => { ipcRenderer.removeAllListeners('show-alert'); };
  },
  onStatusUpdate: (callback: (data: any) => void) => {
    ipcRenderer.on('status-update', (_event, data) => callback(data));
    return () => { ipcRenderer.removeAllListeners('status-update'); };
  },
  onPlayAudio: (callback: (data: any) => void) => {
    ipcRenderer.on('play-audio', (_event, data) => callback(data));
    return () => { ipcRenderer.removeAllListeners('play-audio'); };
  },
});
