import torch
import torch.nn as nn
import onnx
try:
    from onnxruntime.transformers.optimizer import optimize_model
    from onnxconverter_common import float16
except ImportError:
    print("Optimization libraries not found. Skipping optimization parts.")
import os

"""
VibeVoice ONNX Export Script
This script exports the sub-components of VibeVoice to ONNX format for C# / .NET 8 integration.
"""

def export_and_optimize(model, dummy_input, output_path, input_names, output_names, dynamic_axes):
    model.eval()
    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        input_names=input_names,
        output_names=output_names,
        dynamic_axes=dynamic_axes,
        opset_version=17,
        do_constant_folding=True
    )
    print(f"✅ Model exported: {output_path}")
    
    # FP16 量子化
    model_fp32 = onnx.load(output_path)
    model_fp16 = float16.convert_float_to_float16(model_fp32)
    fp16_path = output_path.replace(".onnx", "_fp16.onnx")
    onnx.save(model_fp16, fp16_path)
    print(f"🚀 Optimized to FP16: {fp16_path}")

def export_text_encoder(model, output_path):
    """
    Text Encoder (Qwen2.5 based) Export
    Input: input_ids [batch, seq_len], attention_mask [batch, seq_len]
    Output: last_hidden_state [batch, seq_len, hidden_size]
    """
    
    torch.onnx.export(
        model,
        (dummy_input_ids, dummy_mask),
        output_path,
        input_names=['input_ids', 'attention_mask'],
        output_names=['text_embeddings'],
        dynamic_axes={
            'input_ids': {0: 'batch', 1: 'sequence'},
            'attention_mask': {0: 'batch', 1: 'sequence'},
            'text_embeddings': {0: 'batch', 1: 'sequence'}
        },
        opset_version=17,
        do_constant_folding=True
    )
    print(f"✅ Text Encoder exported: {output_path}")

def export_prompt_encoder(model, output_path):
    """
    Reference (Prompt) Encoder Export
    Input: ref_audio [batch, 1, samples]
    Output: prompt_embeddings [batch, 1, hidden_size]
    """
    model.eval()
    dummy_audio = torch.randn(1, 1, 16000 * 5) # 5 seconds
    
    torch.onnx.export(
        model,
        (dummy_audio,),
        output_path,
        input_names=['ref_audio'],
        output_names=['prompt_embeddings'],
        dynamic_axes={
            'ref_audio': {0: 'batch', 2: 'samples'},
            'prompt_embeddings': {0: 'batch'}
        },
        opset_version=17,
        do_constant_folding=True
    )
    print(f"✅ Prompt Encoder exported: {output_path}")

def export_diffusion_decoder(model, output_path):
    """
    Flow Matching / Diffusion Decoder Export
    Input: 
        cond [batch, seq, dim], 
        prompt [batch, 1, dim], 
        timesteps [batch]
    Output:
        latents [batch, seq, dim]
    """
    model.eval()
    dummy_cond = torch.randn(1, 100, 512)
    dummy_prompt = torch.randn(1, 1, 512)
    dummy_ts = torch.tensor([0.5])
    
    torch.onnx.export(
        model,
        (dummy_cond, dummy_prompt, dummy_ts),
        output_path,
        input_names=['cond', 'prompt', 'timesteps'],
        output_names=['output_latents'],
        dynamic_axes={
            'cond': {0: 'batch', 1: 'sequence'},
            'prompt': {0: 'batch'},
            'output_latents': {0: 'batch', 1: 'sequence'}
        },
        opset_version=17,
        do_constant_folding=True
    )
    print(f"✅ Diffusion Decoder exported: {output_path}")

def export_vocoder(model, output_path):
    """
    Acoustic Decoder (Vocoder) Export
    Input: latents [batch, dim, time]
    Output: audio [batch, 1, samples]
    """
    model.eval()
    dummy_latents = torch.randn(1, 512, 100)
    
    torch.onnx.export(
        model,
        (dummy_latents,),
        output_path,
        input_names=['latents'],
        output_names=['audio'],
        dynamic_axes={
            'latents': {0: 'batch', 2: 'time'},
            'audio': {0: 'batch', 2: 'samples'}
        },
        opset_version=17,
        do_constant_folding=True
    )
    print(f"✅ Vocoder exported: {output_path}")

def convert_to_fp16(path):
    import onnx
    from onnxconverter_common import float16
    model = onnx.load(path)
    model_fp16 = float16.convert_float_to_float16(model)
    output_path = path.replace(".onnx", "_fp16.onnx")
    onnx.save(model_fp16, output_path)
    print(f"🚀 Optimized to FP16: {output_path}")

if __name__ == "__main__":
    print("VibeVoice ONNX Exporter starting...")
    # NOTE: Actual implementation requires loading the PyTorch state_dict
    # and instantiating the models. This is a template for the export logic.
    # Example:
    # model = VibeVoiceLLM()
    # model.load_state_dict(torch.load("vibevoice_llm.pt"))
    # export_text_encoder(model, "text_encoder.onnx")
