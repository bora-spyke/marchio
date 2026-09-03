using UnityEngine;
using UnityEngine.InputSystem;

namespace Marchio
{
    public struct InputFrame
    {
        public Vector2 Move;
        public bool Draw;
    }

    public sealed class InputReader : MonoBehaviour
    {
        float touchAutoResumeLeft;
        bool wasTouching;
        bool touchEngaged;
        Vector2 mouseOrigin;
        bool mouseHeld;

        public Vector2 JoystickOrigin { get; private set; }
        public Vector2 JoystickCurrent { get; private set; }
        public bool JoystickVisible { get; private set; }

        public void ResetState()
        {
            touchAutoResumeLeft = 0f;
            wasTouching = false;
            touchEngaged = false;
            mouseHeld = false;
            JoystickVisible = false;
        }

        bool TryReadPointer(out Vector2 origin, out Vector2 current)
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed)
            {
                origin = touch.startPosition.ReadValue();
                current = touch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                current = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame || !mouseHeld) mouseOrigin = current;
                mouseHeld = true;
                origin = mouseOrigin;
                return true;
            }
            mouseHeld = false;
            origin = current = Vector2.zero;
            return false;
        }

        public InputFrame Read(float dt)
        {
            var cfg = GameManager.I.Config;
            var move = Vector2.zero;
            bool draw = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.spaceKey.isPressed) draw = true;
            }
            bool usingKeyboard = move.sqrMagnitude > 0f;
            if (usingKeyboard) move.Normalize();

            JoystickVisible = false;
            bool pointerDown = TryReadPointer(out var origin, out var current);

            if (!usingKeyboard && pointerDown)
            {
                touchEngaged = true;
                if (!wasTouching) touchAutoResumeLeft = 0f;
                wasTouching = true;
                var delta = current - origin;
                float d = delta.magnitude;
                if (d > cfg.joystickDeadzone)
                {
                    float mag = Mathf.Clamp01(d / cfg.joystickRadius);
                    move = delta / d * mag;
                }
                JoystickOrigin = origin;
                JoystickCurrent = current;
                JoystickVisible = true;
                draw = true;
            }
            else if (!usingKeyboard && touchEngaged)
            {
                if (wasTouching)
                {
                    wasTouching = false;
                    touchAutoResumeLeft = cfg.touchAutoResumeMs;
                }
                else if (touchAutoResumeLeft > 0f)
                {
                    touchAutoResumeLeft -= dt * 1000f;
                }
                else
                {
                    draw = true;
                }
            }

            return new InputFrame
            {
                Move = new Vector2(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.y, -1f, 1f)),
                Draw = draw
            };
        }
    }
}
