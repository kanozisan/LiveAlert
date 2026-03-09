import React from 'react';
import { AlertOptions } from '../../core/config-models';

interface Props {
  options: AlertOptions;
  onChange: (options: AlertOptions) => void;
}

export function SettingsPanel({ options, onChange }: Props) {
  const update = (partial: Partial<AlertOptions>) => {
    onChange({ ...options, ...partial });
  };

  return (
    <div className="settings-panel">
      <div className="form-row">
        <div className="form-group">
          <label>ポーリング間隔 (秒)</label>
          <input
            type="number"
            min={5}
            value={options.pollIntervalSec}
            onChange={(e) => update({ pollIntervalSec: Number(e.target.value) })}
          />
        </div>
        <div className="form-group">
          <label>アラーム最大時間 (秒)</label>
          <input
            type="number"
            min={1}
            value={options.maxAlarmDurationSec}
            onChange={(e) => update({ maxAlarmDurationSec: Number(e.target.value) })}
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label>バンド位置</label>
          <select
            value={options.bandPosition}
            onChange={(e) => update({ bandPosition: e.target.value as AlertOptions['bandPosition'] })}
          >
            <option value="top">上</option>
            <option value="center">中央</option>
            <option value="bottom">下</option>
          </select>
        </div>
        <div className="form-group">
          <label>バンド高さ (px)</label>
          <input
            type="number"
            min={100}
            value={options.bandHeightPx}
            onChange={(e) => update({ bandHeightPx: Number(e.target.value) })}
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label>表示モード</label>
          <select
            value={options.displayMode}
            onChange={(e) => update({ displayMode: e.target.value as AlertOptions['displayMode'] })}
          >
            <option value="alarm">バンド表示</option>
            <option value="notification">OS通知</option>
            <option value="manner">マナー</option>
            <option value="off">オフ</option>
          </select>
        </div>
        <div className="form-group">
          <label>音声モード</label>
          <select
            value={options.audioMode}
            onChange={(e) => update({ audioMode: e.target.value as AlertOptions['audioMode'] })}
          >
            <option value="alarm">アラーム</option>
            <option value="manner">マナー</option>
            <option value="off">オフ</option>
          </select>
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label>重複除外 (分)</label>
          <input
            type="number"
            min={1}
            max={30}
            value={options.dedupeMinutes}
            onChange={(e) => update({ dedupeMinutes: Number(e.target.value) })}
          />
        </div>
        <div className="form-group">
          <label>ループ間隔 (秒)</label>
          <input
            type="number"
            min={1}
            value={options.loopIntervalSec}
            onChange={(e) => update({ loopIntervalSec: Number(e.target.value) })}
          />
        </div>
      </div>

      <div className="form-group">
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={options.debugMode}
            onChange={(e) => update({ debugMode: e.target.checked })}
          />
          デバッグモード
        </label>
      </div>
    </div>
  );
}
