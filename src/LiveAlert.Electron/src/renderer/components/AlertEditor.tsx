import React, { useState } from 'react';
import { AlertConfig } from '../../core/config-models';

interface Props {
  index: number;
  alert: AlertConfig;
  onChange: (alert: AlertConfig) => void;
  onDelete: () => void;
}

export function AlertEditor({ index, alert, onChange, onDelete }: Props) {
  const [expanded, setExpanded] = useState(false);

  const update = (partial: Partial<AlertConfig>) => {
    onChange({ ...alert, ...partial });
  };

  const updateColors = (partial: Partial<typeof alert.colors>) => {
    onChange({ ...alert, colors: { ...alert.colors, ...partial } });
  };

  return (
    <div className="alert-editor" style={{ borderLeft: `4px solid ${alert.colors.background}` }}>
      <div className="alert-header" onClick={() => setExpanded(!expanded)}>
        <span className="alert-index">#{index + 1}</span>
        <span className="alert-label">{alert.label || '(未設定)'}</span>
        <span className="alert-service">{alert.service || 'youtube'}</span>
        <span className="expand-icon">{expanded ? '▼' : '▶'}</span>
      </div>

      {expanded && (
        <div className="alert-body">
          <div className="form-group">
            <label>サービス</label>
            <select value={alert.service || 'youtube'} onChange={(e) => update({ service: e.target.value })}>
              <option value="youtube">YouTube</option>
              <option value="x_space">X Space</option>
            </select>
          </div>

          <div className="form-group">
            <label>URL</label>
            <input
              type="text"
              value={alert.url}
              onChange={(e) => update({ url: e.target.value })}
              placeholder="https://www.youtube.com/channel/..."
            />
          </div>

          <div className="form-group">
            <label>ラベル</label>
            <input
              type="text"
              value={alert.label}
              onChange={(e) => update({ label: e.target.value })}
              placeholder="表示名"
            />
          </div>

          <div className="form-group">
            <label>メッセージ</label>
            <input
              type="text"
              value={alert.message}
              onChange={(e) => update({ message: e.target.value })}
              placeholder="警告　{label} がライブ開始"
            />
          </div>

          {alert.service === 'x_space' && (
            <div className="form-group">
              <label>タイトル含む文字列</label>
              <input
                type="text"
                value={alert.titleContains}
                onChange={(e) => update({ titleContains: e.target.value })}
              />
            </div>
          )}

          <div className="form-row">
            <div className="form-group">
              <label>音声ファイル</label>
              <input
                type="text"
                value={alert.voice}
                onChange={(e) => update({ voice: e.target.value })}
                placeholder="voice_live.wav"
              />
            </div>
            <div className="form-group small">
              <label>音量</label>
              <input
                type="number"
                min={0}
                max={100}
                value={alert.voiceVolume}
                onChange={(e) => update({ voiceVolume: Number(e.target.value) })}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>BGMファイル</label>
              <input
                type="text"
                value={alert.bgm}
                onChange={(e) => update({ bgm: e.target.value })}
                placeholder="bgm.mp3"
              />
            </div>
            <div className="form-group small">
              <label>音量</label>
              <input
                type="number"
                min={0}
                max={100}
                value={alert.bgmVolume}
                onChange={(e) => update({ bgmVolume: Number(e.target.value) })}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>背景色</label>
              <input
                type="color"
                value={alert.colors.background}
                onChange={(e) => updateColors({ background: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label>文字色</label>
              <input
                type="color"
                value={alert.colors.text}
                onChange={(e) => updateColors({ text: e.target.value })}
              />
            </div>
          </div>

          <div className="alert-actions">
            <button className="btn btn-danger" onClick={onDelete}>削除</button>
          </div>
        </div>
      )}
    </div>
  );
}
