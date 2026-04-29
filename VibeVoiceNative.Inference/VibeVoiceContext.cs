using Microsoft.ML.OnnxRuntime.Tensors;

namespace VibeVoiceNative.Inference
{
    public class VibeVoiceContext
    {
        public DenseTensor<float>? PastKeys { get; set; }
        public DenseTensor<float>? PastValues { get; set; }
        public DenseTensor<float>? LastHidden { get; set; }
        public int TotalSteps { get; set; }

        public void Reset()
        {
            PastKeys = null;
            PastValues = null;
            LastHidden = null;
            TotalSteps = 0;
        }
    }
}
