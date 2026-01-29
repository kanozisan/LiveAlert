
-----------------------------------------------------------
　　LiveAlert (Android)
-----------------------------------------------------------

LiveAlert は、以下の2種の監視を行い、警告帯と通知/音声で知らせる Android 向けのアプリです。
-  YouTube ライブ配信の開始をポーリング検知（公式 API 不使用）
- X（旧Twitter）の Space は通知（push）から検知

■　特徴
- EVA 風の警告帯オーバーレイ表示
- 音声/BGM の同時再生

■　インストール/起動
- 初回起動後、権限設定ボタンから必要な権限/設定を有効化

■　設定方法

1) 監視の開始/停止
- 画面上部の 「常駐ON/OFF」 を切り替えます。

2) 通知・画面表示・音声の動作
- 通知：アラーム / マナー / OFF
- 画面表示：アラーム / マナー / OFF
- 音声再生：アラーム / マナー / OFF

- アラーム：強い通知・ロック画面表示・アラーム用音声など、寝ていても気づける動作
- マナー：通常のアプリと同等の動作（マナーモード時は音が鳴らない可能性があります）
- OFF：その項目は無効（通知なし／画面表示なし／音声なし）

3) 監視の間隔と帯の表示
- 監視ポーリング間隔（秒）：youtubeを監視する間隔
- X Space 重複通知抑止（分）：同じ通知の連続発報を抑制する時間
- 最大鳴動時間（秒）：鳴らし続ける最大時間
- 音声ループ時のウェイト（秒）：音はループ再生されますが、ループとループの間の待ち時間
- アラーム帯の位置：top / bottom
- アラーム帯の高さ：スライダーで調整

4) 監視対象設定
- 設定できる項目：
  - サービス（YouTube / X Space）
  - URL / 通知タイトル（表示名） / 表示名
    - YouTube: チャンネルURL または watch URL を入力
      例: https://www.youtube.com/@xxxxx / https://www.youtube.com/channel/XXXX / https://www.youtube.com/watch?v=YYYY
    - X Space: 「通知アカウント名（表示名）」に、対象アカウントの表示名を入力（部分一致）
  - メッセージ（{label} は監視名に置換）
  - 音声ファイル / BGM ファイル
  - 音声の音量 / BGMの音量（スライダー）・テスト再生
  - 背景色 / 文字色
  - 削除

5) そのほかのボタン
- ログ出力：動作ログをファイルに保存します
- 権限設定：必要な権限画面を開きます（通知・通知アクセス・他のアプリの上に表示）
- テスト発報：今の設定でアラートをテストします
- このプログラムについて：アプリの説明を表示します
- 外部ライセンス：同梱フォント/音源のライセンスを確認できます

■　権限と注意
Android ビルドで宣言している権限:
- SYSTEM_ALERT_WINDOW（オーバーレイ表示）
- WAKE_LOCK（画面点灯維持）
- FOREGROUND_SERVICE / FOREGROUND_SERVICE_MEDIA_PLAYBACK / FOREGROUND_SERVICE_DATA_SYNC
- POST_NOTIFICATIONS（Android 13+ 通知）
- USE_FULL_SCREEN_INTENT（ロック中の全画面通知）

■　補足
- 端末のバッテリー最適化/省電力設定により、常駐監視が停止される場合があります。
- 設定画面の「権限設定」から各設定画面を開けます。

■　ライセンス
- このプログラムのライセンスはMITライセンスとします。
- 同梱フォント・音源のライセンスはアプリ内の「外部ライセンス」から確認できます。

MIT License

Copyright (c) 2026 yasagurebug

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
