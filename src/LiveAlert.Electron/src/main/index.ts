import { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage, shell, dialog, Notification, screen } from 'electron';
import * as path from 'path';
import * as fs from 'fs';
import * as fsp from 'fs/promises';
import { AlertMonitor, AlertEvent, MonitoringSummary, MonitoringFailure } from '../core/alert-monitor';
import { AlertQueue, AlertQueueItem } from '../core/alert-queue';
import { ConfigRoot, createDefaultConfig } from '../core/config-models';

let mainWindow: BrowserWindow | null = null;
let overlayWindow: BrowserWindow | null = null;
let tray: Tray | null = null;
let isQuitting = false;

const isDev = !app.isPackaged;
app.setName('LiveAlert');

// --- State ---
let currentConfig: ConfigRoot = createDefaultConfig();
let currentItem: AlertQueueItem | null = null;
let autoStopTimer: ReturnType<typeof setTimeout> | null = null;

const alertQueue = new AlertQueue();
const monitor = new AlertMonitor(() => currentConfig);

// --- Paths ---
function getConfigDir(): string {
  const dir = path.join(app.getPath('userData'), 'LiveAlert');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function getConfigPath(): string {
  return path.join(getConfigDir(), 'config.json');
}

function getAssetsPath(): string {
  if (isDev) {
    return path.resolve(__dirname, '../../assets');
  }
  return path.join(process.resourcesPath, 'assets');
}

// --- Config ---
function loadConfig(): ConfigRoot {
  const configPath = getConfigPath();
  if (!fs.existsSync(configPath)) {
    const def = createDefaultConfig();
    fs.writeFileSync(configPath, JSON.stringify(def, null, 2), 'utf-8');
    return def;
  }
  try {
    const json = fs.readFileSync(configPath, 'utf-8');
    return JSON.parse(json) as ConfigRoot;
  } catch {
    return createDefaultConfig();
  }
}

// --- Windows ---
function createMainWindow(): void {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 700,
    minWidth: 600,
    minHeight: 500,
    title: 'LiveAlert',
    backgroundColor: '#101010',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
    show: false,
  });

  if (isDev) {
    mainWindow.loadFile(path.resolve(__dirname, '../../dist/renderer/src/renderer/index.html'));
  } else {
    mainWindow.loadFile(path.join(__dirname, '../renderer/src/renderer/index.html'));
  }

  mainWindow.once('ready-to-show', () => {
    mainWindow?.show();
  });

  mainWindow.on('close', (e) => {
    if (!isQuitting) {
      e.preventDefault();
      mainWindow?.hide();
      // Hide from Dock when window is closed (macOS menu bar app pattern)
      if (process.platform === 'darwin') {
        app.dock?.hide();
      }
    }
  });
}

function showMainWindow(): void {
  if (process.platform === 'darwin') {
    app.dock?.show();
  }
  mainWindow?.show();
  mainWindow?.focus();
}

function createOverlayWindow(bandHeightPx: number, bandPosition: string): BrowserWindow {
  const primaryDisplay = screen.getPrimaryDisplay();
  const { width: screenWidth, height: screenHeight } = primaryDisplay.workAreaSize;
  const bandHeight = Math.min(bandHeightPx, Math.floor(screenHeight * 0.5));

  let y = 0;
  if (bandPosition === 'bottom') {
    y = screenHeight - bandHeight;
  } else if (bandPosition === 'center') {
    y = Math.floor((screenHeight - bandHeight) / 2);
  }

  const win = new BrowserWindow({
    x: 0,
    y,
    width: screenWidth,
    height: bandHeight,
    transparent: true,
    frame: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    focusable: true,  // allow click to dismiss
    resizable: false,
    movable: false,
    hasShadow: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  // Keep on top of all windows including fullscreen apps
  win.setAlwaysOnTop(true, 'screen-saver');
  win.setVisibleOnAllWorkspaces(true);

  if (isDev) {
    win.loadFile(path.resolve(__dirname, '../../dist/renderer/src/overlay/index.html'));
  } else {
    win.loadFile(path.join(__dirname, '../renderer/src/overlay/index.html'));
  }

  win.on('closed', () => { overlayWindow = null; });
  return win;
}

function createTray(): void {
  // macOS: "Template" suffix enables automatic dark/light mode adaptation
  const resourcesDir = isDev
    ? path.resolve(__dirname, '../../resources')
    : path.join(process.resourcesPath, 'resources');
  const iconPath = path.join(resourcesDir, 'tray-iconTemplate.png');
  let icon: Electron.NativeImage;
  if (fs.existsSync(iconPath)) {
    icon = nativeImage.createFromPath(iconPath);
    icon.setTemplateImage(true);
  } else {
    // Fallback: "LA" text icon
    icon = nativeImage.createFromDataURL('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAQ0lEQVQ4T2NkoBAwUqifAacBjP8ZGP4zMDL8Z2RkYPj/n+E/AwMDIwMjA+P//wyM/xkYGBn/MzD+Z2T8D1IHAMV+FAIAb6oOEYDsWkgAAAAASUVORK5CYII=');
  }

  tray = new Tray(icon);

  const contextMenu = Menu.buildFromTemplate([
    { label: '設定を開く', click: () => { showMainWindow(); } },
    { label: 'テストアラート', click: () => triggerTestAlert() },
    { label: 'アラート停止', click: () => stopCurrentAlert(false) },
    { type: 'separator' },
    { label: '設定フォルダを開く', click: () => { shell.openPath(getConfigDir()); } },
    { type: 'separator' },
    { label: '終了', click: () => { isQuitting = true; app.quit(); } },
  ]);

  tray.setToolTip('LiveAlert - 監視中');
  tray.setContextMenu(contextMenu);
  tray.on('double-click', () => {
    showMainWindow();
  });
}

// --- Alert handling ---
function showOverlay(item: AlertQueueItem): void {
  // Close existing overlay if position/size changed
  if (overlayWindow) {
    overlayWindow.close();
    overlayWindow = null;
  }
  overlayWindow = createOverlayWindow(
    currentConfig.options.bandHeightPx,
    currentConfig.options.bandPosition,
  );

  const message = item.alert.message.replace('{label}', item.alert.label);
  const alertData = {
    message,
    backgroundColor: item.alert.colors.background,
    textColor: item.alert.colors.text,
    bandPosition: currentConfig.options.bandPosition,
    bandHeightPx: currentConfig.options.bandHeightPx,
  };

  // Wait for overlay to load before sending data
  if (overlayWindow.webContents.isLoading()) {
    overlayWindow.webContents.once('did-finish-load', () => {
      overlayWindow?.webContents.send('show-alert', alertData);
    });
  } else {
    overlayWindow.webContents.send('show-alert', alertData);
  }
  overlayWindow.show();
}

function showOSNotification(item: AlertQueueItem): void {
  const message = item.alert.message.replace('{label}', item.alert.label);
  const notification = new Notification({
    title: 'LiveAlert',
    body: message,
    silent: false,
  });
  notification.on('click', () => {
    stopCurrentAlert(true);
  });
  notification.show();
}

function stopCurrentAlert(openTarget: boolean): void {
  if (autoStopTimer) {
    clearTimeout(autoStopTimer);
    autoStopTimer = null;
  }

  const targetUrl = openTarget && currentItem && !isSampleAlert(currentItem.alert.label)
      && /^[\w-]{11}$/.test(currentItem.videoId)
    ? `https://www.youtube.com/watch?v=${currentItem.videoId}`
    : null;

  if (overlayWindow) {
    overlayWindow.close();
    overlayWindow = null;
  }
  currentItem = null;

  sendToRenderer('status-update', { currentAlert: 'なし' });
  tray?.setToolTip('LiveAlert - 監視中');

  if (targetUrl) {
    shell.openExternal(targetUrl);
  }

  processQueue();
}

function processQueue(): void {
  if (currentItem) return;

  const next = alertQueue.dequeueNext();
  if (!next) return;

  currentItem = next;
  const label = next.alert.label || '(no label)';
  sendToRenderer('status-update', { currentAlert: label });
  tray?.setToolTip(`LiveAlert - アラート: ${label}`);

  // Audio: send to renderer for playback via Web Audio (respect audioMode)
  const audioMode = currentConfig.options.audioMode;
  if (audioMode !== 'off') {
    sendToRenderer('play-audio', {
      voice: next.alert.voice,
      voiceVolume: audioMode === 'manner' ? 0 : next.alert.voiceVolume,
      bgm: next.alert.bgm,
      bgmVolume: audioMode === 'manner' ? 0 : next.alert.bgmVolume,
    });
  }

  // Show alert based on display mode
  const displayMode = currentConfig.options.displayMode;
  if (displayMode === 'notification') {
    showOSNotification(next);
  } else if (displayMode !== 'off') {
    showOverlay(next);
  }

  // Auto-stop timer
  const duration = Math.max(1, currentConfig.options.maxAlarmDurationSec) * 1000;
  autoStopTimer = setTimeout(() => stopCurrentAlert(false), duration);
}

function triggerTestAlert(): void {
  const alert = currentConfig.alerts[0];
  if (!alert) return;
  const videoId = `test:${Date.now()}`;
  alertQueue.enqueue({
    alert,
    alertIndex: 0,
    videoId,
    detectedAt: Date.now(),
  });
  processQueue();
}

function isSampleAlert(label?: string): boolean {
  return label?.trim().toUpperCase() === 'SAMPLE';
}

function sendToRenderer(channel: string, data: any): void {
  mainWindow?.webContents.send(channel, data);
}

// --- Monitor events ---
monitor.on('alert', (event: AlertEvent) => {
  const label = event.alert.label || '(no label)';
  console.log(`[LiveAlert] Alert detected: ${label} videoId=${event.videoId}`);
  alertQueue.enqueue(event);
  processQueue();
});

monitor.on('ended', (videoId: string) => {
  alertQueue.removeByVideoId(videoId);
  if (currentItem?.videoId === videoId) {
    stopCurrentAlert(false);
  }
});

monitor.on('summary', (summary: MonitoringSummary) => {
  const timestamp = new Date().toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' });
  let text: string;
  if (summary.anyError) {
    text = `${timestamp} 監視に失敗しました`;
  } else if (summary.liveLabels.length > 0) {
    text = `${timestamp} 監視: ${summary.liveLabels.join(', ')} でLIVE検知`;
  } else {
    text = `${timestamp} 監視: LIVEなし`;
  }
  sendToRenderer('status-update', { statusText: text });
  tray?.setToolTip(`LiveAlert - ${text}`);
});

monitor.on('failure', (failure: MonitoringFailure) => {
  console.warn(`[LiveAlert] Monitoring failure: label=${failure.label} reason=${failure.reason}`);
});

monitor.on('debug', (message: string) => {
  console.log(`[LiveAlert] ${message}`);
});

// --- IPC handlers ---
ipcMain.handle('get-config-path', () => getConfigPath());
ipcMain.handle('get-assets-path', () => getAssetsPath());
ipcMain.handle('get-config-dir', () => getConfigDir());

ipcMain.handle('read-config', async () => {
  const configPath = getConfigPath();
  try {
    return await fsp.readFile(configPath, 'utf-8');
  } catch {
    return null;
  }
});

ipcMain.handle('write-config', async (_event, json: string) => {
  try {
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed.alerts) || !parsed.options) {
      return;
    }
    await fsp.writeFile(getConfigPath(), json, 'utf-8');
    currentConfig = parsed as ConfigRoot;
  } catch { /* invalid JSON, ignore */ }
});

ipcMain.handle('open-url', async (_event, url: string) => {
  if (typeof url === 'string' && /^https:\/\/(www\.)?youtube\.com\//i.test(url)) {
    shell.openExternal(url);
  }
});

ipcMain.handle('show-overlay', async (_event, alertData: any) => {
  if (!overlayWindow) {
    overlayWindow = createOverlayWindow(
      currentConfig.options.bandHeightPx,
      currentConfig.options.bandPosition,
    );
  }
  overlayWindow.webContents.send('show-alert', alertData);
  overlayWindow.show();
});

ipcMain.handle('hide-overlay', async () => {
  overlayWindow?.hide();
});

ipcMain.handle('stop-alert', async () => {
  stopCurrentAlert(false);
});

ipcMain.handle('test-alert', async () => {
  triggerTestAlert();
});

// --- Config import/export ---
ipcMain.handle('export-config', async () => {
  if (!mainWindow) return { success: false, error: 'ウィンドウが存在しません' };
  const result = await dialog.showSaveDialog(mainWindow, {
    title: '設定のエクスポート',
    defaultPath: 'config.json',
    filters: [{ name: 'JSON', extensions: ['json'] }],
  });
  if (result.canceled || !result.filePath) return { success: false };
  try {
    const configJson = await fsp.readFile(getConfigPath(), 'utf-8');
    await fsp.writeFile(result.filePath, configJson, 'utf-8');
    return { success: true };
  } catch (e: any) {
    return { success: false, error: e.message };
  }
});

ipcMain.handle('import-config', async () => {
  if (!mainWindow) return { success: false, error: 'ウィンドウが存在しません' };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: '設定のインポート',
    filters: [{ name: 'JSON', extensions: ['json'] }],
    properties: ['openFile'],
  });
  if (result.canceled || result.filePaths.length === 0) return { success: false };
  try {
    const json = await fsp.readFile(result.filePaths[0], 'utf-8');
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed.alerts) || !parsed.options) {
      return { success: false, error: '無効な設定ファイルです' };
    }
    await fsp.writeFile(getConfigPath(), json, 'utf-8');
    currentConfig = parsed as ConfigRoot;
    return { success: true, config: json };
  } catch (e: any) {
    return { success: false, error: e.message };
  }
});

// --- App lifecycle ---
app.on('ready', () => {
  currentConfig = loadConfig();

  createMainWindow();
  createTray();

  // Start monitoring
  console.log('[LiveAlert] Starting monitor...');
  monitor.start();
});

app.on('window-all-closed', () => {
  // Keep running in tray
});

app.on('activate', () => {
  if (mainWindow === null) {
    createMainWindow();
  } else {
    showMainWindow();
  }
});

app.on('before-quit', () => {
  isQuitting = true;
  monitor.stop();
});
