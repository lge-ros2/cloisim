using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArticulationBodyConstantForce : MonoBehaviour
{
  private ArticulationBody targetBody;

  public Vector3 force;
  public Vector3 positionToForce;
  public Vector3 relativeForce;
  public Vector3 torque;
  public Vector3 relativeTorque;

	public float[] jointForce;

  public float[] jointAcceleration;

  public float[] jointVelocity;

  public float[] jointPosition;


	// Start is called before the first frame update
	void Start()
	{
    targetBody = this.GetComponent<ArticulationBody>();

    if (targetBody == null)
    {
      Debug.LogWarning("this script needs articulation body.");
    }
    else
    {
      jointForce = new float[targetBody.jointForce.dofCount];
      jointAcceleration = new float[targetBody.jointAcceleration.dofCount];
      jointVelocity = new float[targetBody.jointVelocity.dofCount];
      jointPosition = new float[targetBody.jointPosition.dofCount];
    }
	}

	// Update is called once per frame
	void FixedUpdate()
	{
    if (targetBody == null)
    {
      return;
    }

    if (positionToForce.Equals(Vector3.zero))
    {
      targetBody.AddForce(force);
    }
    else
    {
      targetBody.AddForceAtPosition(force, positionToForce);
    }

    targetBody.AddRelativeForce(relativeForce);
    targetBody.AddTorque(torque);
    targetBody.AddRelativeTorque(relativeTorque);

    for (var index = 0; index < targetBody.jointForce.dofCount; index++)
    {
      jointForce[index] = targetBody.jointForce[index];
    }
    for (var index = 0; index < targetBody.jointAcceleration.dofCount; index++)
    {
      jointAcceleration[index] = targetBody.jointAcceleration[index];
    }
    for (var index = 0; index < targetBody.jointVelocity.dofCount; index++)
    {
      jointVelocity[index] = targetBody.jointVelocity[index];
    }
    for (var index = 0; index < targetBody.jointPosition.dofCount; index++)
    {
      jointPosition[index] = targetBody.jointPosition[index];
    }
  }
}
