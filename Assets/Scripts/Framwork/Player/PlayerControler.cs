using System;
using Unity.VisualScripting;
using Unity.XR.Oculus.Input;
using UnityEngine.InputSystem;
using UnityEngine;

namespace Framework.Player
{
    public class PlayerControler:MonoBehaviour
    {
        
        public PlayerInputControler _playerControler;
        public Rigidbody2D rb;
        public Vector2 inputDirection;
        public GameObject player;
        public float speed = 0.05f;

        public void Awake()
        {
            _playerControler = new PlayerInputControler();
            rb = GetComponent<Rigidbody2D>();
        }
        public void OnEnable()
        {
            _playerControler.Enable();
        }
        
        public void OnDisable()
        {
            _playerControler.Disable();
        }

        public void Update()
        {
            inputDirection = _playerControler.Gameplay.Move.ReadValue<Vector2>().normalized;
        }

        public void FixedUpdate()
        {
            Move();
        }

        public void Move()
        {
            rb.velocity+=new Vector2()
        }
    }
}