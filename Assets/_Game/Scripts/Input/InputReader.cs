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
        Vector2 lastTouchMove;

        public Vector2 JoystickOrigin { get; private set; }
        public Vector2 JoystickCurrent { get; private set; }
        public bool JoystickVisible { get; private set; }

        public void ResetState()
        {
            touchAutoResumeLeft = 0f;
            wasTouching = false;
            touchEngaged = false;
            lastTouchMove = Vector2.zero;
            JoystickVisible = false;
        }

        public InputFrame Read(float dt, Vector2 playerPos)
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
            bool usingTouch = false;
            var ts = Touchscreen.current;
            var touch = ts != null ? ts.primaryTouch : null;
            bool touching = touch != null && touch.press.isPressed;

            if (!usingKeyboard && touching)
            {
                usingTouch = true;
                touchEngaged = true;
                if (!wasTouching) touchAutoResumeLeft = 0f;
                wasTouching = true;
                Vector2 origin = touch.startPosition.ReadValue();
                Vector2 cur = touch.position.ReadValue();
                var delta = cur - origin;
                float d = delta.magnitude;
                if (d > cfg.joystickDeadzone)
                {
                    float mag = Mathf.Clamp01(d / cfg.joystickRadius);
                    move = delta / d * mag;
                }
                lastTouchMove = move;
                JoystickOrigin = origin;
                JoystickCurrent = cur;
                JoystickVisible = true;
                draw = true;
            }
            else if (!usingKeyboard && touchEngaged)
            {
                move = lastTouchMove;
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

            var mouse = Mouse.current;
            bool touchWaiting = touchEngaged && touchAutoResumeLeft > 0f;
            if (!usingKeyboard && !usingTouch && !touchWaiting && mouse != null && !touchEngaged)
            {
                var target = GameManager.I.Cam.ScreenToPlane(mouse.position.ReadValue());
                var delta = target - playerPos;
                float d = delta.magnitude;
                if (d > 4f)
                {
                    float t = Mathf.Clamp01(d / cfg.mouseFollowSlowRadius);
                    move = delta / d * t;
                }
            }
            if (mouse != null && mouse.rightButton.isPressed) draw = true;

            return new InputFrame
            {
                Move = new Vector2(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.y, -1f, 1f)),
                Draw = draw
            };
        }
    }
}
