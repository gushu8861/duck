using UnityEngine;
namespace DuckGame {
[DefaultExecutionOrder(-40)] public sealed class GooseMotor:MonoBehaviour {
 public CharacterController controller;public Animator animator;public ShoulderCamera view;
 public float walkSpeed=2.2f,runSpeed=4.4f,acceleration=.12f,turnTime=.10f;
 public bool InjectInput;public Vector2 TestMove;public bool TestRun;
 Vector3 velocity,smoothRef;float fall,turnVelocity;
 void Update(){Vector2 input=InjectInput?TestMove:new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));input=Vector2.ClampMagnitude(input,1);bool run=InjectInput?TestRun:Input.GetKey(KeyCode.LeftShift);Vector3 forward=Quaternion.Euler(0,view.Yaw,0)*Vector3.forward;Vector3 right=Quaternion.Euler(0,view.Yaw,0)*Vector3.right;Vector3 target=(right*input.x+forward*input.y)*(run?runSpeed:walkSpeed);velocity=Vector3.SmoothDamp(velocity,target,ref smoothRef,acceleration);if(controller.isGrounded&&fall<0)fall=-2;fall+=Physics.gravity.y*Time.deltaTime;controller.Move((velocity+Vector3.up*fall)*Time.deltaTime);float angle=Mathf.SmoothDampAngle(transform.eulerAngles.y,view.Yaw,ref turnVelocity,turnTime);transform.rotation=Quaternion.Euler(0,angle,0);animator.SetFloat("Speed",velocity.magnitude,.12f,Time.deltaTime);
 if(!InjectInput){if(Input.GetKeyDown(KeyCode.Alpha1))animator.SetTrigger("Honk");if(Input.GetKeyDown(KeyCode.Alpha2))animator.SetTrigger("Peck");if(Input.GetKeyDown(KeyCode.Alpha3))animator.SetTrigger("WingFlap");if(Input.GetKeyDown(KeyCode.Alpha4))animator.SetTrigger("Startle");}
 }
}
}
