using UnityEngine;
using UnityEngine.UI;

namespace Vr
{
    [RequireComponent(typeof(Text))]
    public sealed class VrKeyLabel : MonoBehaviour
    {
        private void Awake()
        {
            var label = GetComponent<Text>();
            label.text = VrKeyNames.Translate(label.text);
        }
    }
}
