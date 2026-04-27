# VibeVoice Studio (Standalone C# TTS)

<p align="center">
  <img src="VibeVoiceNative.UI/Assets/AppIcon.png" width="200" height="200" alt="VibeVoice App Icon">
</p>

[![Windows 11](https://img.shields.io/badge/OS-Windows%2011-blue)](https://www.microsoft.com/windows)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/download)
[![ONNX Runtime](https://img.shields.io/badge/Inference-ONNX%20Runtime-green)](https://onnxruntime.ai/)

## 日本語 (Japanese)

### 概要
VibeVoice Studio は、Microsoft の VibeVoice モデルをベースにした、高性能でポータブルな Windows 11 ネイティブ TTS（音声合成）アプリケーションです。Python ランタイムを一切必要とせず、C# と ONNX Runtime のみで動作するように設計されています。

### 最新のアップデート (v1.1.0)
- **究極の高速化**: TensorRT および CUDA をサポート。NVIDIA GPU 環境で圧倒的な推論速度を実現。
- **FP16 量子化**: VRAM 消費を抑えつつ、処理能力を向上。
- **ポータブル設計**: 依存関係をすべて内包した「自己完結型」バイナリ。

### セットアップ
1. 本リポジトリからポータブル版をダウンロードするか、`dotnet publish` でビルドします。
2. `VibeVoiceStudio.exe` を実行すると、必要なモデルのダウンロードが自動開始されます。

---

## English (English)

### Latest Updates (v1.1.0)
- **Ultimate Speed**: Support for TensorRT and CUDA. Unmatched inference speed on NVIDIA GPUs.
- **FP16 Quantization**: Faster processing with reduced VRAM footprint.
- **Portable Design**: "Self-contained" binary with all dependencies included.

### Setup
1. Download the portable version or build using `dotnet publish`.
2. Run `VibeVoiceStudio.exe`, and the models will be downloaded automatically.

## ✨ Features
- **Complete C# Native Inference Pipeline:** Uses `Microsoft.ML.OnnxRuntime` for true zero-shot TTS inference without Python dependencies.
- **Integrated Tokenizer:** Uses `Tokenizers.DotNet` to natively load Hugging Face `tokenizer.json` for Qwen2 BPE tokenization.
- **Native Audio Processing:** Implements real-time WAV resampling and 80-bin Mel Spectrogram extraction using `NAudio` and `MathNet.Numerics`.
- **Advanced Diffusion Solver:** Includes a native C# implementation of the Euler ODE solver loop for Flow Matching acoustic generation.
- **Automated Model Management:** Automatically downloads required ONNX files (including split `.data` weights) from Hugging Face on first launch with UI freeze protection.

## 📦 Download
Check the `publish_v1.1.0_new` directory for the fully self-contained portable Windows executable.

## License & Disclaimer
This project is for research and personal use only. Please respect the "Responsible AI" guidelines and ethical considerations regarding voice cloning.
