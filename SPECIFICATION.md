# VibeVoice Studio Ultimate 技術仕様書

## 1. システムアーキテクチャ
VibeVoice Studio Ultimate は、C# / WinUI 3 をベースとしたデスクトップアプリケーションであり、推論エンジンには ONNX Runtime を採用しています。

### 構成コンポーネント
- **UI 層 (WinUI 3)**: モダンな Fluent Design を採用したユーザーインターフェース。
- **推論層 (Inference Engine)**: ONNX Runtime によるマルチデバイス（CPU/GPU）推論。
- **NLP 層 (Text Processing)**: MeCab および Tokenizers.DotNet による高度なテキスト解析。
- **API 層 (REST Server)**: 外部連携用の軽量 HTTP サーバー。
- **ローカライズ層**: Resources.resw による日本語/英語の動的切り替え。

## 2. 推論エンジン仕様
### 対応モデル: VibeVoice-0.5B-v4 (ONNX)
以下の 7 つのセッションを連結して推論を行います：
1. `text_encoder.onnx`: テキストの埋め込み表現生成
2. `tts_lm_prefill.onnx`: 言語モデルの初期推論
3. `tts_lm_step.onnx`: 言語モデルの逐次推論（オートリグレッシブ）
4. `text_to_condition.onnx`: 条件付けベクトルの生成
5. `prediction_head.onnx`: 音響特徴量の予測
6. `acoustic_decoder.onnx`: メルスペクトログラムから波形への変換（Vocoder）
7. `acoustic_connector.onnx`: 各層の接続

### 高速化技術
- **FP16 推論**: GPU 使用時に半精度モデルを優先利用。
- **Parallel Generation**: バッチ処理時の複数文同時生成。
- **Streaming Support**: 生成された音声チャンクを逐次再生し、初動レイテンシを最小化。
- **CFM Steps Adjustment**: 4〜50ステップの範囲で品質と速度を調整可能。

## 3. 外部連携 API (REST)
デフォルトポート: `5050`

### エンドポイント: `GET /tts`
| パラメータ | 説明 | 例 |
| :--- | :--- | :--- |
| `text` | 合成するテキスト | `こんにちは` |
| `voice` | 話者名（VoiceGallery登録名） | `Speaker1` |

**レスポンス**: `audio/wav` 形式のバイナリデータ。

## 4. ディレクトリ構造
```text
VibeVoiceStudio/
├── bin/                    # 実行バイナリ
├── models/                 # ONNXモデルファイル群 (.onnx, .onnx.data)
├── dic/                    # MeCab 用辞書ファイル
├── logs/                   # 動作ログ (ui_.log, engine_.log)
├── Assets/                 # アプリ用アセット（アイコン等）
└── VibeVoiceStudio.bat     # 起動用ランチャー
```

## 5. 依存ライブラリ
- **Microsoft.ML.OnnxRuntime.DirectML / CUDA**: 推論エンジン
- **SkiaSharp**: スペクトログラム描画
- **NAudio**: 音声再生および WAV 生成
- **CommunityToolkit.Mvvm**: MVVM パターン
- **Serilog**: 構造化ログ

---
仕様書最終更新日: 2026年5月7日
