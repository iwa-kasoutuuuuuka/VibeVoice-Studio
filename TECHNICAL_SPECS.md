# VibeVoice Studio 技術仕様書 (Technical Specifications)

## 日本語 (Japanese)

### 1. システムアーキテクチャ
VibeVoice Studio は以下の 3 層構造で設計されています。
- **Frontend (WinUI 3)**: Windows App SDK を使用した UI 層。`ResourceLoader` による多言語対応（日本語/英語）を実現。
- **Backend (C# / .NET 8)**: `VibeVoicePipeline` による推論のパイプライン制御。ストリーミング再生（`IAsyncEnumerable`）に対応。
- **Inference Layer (ONNX Runtime)**: 7 つの ONNX モデルを組み合わせた複雑な推論パイプラインを実行。

### 2. G2P & トークナイゼーション
- **日本語**: `MeCab.DotNet` を使用。ポータブル環境向けに辞書パス（`dic/ipadic`）の自動探索ロジックを搭載。
- **英語**: 単語レベルでの簡易的な音素マッピングおよびルールベースの正規化を適用。
- **トークナイザー**: `Tokenizers.DotNet` (HuggingFace tokenizers wrapper) を使用。

### 3. ストリーミング推論
推論エンジンは `GenerateAudioStreamingAsync` を提供し、生成されたオーディオチャンクを逐次 UI（AudioPlayer）に転送することで、長いテキストでも低遅延で再生を開始できます。

---

## English (English)

### 1. System Architecture
VibeVoice Studio is designed with a 3-layer architecture:
- **Frontend (WinUI 3)**: UI layer using Windows App SDK. MVVM pattern separates inference logic from the UI.
- **Backend (C# / .NET 8)**: Orchestrates the inference engine, G2P processing, audio playback, and automatic download management.
- **Inference Layer (ONNX Runtime)**: Executes exported ONNX models via DirectML for hardware acceleration.

### 2. G2P & Tokenization
- **Japanese**: Uses `MeCab.DotNet` for morphological analysis to get readings (Kana), which are then converted to phonemes and mapped to model input IDs.
- **English**: Uses `G2P.Net` or a rule-based tokenizer to convert words into phoneme IDs.

### 3. Automatic Download Feature
If the `models` folder is missing on the first run, the `ModelDownloader` class automatically retrieves the required ONNX files (Text Encoder, Diffusion, Vocoder) from external storage and reports progress to the UI.

### 4. Model I/O Specifications (v1.1.1)

| Component | Filename | Description |
| :--- | :--- | :--- |
| **Text Encoder** | `text_encoder.onnx` | Converts tokens to hidden states. |
| **LM Prefill** | `tts_lm_prefill.onnx` | Initial stage of AR Language Model. |
| **LM Step** | `tts_lm_step.onnx` | Iterative stage of AR Language Model. |
| **Text to Cond** | `text_to_condition.onnx` | Maps text features to conditioning vectors. |
| **Prediction Head** | `prediction_head.onnx` | Predicts acoustic features (F0, duration). |
| **Acoustic Connector**| `acoustic_connector.onnx`| Bridges LM and Vocoder features. |
| **Acoustic Decoder** | `acoustic_decoder.onnx` | Vocoder (GAN/Diffusion based). |
