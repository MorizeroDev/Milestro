using UnityEngine;

namespace Milestro.Model
{
    [CreateAssetMenu(fileName = "TextGradient", menuName = "Milestro/Text Gradient")]
    public sealed class TextGradientAsset : ScriptableObject
    {
        [SerializeField]
        private TextGradient m_gradient = TextGradient.White;

        public TextGradient Gradient
        {
            get => m_gradient;
            set => m_gradient = value;
        }
    }
}
