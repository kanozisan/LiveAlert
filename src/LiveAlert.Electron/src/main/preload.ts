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
  exportConfig: () => ipcRenderer.invoke('export-config'),
  importConfig: () => ipcRenderer.invoke('import-config'),

  onTestAlert: (callback: () => void) => {
    const handler = () => callback();
    ipcRenderer.on('test-alert', handler);
    return () => { ipcRenderer.removeListener('test-alert', handler); };
  },
  onShowAlert: (callback: (data: any) => void) => {
    const handler = (_event: any, data: any) => callback(data);
    ipcRenderer.on('show-alert', handler);
    return () => { ipcRenderer.removeListener('show-alert', handler); };
  },
  onStatusUpdate: (callback: (data: any) => void) => {
    const handler = (_event: any, data: any) => callback(data);
    ipcRenderer.on('status-update', handler);
    return () => { ipcRenderer.removeListener('status-update', handler); };
  },
  onPlayAudio: (callback: (data: any) => void) => {
    const handler = (_event: any, data: any) => callback(data);
    ipcRenderer.on('play-audio', handler);
    return () => { ipcRenderer.removeListener('play-audio', handler); };
  },
});
