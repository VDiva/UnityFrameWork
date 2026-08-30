using UnityEngine;

#if WEIXINMINIGAME && !UNITY_EDITOR
using System;
using TMPro;
using UnityEngine.EventSystems;
using WeChatWASM;
#endif

namespace FrameWork
{
    /// <summary>
    /// 微信小游戏 TMP_InputField 虚拟键盘适配器。
    /// 自动监听当前 EventSystem 选中的输入框，无需逐个挂载到 UI Prefab。
    /// </summary>
    public sealed class WeChatMiniGameKeyboard : MonoBehaviour
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        private TMP_InputField _activeInput;
        private bool _keyboardVisible;
        private bool _switchingInput;

        private Action<OnKeyboardInputListenerResult> _onInput;
        private Action<OnKeyboardInputListenerResult> _onConfirm;
        private Action<OnKeyboardInputListenerResult> _onComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (FindFirstObjectByType<WeChatMiniGameKeyboard>() != null)
                return;

            var root = new GameObject(nameof(WeChatMiniGameKeyboard));
            DontDestroyOnLoad(root);
            root.AddComponent<WeChatMiniGameKeyboard>();
        }

        private void Awake()
        {
            _onInput = OnKeyboardInput;
            _onConfirm = OnKeyboardConfirm;
            _onComplete = OnKeyboardComplete;
            WX.OnKeyboardInput(_onInput);
            WX.OnKeyboardConfirm(_onConfirm);
            WX.OnKeyboardComplete(_onComplete);
        }

        private void Update()
        {
            if (_switchingInput)
                return;

            TMP_InputField selected = GetSelectedInputField();
            if (ReferenceEquals(selected, _activeInput))
                return;

            if (_keyboardVisible)
            {
                _switchingInput = true;
                _activeInput = null;
                WX.HideKeyboard(new HideKeyboardOption
                {
                    complete = _ =>
                    {
                        _keyboardVisible = false;
                        _switchingInput = false;
                        if (selected != null && selected.isActiveAndEnabled)
                            ShowKeyboard(selected);
                    }
                });
                return;
            }

            if (selected != null)
                ShowKeyboard(selected);
        }

        private static TMP_InputField GetSelectedInputField()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            return selected == null ? null : selected.GetComponentInParent<TMP_InputField>();
        }

        private void ShowKeyboard(TMP_InputField input)
        {
            _activeInput = input;
            input.ActivateInputField();

            bool isNumber = input.contentType == TMP_InputField.ContentType.IntegerNumber ||
                            input.contentType == TMP_InputField.ContentType.DecimalNumber;
            bool isMultiple = input.lineType != TMP_InputField.LineType.SingleLine;
            WX.ShowKeyboard(new ShowKeyboardOption
            {
                defaultValue = input.text ?? string.Empty,
                maxLength = input.characterLimit > 0 ? input.characterLimit : 140,
                multiple = isMultiple,
                confirmHold = false,
                confirmType = isMultiple ? "done" : "send",
                keyboardType = isNumber ? "number" : "text",
                success = _ => _keyboardVisible = true,
                fail = result =>
                {
                    _keyboardVisible = false;
                    _activeInput = null;
                    Debug.LogError($"微信虚拟键盘打开失败：{result.errMsg}");
                }
            });
        }

        private void OnKeyboardInput(OnKeyboardInputListenerResult result)
        {
            SetInputText(result?.value);
        }

        private void OnKeyboardConfirm(OnKeyboardInputListenerResult result)
        {
            SetInputText(result?.value);
            if (_activeInput == null)
                return;

            TMP_InputField input = _activeInput;
            input.onSubmit?.Invoke(input.text);
            input.DeactivateInputField();
            Deselect(input);
            _activeInput = null;
            _keyboardVisible = false;
        }

        private void OnKeyboardComplete(OnKeyboardInputListenerResult result)
        {
            SetInputText(result?.value);
            if (_activeInput != null)
            {
                TMP_InputField input = _activeInput;
                input.DeactivateInputField();
                Deselect(input);
            }
            _activeInput = null;
            _keyboardVisible = false;
        }

        private void SetInputText(string value)
        {
            if (_activeInput == null || !_activeInput.isActiveAndEnabled)
                return;

            _activeInput.text = value ?? string.Empty;
            _activeInput.caretPosition = _activeInput.text.Length;
        }

        private static void Deselect(TMP_InputField input)
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.GetComponentInParent<TMP_InputField>() == input)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            if (_onInput != null)
                WX.OffKeyboardInput(_onInput);
            if (_onConfirm != null)
                WX.OffKeyboardConfirm(_onConfirm);
            if (_onComplete != null)
                WX.OffKeyboardComplete(_onComplete);
        }
#endif
    }
}
