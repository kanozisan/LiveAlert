# LiveAlert

YouTube のライブ開始をポーリングで検知し、X（旧Twitter）の Space は通知アクセスで検知して、警告帯・通知・音声/BGM で知らせる Android アプリです。

## 主要ポイント
- YouTube: 公開ページのポーリング検知（公式 API 不使用）
- X Space: 通知アクセスによる push 検知
- EVA 風の警告帯オーバーレイ + 音声/BGM

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

ライセンス: MIT（詳細は `assets/readme_android.txt` を参照）
