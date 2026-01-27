using PureDOTS.Runtime.Config;

namespace PureDOTS.Runtime.Presentation
{
    public static class PresentationBindingConfigVars
    {
        [RuntimeConfigVar("presentation.binding.sample", "graybox-minimal", Flags = RuntimeConfigFlags.Save, Description = "Select the presentation binding sample to load (graybox-minimal|graybox-fancy).")]
        public static RuntimeConfigVar BindingSample;
    }
}
