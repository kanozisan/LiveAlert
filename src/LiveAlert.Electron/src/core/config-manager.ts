import { ConfigRoot, createDefaultConfig } from './config-models';

export type ElectronAPI = {
  readConfig: () => Promise<string | null>;
  writeConfig: (json: string) => Promise<void>;
};

export class ConfigManager {
  private _current: ConfigRoot = createDefaultConfig();
  private _onReload?: (config: ConfigRoot) => void;

  get current(): ConfigRoot {
    return this._current;
  }

  set onReload(callback: (config: ConfigRoot) => void) {
    this._onReload = callback;
  }

  async load(api: ElectronAPI): Promise<void> {
    const json = await api.readConfig();
    if (!json) {
      await this.save(api, this._current);
      return;
    }
    try {
      const config = JSON.parse(json) as ConfigRoot;
      this._current = config;
      if (this._current.options.dedupeMinutes <= 0) {
        this._current.options.dedupeMinutes = 5;
      }
      this._onReload?.(this._current);
    } catch {
      // keep default
    }
  }

  async save(api: ElectronAPI, config: ConfigRoot): Promise<void> {
    this._current = config;
    const json = JSON.stringify(config, null, 2);
    await api.writeConfig(json);
  }
}
