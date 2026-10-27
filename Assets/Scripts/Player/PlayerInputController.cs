using SesiVillage.UI;
using RPGCharacterAnims;
using RPGCharacterAnims.Actions;
using RPGCharacterAnims.Lookups;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SesiVillage.Player
{
    [RequireComponent(typeof(WeaponHolster))]
    public class PlayerInputController : MonoBehaviour
    {
        RPGCharacterController rpgCharacterController;
        private WeaponHolster weaponHolster;

        //InputSystem
        public @RPGInputs rpgInputs;

        // Inputs.
        private bool inputJump;
        private bool inputLightHit;
        private bool inputKnockdown;
        private bool inputAttackL;
        private bool inputAttackR;
        private bool inputRoll;
        private bool inputAim;
        private Vector2 inputMovement;
        private bool inputFace;
        private Vector2 inputFacing;
        private bool inputSprint;

        // Variables.
        private Vector3 moveInput;
        private Vector3 currentAim;
        private float inputPauseTimeout = 0;
        private bool inputPaused = false;

        private void Awake()
        {
            rpgCharacterController = GetComponent<RPGCharacterController>();
            weaponHolster = GetComponent<WeaponHolster>();
            rpgInputs = new @RPGInputs();
            currentAim = Vector3.zero;
        }

        private void OnEnable()
        {
            rpgInputs.Enable();
        }

        private void OnDisable()
        {
            rpgInputs.Disable();
        }
        
        private void Start()
        {
            if (UIState.Instance == null) return;
            UIState.Instance.Changed += OnUIChanged;
            OnUIChanged(UIState.Instance.IsOpen);
        }
        
        private void OnDestroy()
        {
            if (UIState.Instance != null) UIState.Instance.Changed -= OnUIChanged;
        }
        
        /// <summary>UI가 열리면 이동·공격 입력을 끈다 (꺼진 입력은 0으로 읽혀 제자리에 선다)</summary>
        private void OnUIChanged(bool uiOpen)
        {
            if (uiOpen) rpgInputs.Disable();
            else if (isActiveAndEnabled) rpgInputs.Enable();
        }

        public bool HasMoveInput() => moveInput.magnitude > 0.1f;

        public bool HasAimInput() => inputAim;

        public bool HasFacingInput() => inputFacing != Vector2.zero || inputFace;
        public bool HasSprintInput() => inputSprint;

        private void Update()
        {
            // Pause input for other external input.
            if (inputPaused)
            {
                if (Time.time > inputPauseTimeout)
                {
                    inputPaused = false;
                }
                else
                {
                    return;
                }
            }

            if (!inputPaused)
            {
                Inputs();
            }

            Moving();
            Jumping();
            Damage();
            Strafing();
            Facing();
            Aiming();
            Rolling();
            Attacking();
        }

        /// <summary>
        /// Pause input for a number of seconds.
        /// </summary>
        /// <param name="timeout">The amount of time in seconds to ignore input.</param>
        public void PauseInput(float timeout)
        {
            inputPaused = true;
            inputPauseTimeout = Time.time + timeout;
        }

        /// <summary>
        /// Input abstraction for easier asset updates using outside control schemes.
        /// </summary>
        private void Inputs()
        {
            try
            {
                inputAttackL = rpgInputs.RPGCharacter.AttackL.WasPressedThisFrame();
                inputAttackR = rpgInputs.RPGCharacter.AttackR.WasPressedThisFrame();
                inputKnockdown = rpgInputs.RPGCharacter.Knockdown.WasPressedThisFrame();
                inputFace = rpgInputs.RPGCharacter.Face.IsPressed();
                inputFacing = rpgInputs.RPGCharacter.Facing.ReadValue<Vector2>();
                inputJump = rpgInputs.RPGCharacter.Jump.IsPressed();
                inputLightHit = rpgInputs.RPGCharacter.LightHit.WasPressedThisFrame();
                inputMovement = rpgInputs.RPGCharacter.Move.ReadValue<Vector2>();
                inputRoll = rpgInputs.RPGCharacter.Roll.WasPressedThisFrame();
                inputAim = rpgInputs.RPGCharacter.Aim.IsPressed();
                inputSprint = rpgInputs.RPGCharacter.Sprint.IsPressed();
            }
            catch (System.Exception)
            {
                Debug.LogError("Inputs not found!  " +
                               "Make sure your project is using the new InputSystem: Edit>Project Settings>Player>Active Input Handling  - change to 'Input System Package (New)'.");
            }
        }

        public void Moving()
        {
            moveInput = new Vector3(inputMovement.x, inputMovement.y, 0f);

            // Filter the 0.1 threshold of HasMoveInput.
            if (HasMoveInput())
            {
                rpgCharacterController.SetMoveInput(moveInput);
            }
            else
            {
                rpgCharacterController.SetMoveInput(Vector3.zero);
            }
        }

        private void Jumping()
        {
            // Set the input on the jump axis every frame.
            Vector3 jumpInput = inputJump ? Vector3.up : Vector3.zero;
            rpgCharacterController.SetJumpInput(jumpInput);

            // If we pressed jump button this frame, jump.
            if (inputJump && rpgCharacterController.CanStartAction("Jump"))
            {
                rpgCharacterController.StartAction("Jump");
            }
            else if (inputJump && rpgCharacterController.CanStartAction("DoubleJump"))
            {
                rpgCharacterController.StartAction("DoubleJump");
            }
        }

        public void Rolling()
        {
            if (!inputRoll)
            {
                return;
            }

            if (!rpgCharacterController.CanStartAction("DiveRoll"))
            {
                return;
            }

            rpgCharacterController.StartAction("DiveRoll", 1);
        }

        private void Aiming()
        {
            Strafing();
        }

        private void Strafing()
        {
            if (rpgCharacterController.canStrafe)
            {
                if (inputAim)
                {
                    if (rpgCharacterController.CanStartAction("Strafe"))
                    {
                        rpgCharacterController.StartAction("Strafe");
                    }
                }
                else
                {
                    if (rpgCharacterController.CanEndAction("Strafe"))
                    {
                        rpgCharacterController.EndAction("Strafe");
                    }
                }
            }
        }

        private void Facing()
        {
            if (rpgCharacterController.canFace)
            {
                if (HasFacingInput())
                {
                    if (inputFace)
                    {
                        // Get world position from mouse position on screen and convert to direction from character.
                        Plane playerPlane = new Plane(Vector3.up, transform.position);
                        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                        float hitdist = 0.0f;
                        if (playerPlane.Raycast(ray, out hitdist))
                        {
                            Vector3 targetPoint = ray.GetPoint(hitdist);
                            Vector3 lookTarget = new Vector3(targetPoint.x - transform.position.x,
                                transform.position.z - targetPoint.z, 0);
                            rpgCharacterController.SetFaceInput(lookTarget);
                        }
                    }
                    else
                    {
                        rpgCharacterController.SetFaceInput(new Vector3(inputFacing.x, inputFacing.y, 0));
                    }

                    if (rpgCharacterController.CanStartAction("Face"))
                    {
                        rpgCharacterController.StartAction("Face");
                    }
                }
                else
                {
                    if (rpgCharacterController.CanEndAction("Face"))
                    {
                        rpgCharacterController.EndAction("Face");
                    }
                }
            }
        }

        private void Attacking()
        {
            if (!inputAttackL && !inputAttackR) { return; }
            
            // 맨손이면 공격 대신 칼을 꺼낸다. (기획: 평소엔 칼을 넣어 두고 공격 키로 꺼냄)
            if (!weaponHolster.IsDrawn)
            {
                weaponHolster.Draw();
                return;
            }
            
            // Check to make sure Attack Action exists.
            if (!rpgCharacterController.HandlerExists(HandlerTypes.Attack))
            {
                return;
            }

            // Check to make character can Attack.
            if (!rpgCharacterController.CanStartAction(HandlerTypes.Attack))
            {
                return;
            }
            
            // 공격할 때마다 자동 넣기 타이머를 다시 잰다.
            weaponHolster.NotifyAttack();

            if (inputAttackL)
            {
                rpgCharacterController.StartAction(HandlerTypes.Attack,
                    new AttackContext(HandlerTypes.Attack, Side.Left));
            }
            else if (inputAttackR)
            {
                rpgCharacterController.StartAction(HandlerTypes.Attack,
                    new AttackContext(HandlerTypes.Attack, Side.Right));
            }
        }

        private void Damage()
        {
            // Hit.
            if (rpgCharacterController.HandlerExists(HandlerTypes.GetHit))
            {
                if (inputLightHit)
                {
                    rpgCharacterController.StartAction(HandlerTypes.GetHit, new HitContext());
                }
            }

            // Knockdown.
            if (rpgCharacterController.HandlerExists(HandlerTypes.Knockdown))
            {
                if (inputKnockdown && rpgCharacterController.CanStartAction(HandlerTypes.Knockdown))
                {
                    rpgCharacterController.StartAction(HandlerTypes.Knockdown,
                        new HitContext((int)KnockdownType.Knockdown1, Vector3.back));
                }
            }
        }
    }
}