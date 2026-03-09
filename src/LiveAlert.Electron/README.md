# LiveAlert Electron版

YouTube のライブ開始をポーリングで検知し、通知と音声/BGM で知らせるクロスプラットフォーム版です。

## 主要ポイント
- YouTube: 公開ページのポーリング検知（公式 API 不使用）
- macOS / Windows 対応（Electron）
- 通知方式を選択可能: OS通知 / 警告バンド表示
- メニューバー（Mac）/ システムトレイ（Windows）常駐
- 設定のインポート/エクスポート対応

## 技術スタック
- Electron + TypeScript
- React (UI)
- Vite (ビルド)
- electron-builder (パッケージング)

## セットアップ

```bash
cd src/LiveAlert.Electron
npm install
```

## 開発

```bash
# ビルド＆起動
npm run build
npm run dev

# レンダラーのみビルド
npm run dev:renderer

# メインプロセスのみビルド＆起動
npm run dev:electron
```

## パッケージビルド

```bash
# Mac (.dmg + .zip)
npm run build:mac

# Windows (.exe + .zip)
npm run build:win

# 両方
npm run build:all
```

成果物は `release/` に出力されます。

## 主要設定
- 監視ポーリング間隔
- 最大鳴動時間 / 音声ループ時のウェイト
- 表示モード: バンド表示 / OS通知 / マナー / オフ
- 音声モード: アラーム / マナー / オフ
- バンドの位置（上 / 中央 / 下）と高さ
- 監視対象:
  - YouTube チャンネル URL / watch URL
  - 表示名 / メッセージ / 音声+BGM / 色

## ディレクトリ構成

```
src/
  main/           Electron メインプロセス
  core/           ビジネスロジック (YouTube検知, 監視, 設定管理)
  renderer/       React UI (設定画面)
  overlay/        警告バンド表示
assets/           音声ファイル, フォント
resources/        トレイアイコン
```

## ライセンス
MITライセンス
