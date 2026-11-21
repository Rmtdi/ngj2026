using System;
using Unity.VisualScripting;
using Unity.XR.Oculus.Input;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using Tools;
using System.Collections.Generic;
using MoveDirection = Tools.MoveDirection;

namespace Play
{
    public class PlayHandler : MonoBehaviour
    {

        public PlayerInputController _inputControler;
        public Rigidbody2D rb;
        public float speed;

        //用栈来存输入的按键，保证后输入的为当前方向
        private readonly List<MoveDirection> _pressed = new List<MoveDirection>();
        private MoveDirection? _current;
        private Vector3 _faceDir;

        public void OnEnable()
        {
            _inputControler.Enable();
        }

        public void OnDisable()
        {
            _inputControler.Disable();
        }

        public void Awake()
        {
            _inputControler = new PlayerInputController();
            rb = GetComponent<Rigidbody2D>();
            Bind();
        }

        void Bind()
        {
            var g = _inputControler.Gameplay;
            g.Up.performed += _ => Press(MoveDirection.Up);
            g.Down.performed += _ => Press(MoveDirection.Down);
            g.Left.performed += _ => Press(MoveDirection.Left);
            g.Right.performed += _ => Press(MoveDirection.Right);

            g.Up.canceled += _ => Release(MoveDirection.Up);
            g.Down.canceled += _ => Release(MoveDirection.Down);
            g.Left.canceled += _ => Release(MoveDirection.Left);
            g.Right.canceled += _ => Release(MoveDirection.Right);

            g.Detect.started += DetectWall;
        }

        private void DetectWall(InputAction.CallbackContext obj)
        {
            switch (_current)
            {
                case MoveDirection.Up :
                    _faceDir = new Vector3(0, 1);
                    break;
                case MoveDirection.Down:
                    _faceDir = new Vector3(0, -1);
                    break;
                case MoveDirection.Left:
                    _faceDir = new Vector3(-1, 0);
                    break;
                case MoveDirection.Right:
                    _faceDir = new Vector3(1, 0);
                    break;
            }
            if (Physics.Raycast(transform.position, _faceDir, out RaycastHit hit))
            {
                if (hit.transform.CompareTag("NormalWall"))
                {
                    Debug.Log("NormalWall");
                }
            }
        }

        void Press(MoveDirection dir)
        {
            _pressed.Remove(dir); // 重复按下（比如 W 和 ↑ 都映射 Up）也保证它排到最后
            _pressed.Add(dir);
            _current = dir;
        }

        void Release(MoveDirection dir)
        {
            _pressed.Remove(dir);
            _current = _pressed.Count > 0 ? _pressed[_pressed.Count - 1] : (MoveDirection?)null;
        }

        public void FixedUpdate()
        {
            rb.velocity = _current.HasValue ? ToVector(_current.Value) * speed : Vector2.zero;
        }

        static Vector2 ToVector(MoveDirection dir)
        {
            switch (dir)
            {
                case MoveDirection.Up:    return Vector2.up;
                case MoveDirection.Down:  return Vector2.down;
                case MoveDirection.Left:  return Vector2.left;
                case MoveDirection.Right: return Vector2.right;
                default:                  return Vector2.zero;
            }
        }
    }
}