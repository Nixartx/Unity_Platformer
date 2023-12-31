using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


public class Player : MonoBehaviour
{
    private Rigidbody2D _playerRb;
    public float speed;
    public float jumpForce;
    private bool _isJumpPressed;
    private float _axisX;
    private CharacterUIController _controller;
    private List<Arrow> _arrowsPull = new();
    private int _arrowCount = 3;
    [SerializeField] private GroundDetection _groundDetection;
    [SerializeField] private Arrow _arrow;
    [SerializeField] private Transform _arrowSpawner;
    [SerializeField] private GameObject _swordTrigger;
    [SerializeField] private float _cooldown = 1;
    [SerializeField] private Health _health;
    [SerializeField] private BuffReciever _buffReciever;
    [SerializeField] private AudioSource _swordSwishSound;
    [SerializeField] private AudioSource _jumpSound;

    public Health Health
    {
        get { return _health; }
    }
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    private float bonusDamage;
    
    private bool _isCooldown;
    private bool _isJumping;
    private bool _isFalling;
    private float _timeSinceFalling = 0;
    private bool _isShooting;
    private int  _currentAttack = 0;
    private float _timeSinceAttack = 0;

    private void Start()
    {
        _playerRb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        InitCharacterUIController();
        
        for (var i = 0; i < _arrowCount; i++)
        {
            var tempArrow = Instantiate(_arrow, _arrowSpawner);
            _arrowsPull.Add(tempArrow);
            tempArrow.gameObject.SetActive(false);
        }

        _buffReciever.OnBuffsChanged += ApplyBuffs;
    }

    public void InitCharacterUIController()
    {
        _controller = GameManager.Instance.CharacterUIController;
        _controller.JumpBtn.onClick.AddListener(JumpCall);
        _controller.FireBtn.onClick.AddListener(ShootCall);
    }

    private void ApplyBuffs()
    {
        var damageBuff = _buffReciever.Buffs.Find(t => t.type == BuffType.Damage);
        bonusDamage = damageBuff == null ? 0 : damageBuff.additiveBinus;
        
        var healthBuff = _buffReciever.Buffs.Find(t => t.type == BuffType.Health);
        var bonusHealth = healthBuff == null ? 0 : healthBuff.additiveBinus;
        _health.SetHealth((int)bonusHealth);
    }

    private void Update()
    {
        if (_controller.LeftBtn.IsPressed)
            _axisX = -1;
        else if (_controller.RightBtn.IsPressed)
            _axisX = 1;
        else _axisX = 0;

        if (_isFalling)
            _timeSinceFalling += Time.deltaTime;


        _timeSinceAttack += Time.deltaTime;
        if (_controller.AttackBtn.IsPressed && _timeSinceAttack > 0.3f)
        {
            AttackCall();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
            GameManager.Instance.OnCallMenu();
        
#if UNITY_EDITOR
        if (!_controller.LeftBtn.IsPressed && !_controller.RightBtn.IsPressed)
            _axisX = Input.GetAxisRaw("Horizontal");
        
        if (Input.GetButtonDown("Jump"))
            JumpCall();
        if (Input.GetKeyDown(KeyCode.F))
            ShootCall();
        if (Input.GetKey(KeyCode.Return) && _timeSinceAttack > 0.3f)
            AttackCall();
        
        if (Input.GetKeyDown(KeyCode.I))
            GameManager.Instance.OnClickPause();
#endif
    }
    
    private void FixedUpdate()
    {
        _playerRb.velocity = new Vector2(_axisX * speed, _playerRb.velocity.y);

        _animator.SetBool("IsGrounded", _groundDetection.IsGrounded);
        _animator.SetFloat("Speed", Mathf.Abs(_axisX * speed));
        if (_axisX > 0)
            _spriteRenderer.flipX = false;
        if (_axisX < 0)
            _spriteRenderer.flipX = true;
        
        _isJumping = _isJumping && !_groundDetection.IsGrounded;
        if (_isJumpPressed && _groundDetection.IsGrounded)
        {
            _jumpSound.Play();
            _animator.SetTrigger("StartJump");
            _isJumping = true;
            _playerRb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            _isJumpPressed = false;
        }
        StartFallTrigger();
        CheckArrow();

        GameManager.Instance.PlayerMoveDirection = _axisX;
    }

    private void JumpCall()
    {
        if ( _groundDetection.IsGrounded)
            _isJumpPressed = true;
    }
    private void ShootCall()
    {
        if (!_isShooting)
            _isShooting = true;
    }

    private void AttackCall()
    {
        _currentAttack++;

        // Loop back to one after third attack
        if (_currentAttack > 3)
            _currentAttack = 1;

        // Reset Attack combo if time since last attack is too large
        if (_timeSinceAttack > 1.0f)
            _currentAttack = 1;

        // Call one of three attack animations "Attack1", "Attack2", "Attack3"
        _swordSwishSound.Play();
        _animator.SetTrigger("Attack" + _currentAttack);

        // Reset timer
        _timeSinceAttack = 0.0f;
    }


    private void StartFallTrigger()
    {
        if (!_isJumping && !_isFalling && !_groundDetection.IsGrounded)
        {
            //Because _groundDetection.IsGrounded can be true for some frames after Jump starts
            if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("StartJump"))
            {
                _animator.SetTrigger("StartFall");
                _isFalling = true;    
            }
        }

        if (_isFalling && _groundDetection.IsGrounded)
        {
            _isFalling = false;
            _timeSinceFalling = 0;
        }
        
        if (_timeSinceFalling > 2)
            _health.TakeHit(_health.health);
            
    }

    private void CheckArrow()
    {
        if (_isShooting)
        {
            if (!_isCooldown && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Shoot"))
                _animator.SetTrigger("Shoot");
            _isShooting = false;
        }
    }

    //Starts from Player animation - Shoot
    public void Shoot()
    {
        Quaternion angle = _spriteRenderer.flipX ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        Vector2 direction = _spriteRenderer.flipX ? Vector2.left : Vector2.right;

        var arrow = _arrowsPull.Find(a => !a.gameObject.activeSelf);
        arrow.gameObject.SetActive(true);
        arrow.transform.rotation = angle;
        arrow.SetImpulse(direction, this, (int)bonusDamage);
                
        StartCoroutine(SetShootCooldown());
    }

    //Starts from Player animation - Attack
    public void Attack()
    {
        _swordTrigger.transform.localPosition = new Vector2
        {
            x = _spriteRenderer.flipX? _swordTrigger.transform.localPosition.x * -1.6f: _swordTrigger.transform.localPosition.x,
            y = _swordTrigger.transform.localPosition.y
        };
        _swordTrigger.SetActive(true);
    }


    private IEnumerator SetShootCooldown()
    {
        _isCooldown = true;
        yield return new WaitForSeconds(_cooldown);
        _isCooldown = false;
    }
}
