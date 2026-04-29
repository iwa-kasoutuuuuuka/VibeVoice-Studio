import onnx
import os

def print_io(model_path):
    if not os.path.exists(model_path):
        print(f"File not found: {model_path}")
        return
    try:
        model = onnx.load(model_path, load_external_data=False)
        print(f"\n--- {os.path.basename(model_path)} ---")
        print("Inputs:")
        for input in model.graph.input:
            shape = []
            for dim in input.type.tensor_type.shape.dim:
                shape.append(dim.dim_value if dim.dim_value > 0 else dim.dim_param)
            print(f"  {input.name}: {shape}")
        print("Outputs:")
        for output in model.graph.output:
            shape = []
            for dim in output.type.tensor_type.shape.dim:
                shape.append(dim.dim_value if dim.dim_value > 0 else dim.dim_param)
            print(f"  {output.name}: {shape}")
    except Exception as e:
        print(f"Error loading {model_path}: {e}")

base_dir = r'e:\app\VibeVoiceStudio\VibeVoiceNative.UI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\models'
if not os.path.exists(base_dir):
    # Try another path
    base_dir = r'e:\app\VibeVoiceStudio\models'

print(f"Searching in: {base_dir}")
for f in ["text_encoder.onnx", "tts_lm_prefill.onnx", "tts_lm_step.onnx"]:
    print_io(os.path.join(base_dir, f))
