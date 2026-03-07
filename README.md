# LiveAlert

YouTube のライブ開始をポーリングで検知し、警告帯と音声/BGM で知らせるアプリです。Android 版は X（旧Twitter）の Space 通知検知にも対応し、Windows 版は YouTube 専用です。

## 主要ポイント
- YouTube: 公開ページのポーリング検知（公式 API 不使用）
- Android の X Space: 通知アクセスによる push 検知
- Windows: タスクトレイ常駐 + 設定ウィンドウ + 帯表示
- EVA 風の警告帯オーバーレイ + 音声/BGM

## 対応プラットフォーム
- Android: YouTube + X Space
- Windows (.NET8): YouTube のみ

## かんたん設定（Android）
1) インストールして起動  
2) **権限設定** を開き、以下を有効化  
   - 通知  
   - 通知アクセス（X Space 用）  
   - 他のアプリの上に表示  
3) **常駐ON/OFF** を ON

## 主要設定（抜粋）
- 監視ポーリング間隔（YouTube）
- X Space 重複通知抑止（分）
- 最大鳴動時間 / 音声ループ時のウェイト
- 帯の位置と高さ
- 監視対象:
  - サービス: `youtube` または `x_space`
  - YouTube: チャンネル URL / watch URL
  - X Space: 対象アカウントの表示名（部分一致）
  - 表示名 / メッセージ / 音声+BGM / 色

## 宣言権限
- SYSTEM_ALERT_WINDOW
- WAKE_LOCK
- FOREGROUND_SERVICE（+ MEDIA_PLAYBACK / DATA_SYNC）
- POST_NOTIFICATIONS（Android 13+）
- USE_FULL_SCREEN_INTENT

## ライセンス
- MITライセンス

## 変更履歴

- 2026/01/29  v0.11.0 初版公開
- 2026/02/01  v0.12.0 色設定をカラーピッカーに変更
- 2026/02/01  v0.12.0 背景色と文字色がきちんと反映されていない問題を修正（結果として同じconfigで見た目が変わります）
- 2026/02/01  v0.12.0 設定のエクスポート、インポート機能を実装
- 2026/02/02  v0.13.0 メン限のライブが拾えないケースがあったので検知方法を改善
- 2026/03/08  Windows (WPF) 版を追加（タスクトレイ常駐 / YouTube 専用）
