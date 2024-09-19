using Sandbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;

public sealed class BaseScript : Component
{
	// Movement Properties
	[Property] public float GroundControl { get; set; } = 4.0f;
	[Property] public float AirControl { get; set; } = 0.1f;
	[Property] public float MaxForce { get; set; } = 50f;
	[Property] public float Speed { get; set; } = 160f;
	[Property] public float RunSpeed { get; set; } = 290f;
	[Property] public float CrouchSpeed { get; set; } = 145f;
	[Property] public float WalkSpeed { get; set; } = 90f;
	[Property] public float JumpForce { get; set; } = 400f;


	// Object References
	public GameObject Head { get; set; } = null;
	public GameObject Body { get; set; } = null;
	[Property] public GameObject PrefabCamera { get; set; }


	// Member Variables
	public bool IsCrouching = false;
	public bool IsSprinting = false;
	private CharacterController characterController;
	private CitizenAnimationHelper animationHelper;

	protected override void OnAwake()
	{
		

	}
	protected override void OnStart()
	{
		Logger Log = new Logger( name: "Player:" );

		List<GameObject> ch = GameObject.Children;

		Body = ch.Find( item => item.Name == "Body" );
		Head = ch.Find( item => item.Name == "Head" );

		animationHelper = Body.Components.Get<CitizenAnimationHelper>();
		characterController = Components.Get<CharacterController>();


		if ( !Network.IsProxy )
		{
			var clothing = ClothingContainer.CreateFromLocalUser();
			clothing.Apply( Body.Components.Get<SkinnedModelRenderer>() );

			GameObject cam = PrefabCamera.Clone();
			cam.SetParent( GameObject );

			CameraScript CS = cam.Components.Get<CameraScript>();
			CS.Player = GameObject.Components.Get<BaseScript>();
			CS.Body = Body;
			CS.Head = Head;


			List<GameObject> cl = Body.Children;
			foreach ( GameObject item in cl )
			{
				item.NetworkSpawn();
			}
		}
		base.OnStart();
	}

	// Member Variables
	Vector3 WishVelocity = Vector3.Zero;
	// ...

	void BuildWishVelocity()//������ ��������
	{
		WishVelocity = 0;

		var rot = Head.Transform.Rotation;
		if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;
		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

		if ( IsCrouching ) WishVelocity *= CrouchSpeed; // Crouching takes presedence over sprinting
		else if ( IsSprinting ) WishVelocity *= RunSpeed; // Sprinting takes presedence over walking
		else WishVelocity *= Speed;
	}
	void Move()//������ ��������� �������� � ������ ������
	{
		// Get gravity from our scene
		var gravity = Scene.PhysicsWorld.Gravity;

		if ( characterController.IsOnGround )
		{
			// Apply Friction/Acceleration
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
			characterController.Accelerate( WishVelocity );
			characterController.ApplyFriction( GroundControl );
		}
		else
		{
			// Apply Air Control / Gravity
			characterController.Velocity += gravity * Time.Delta * 0.5f;
			characterController.Accelerate( WishVelocity.ClampLength( MaxForce ) );
			characterController.ApplyFriction( AirControl );
		}

		// Move the character controller
		characterController.Move();

		// Apply the second half of gravity after movement
		if ( !characterController.IsOnGround )
		{
			characterController.Velocity += gravity * Time.Delta * 0.5f;
		}
		else
		{
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
		}
	}
	void RotateBody()
	{
		if ( Body is null ) return;

		var targetAngle = new Angles( 0, Head.Transform.Rotation.Yaw(), 0 ).ToRotation();
		float rotateDifference = Body.Transform.Rotation.Distance( targetAngle );

		// Lerp body rotation if we're moving or rotating far enough
		if ( rotateDifference > 50f || characterController.Velocity.Length > 10f )
		{
			Body.Transform.Rotation = Rotation.Lerp( Body.Transform.Rotation, targetAngle, Time.Delta * 2f );
		}
	}
	void Jump()
	{
		if ( !characterController.IsOnGround ) return;

		characterController.Punch( Vector3.Up * JumpForce );
		animationHelper?.TriggerJump(); // Trigger our jump animation if we have one
	}

	void UpdateAnimation()
	{
		if ( animationHelper is null ) return;

		animationHelper.WithWishVelocity( WishVelocity );
		animationHelper.WithVelocity( characterController.Velocity );
		animationHelper.AimAngle = Head.Transform.Rotation;
		animationHelper.IsGrounded = characterController.IsOnGround;
		animationHelper.WithLook( Head.Transform.Rotation.Forward, 1, 0.75f, 0.5f );
		animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
		animationHelper.DuckLevel = IsCrouching ? 1f : 0f;

		//animationHelper.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}
	void UpdateCrouch()
	{
		if ( characterController is null ) return;

		if ( Input.Pressed( "Duck" ) && !IsCrouching )
		{
			IsCrouching = true;
			characterController.Height /= 2f; // Reduce the height of our character controller
		}

		if ( Input.Released( "Duck" ) && IsCrouching )
		{
			IsCrouching = false;
			characterController.Height *= 2f; // Return the height of our character controller to normal
		}
	}
	protected override void OnUpdate()
	{
		UpdateAnimation();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Network.IsProxy )
		{
			IsSprinting = Input.Down( "Run" );
			if ( Input.Pressed( "Jump" ) ) Jump();

			BuildWishVelocity();
			RotateBody();
			UpdateCrouch();
			Move();
		}
	}
}
