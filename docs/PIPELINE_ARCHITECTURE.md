# VibeVoice Studio 推論パイプライン・アーキテクチャ & デザインコンセプト

`VibeVoicePipeline.cs` に実装されている、7つの ONNX モデルを組み合わせた高度な音声合成プロセスと、次世代 UI のビジョンをまとめたドキュメントです。

## 1. 推論パイプライン全体図 (Internal Architecture)

```mermaid
graph TD
    %% 入力層
    Input[ユーザー入力テキスト] --> G2P[Text Processor / MeCab]
    G2P --> Tokens([Token Sequence])
    
    %% テキスト処理層
    subgraph "Text Processing Layer"
        Tokens --> TextCond[text_to_condition.onnx]
        TextCond --> Embeds([Text Embeddings])
    end

    %% 言語モデル層 (AR Loop)
    subgraph "Autoregressive LM Layer"
        Embeds --> Prefill[tts_lm_prefill.onnx]
        Prefill --> LM_Hidden([LM Hidden States])
        Prefill --> KV_Cache[(KV Cache)]
        
        LM_Hidden --> StepLoop{AR Step Loop}
        StepLoop --> Step[tts_lm_step.onnx]
        Step --> KV_Cache
        Step --> StepLoop
        StepLoop --> CondList([Conditioning Sequence])
    end

    %% 音響条件付け層
    subgraph "Acoustic Conditioning"
        VoiceLatent[Speech Latent / Noise] --> AcousticConn[acoustic_connector.onnx]
        AcousticConn --> VoiceCond([Voice Conditioning])
    end

    %% CFM / Diffusion 層
    subgraph "CFM Diffusion Layer (ODE Solver)"
        CondList & VoiceCond --> AddCond[Conditioning Merger]
        AddCond --> PredHead[prediction_head.onnx]
        GaussianNoise[Gaussian Noise] --> DiffusionLoop{Diffusion Steps / 50 steps}
        DiffusionLoop --> PredHead
        PredHead --> Velocity([Velocity / Gradient])
        Velocity --> DiffusionLoop
        DiffusionLoop --> AcousticLatent([Acoustic Latents])
    end

    %% ボコーダー層
    subgraph "Acoustic Decoding"
        AcousticLatent --> Vocoder[acoustic_decoder.onnx / Vocoder]
        Vocoder --> RawAudio([Raw Waveform])
        RawAudio --> PostProcess[Audio Effects / Speed / Pitch]
    end

    %% 出力層
    PostProcess --> FinalOutput[[Final Audio Output]]

    %% スタイリング
    style Input fill:#f9f,stroke:#333,stroke-width:2px
    style FinalOutput fill:#00ff0033,stroke:#333,stroke-width:4px
    style KV_Cache fill:#ffcc0033,stroke:#f39c12
    style StepLoop fill:#3498db33,stroke:#2980b9
    style DiffusionLoop fill:#e74c3c33,stroke:#c0392b
```

## 2. 次世代 UI デザインコンセプト (Ultimate Edition)

推論性能の向上に伴い、ユーザーインターフェースも「プレミアムかつ直感的」な体験を提供することを目指しています。

![VibeVoice Studio Ultimate UI](images/vibevoice_studio_ultimate_ui.png)

### デザインの柱
- **Rich Aesthetics**: ダークモードを基調とし、ネオンカラーのアクセントとグラスモフィズム（透かし効果）を多用。
- **Dynamic Feedback**: CFM 推論の状態をリアルタイムで波形化し、生成のプロセスを視覚的に楽しむことが可能。
- **State-of-the-art Design**: WinUI 3 のポテンシャルを最大限に引き出した、モダンなデスクトップアプリの理想形。

---

## 3. 実装上の注意 (Implementation Notes)
- **KV Cache**: `tts_lm_step.onnx` 実行時に過去の隠れ状態を保持することで、長い文章でも高速な推論を維持。
- **Regularization**: `Tanh` 関数によるスケーリングを各層の接続部に適用し、数値的な安定性を確保。

---
最終更新日: 2026年5月8日
