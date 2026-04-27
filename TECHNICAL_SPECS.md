# VibeVoice Studio 技術仕様書 (Technical Specifications)

## 日本語 (Japanese)

### 1. システムアーキテクチャ
VibeVoice Studio は以下の 3 層構造で設計されています。
- **Frontend (WinUI 3)**: Windows App SDK を使用した UI 層。MVVM パターンにより、推論ロジックと UI を分離。
- **Backend (C# / .NET 8)**: 推論エンジンのオーケストレーション、G2P 処理、オーディオ再生、自動ダウンロード管理。
- **Inference Layer (ONNX Runtime)**: DirectML を介して、エクスポートされた ONNX モデルを実行。

### 2. G2P & トークナイゼーション
- **日本語**: `MeCab.DotNet` を使用して形態素解析を行い、読み（カナ）を取得。その後、音素に変換してモデル入力用の ID にマッピングします。
- **英語**: `G2P.Net` またはルールベースのトークナイザーを使用して、単語を音素 ID に変換します。

### 3. 自動ダウンロード機能
初回起動時に `models` フォルダが存在しない場合、`ModelDownloader` クラスが外部ストレージから必須の ONNX ファイル（Text Encoder, Diffusion, Vocoder）を自動的に取得し、進捗を UI に報告します。

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

### 4. Model I/O Specifications

| Component | Input Name | Type | Shape |
| :--- | :--- | :--- | :--- |
| **Text Encoder** | `input_ids` | `long` | `[batch, seq]` |
| | `attention_mask` | `long` | `[batch, seq]` |
| **Diffusion Decoder** | `cond` | `float` | `[batch, seq, 512]` |
| | `prompt` | `float` | `[batch, 1, 512]` |
| | `timesteps` | `float` | `[batch]` |
| **Vocoder** | `latents` | `float` | `[batch, 512, time]` |
