import onnx
import sys

def print_io(model_path):
    try:
        model = onnx.load(model_path, load_external_data=False)
        print(f"\n--- {model_path} ---")
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

print_io(r'e:\app\VibeVoiceStudio\publish_v1.1.0\models\text_encoder.onnx')
print_io(r'e:\app\VibeVoiceStudio\publish_v1.1.0\models\acoustic_connector.onnx')
print_io(r'e:\app\VibeVoiceStudio\publish_v1.1.0\models\acoustic_decoder.onnx')
