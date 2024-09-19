using Sandbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.PortableExecutable;

public sealed class PlayerScript : Component
{
	[Property]
	[Category( "Components" )]
	public GameObject camera { get; set; }
	[Property]
	[Category( "Components" )]
	public CharacterController controller { get; set; }
	[Property]
	[Category( "Components" )]
	public CitizenAnimationHelper animator { get; set; }

	[Property]
	[Category( "Stats" )]
	[Range( 0f, 400f, 1f )]
	public float walkspeed { get; set; } = 180;
	[Property]
	[Category( "Stats" )]
	[Range( 0f, 400f, 1f )]
	public float runspeed { get; set; } = 300;

	[Property]
	[Category( "Stats" )]
	[Range( 0f, 1000f, 1f )]
	public float jumpforse { get; set; } = 400;

	[Property]
	public Vector3 eyeposition { get; set; }

	public Angles EyeAngles { get; set; }
	Transform _initialCameraTransform;

	private Logger log = new Logger( "test1: " );
	protected override void OnUpdate()
	{
		if ( Network.IsProxy ) return;

		EyeAngles += Input.AnalogLook;
		EyeAngles = EyeAngles.WithPitch( MathX.Clamp( EyeAngles.pitch, -80f, 80f ) );
		Transform.Rotation = Rotation.FromYaw( EyeAngles.yaw );

		if ( camera != null )
		{
			if ( camera != null )
				camera.Transform.Local = _initialCameraTransform.RotateAround( eyeposition, EyeAngles.WithYaw( 0f ) );
		}

		animator.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
		animator.AimAngle = EyeAngles;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		Gizmo.Draw.LineSphere( eyeposition, 10f );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Network.IsProxy )
		{
			if ( controller == null ) return;

			var factspeed = Input.Down( "Run" ) ? runspeed : walkspeed;
			var MVelocity = Input.AnalogMove.Normal * factspeed * Transform.Rotation;

			controller.Accelerate( MVelocity );

			if ( controller.IsOnGround )
			{
				controller.ApplyFriction( 5f );

				if ( Input.Pressed( "Jump" ) )
				{
					controller.Punch( Vector3.Up * jumpforse );
					if ( animator != null )
						animator.TriggerJump();
				}
			}
			else
				controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;


			controller.Move();
		}

		if ( animator != null )
		{
			animator.IsGrounded = controller.IsOnGround;
			animator.WithVelocity( controller.Velocity );
		}
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( Network.IsProxy ) return;

		if ( Components.TryGet<SkinnedModelRenderer>( out var model ) )
		{
			var clothing = ClothingContainer.CreateFromLocalUser();
			clothing.Apply( model );
		}

		GameObject cam = Scene.Directory.FindByName( "Camera" ).ElementAt( 0 );
		cam.SetParent( this.GameObject );
		cam.Transform.LocalPosition = eyeposition - new Vector3( 200, 0, 0 );
		cam.Transform.LocalRotation = Rotation.FromYaw( EyeAngles.yaw );
		camera = cam;
		if ( camera != null )
		{
			_initialCameraTransform = camera.Transform.Local;
		}
	}
}
