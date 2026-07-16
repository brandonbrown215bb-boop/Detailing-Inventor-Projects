using Inventor;

namespace SkinChannelPunch.Core
{
    internal sealed class FaceSketchResult
    {
        public PlanarSketch Sketch { get; set; }
        public WorkPlane TemporaryWorkPlane { get; set; }
        public bool UsesFaceFrameCoords { get; set; }
    }
}
