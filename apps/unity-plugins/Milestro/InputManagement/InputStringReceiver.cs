#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define ENABLE_INPUT_SYSTEM_ONLY
#endif

using UnityEngine;
#if ENABLE_INPUT_SYSTEM_ONLY
using System;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Milestro.InputManagement
{
    public class InputStringReceiver : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM_ONLY
        [NonSerialized] private string _compositionString = "";
        [NonSerialized] private readonly StringBuilder _inputStringCache = new();
#endif

        // ReSharper disable once MemberCanBeMadeStatic.Global
        public string CompositionString
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.compositionString;
#elif ENABLE_INPUT_SYSTEM
                return _compositionString;
#endif
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Global
        public string InputString
        {
            get
            {
                {
#if ENABLE_LEGACY_INPUT_MANAGER
                    return Input.inputString;
#elif ENABLE_INPUT_SYSTEM
                    var str = _inputStringCache.ToString();
                    _inputStringCache.Clear();
                    return str;
#endif
                }
            }
        }

        void Start()
        {
#if ENABLE_INPUT_SYSTEM_ONLY
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput += OnTextInput;
                keyboard.onIMECompositionChange += OnIMECompositionChanged;
            }
#endif
        }

        private void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM_ONLY
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput -= OnTextInput;
                keyboard.onIMECompositionChange -= OnIMECompositionChanged;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM_ONLY
        private void OnTextInput(char character)
        {
            _inputStringCache.Append(character);
        }

        private void OnIMECompositionChanged(IMECompositionString compositionString)
        {
            _compositionString = compositionString.ToString();
        }
#endif
    }
}