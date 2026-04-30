# VibeVoice Studio Ultimate

VibeVoice Studio Ultimate は、最先端の AI 音声合成エンジン「VibeVoice」を最大限に活用するための、プロフェッショナル向けデスクトップアプリケーションです。

## 概要
わずか数秒のリファレンス音声から、その話者の特徴（声質、イントネーション）を捉えた高品質な音声を生成する「Zero-shot TTS」を実現します。さらに、ライブ配信、動画制作、外部ツール連携に特化した究極の機能を備えています。

## 主な機能
- **超高速推論**: CPU、DirectML (AMD/Intel GPU)、CUDA (NVIDIA GPU) に対応。FP16 最適化による爆速生成。
- **リアルタイム・スペクトログラム**: 生成中の音声を周波数分布として可視化するモダンな UI。
- **台本形式の一括書き出し**: `[話者名] セリフ` 形式のテキストを解析し、自動で話者を切り替えて並列生成。
- **外部連携 API サーバー**: HTTP/REST 経由で他のアプリから音声合成を呼び出し可能（デフォルト: 5050ポート）。
- **ユーザー辞書機能**: 固有名詞や特殊な読みを自由に登録・管理。
- **インテリジェント音声前処理**: 無音カット、ノーマライズを自動適用し、常に最高の品質で出力。

## セットアップ
1. **モデルの配置**: `models/` フォルダに VibeVoice ONNX モデル群（v4推奨）を配置してください。
   - 公式モデル配布先: [Hugging Face - iwa-kasoutuuuuuka/VibeVoice](https://huggingface.co/iwa-kasoutuuuuuka/VibeVoice)
2. **辞書の配置**: `dic/` フォルダに MeCab 用の辞書（ipadic 等）を配置してください。
3. **実行**: `publish/VibeVoiceStudio.bat` を実行して起動します。

## 公式リンク
- **Model Repository**: [iwa-kasoutuuuuuka/VibeVoice](https://huggingface.co/iwa-kasoutuuuuuka/VibeVoice)
- **Developer**: [iwa-kasoutuuuuuka](https://github.com/iwa-kasoutuuuuuka)

## 動作環境
- OS: Windows 10/11 (x64)
- Runtime: .NET 8.0
- GPU 推奨: DirectX 12 互換 (DirectML) または NVIDIA GPU (CUDA)

---
© 2026 VibeVoice Studio Project. Powered by VibeVoice Engine.
