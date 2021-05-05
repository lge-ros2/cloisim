/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class SphericalCoordinates : MonoBehaviour
{
	// Parameters for EARTH_WGS84 model
	public class WGS84
	{
		// wikipedia: World_Geodetic_System#A_new_World_Geodetic_System:_WGS_84

		// a: Equatorial radius. Semi-major axis of the WGS84 spheroid (meters).
		public const float EarthAxisEquatorial = 6378137.0f;

		// b: Polar radius. Semi-minor axis of the wgs84 spheroid (meters).
		public const float EarthAxisPolar = 6356752.314245f;

		// if: WGS84 inverse flattening parameter (no units)
		public const float EarthFlattening = 1.0f / 298.257223563f;
	}

	public enum SurfaceType { EARTH_WGS84 };

	public enum CoordinateType
	{
		SPHERICAL = 1, // Latitude, Longitude and Altitude by SurfaceType
		ECEF = 2, // Earth centered, earth fixed Cartesian
		GLOBAL = 3, // Local tangent plane (East, North, Up)
		LOCAL = 4 // Heading-adjusted tangent plane (X, Y, Z)
	};

	// Radius of the Earth (meters).
	// private const float EarthRadius = 6371000.0f;

	// Semi-major axis ellipse parameter
	private float ellA;

	// Semi-minor axis ellipse parameter
	private float ellB;

	// Flattening ellipse parameter
	private float ellF;

	// First eccentricity ellipse parameter
	private float ellE;

	// Second eccentricity ellipse parameter
	private float ellP;

	// ECEF (earth-centered, earth-fixed)
	private Matrix4x4 matrixECEFToGlobal = new Matrix4x4();
	private Matrix4x4 matrixGlobalToECEF = new Matrix4x4();

	private float cosHea;
	private float sinHea;

	private Vector3 origin = new Vector3(); // It is ECEF coordinates

	private SurfaceType surfaceType;

	private float latitudeReference = 0; // in radian
	private float longitudeReference = 0; // in radian
	private float elevationReference = 0; // in meters
	private float headingOffset = 0; // in radian

	private float haedingOrientationOffset = 0; // in degree

	void Awake()
	{
		SetSurfaceType(SurfaceType.EARTH_WGS84);

		UpdateTransformation();
	}

	private void UpdateTransformation()
	{
		// Cache trig results
		var cosLat = Mathf.Cos(latitudeReference);
		var sinLat = Mathf.Sin(latitudeReference);
		var cosLon = Mathf.Cos(longitudeReference);
		var sinLon = Mathf.Sin(longitudeReference);

		// Create a rotation matrix that moves ECEF to GLOBAL
		// Transformations_between_ECEF_and_ENU_coordinates
		// https://gssc.esa.int/navipedia/index.php/Transformations_between_ECEF_and_ENU_coordinates
		// https://en.wikipedia.org/wiki/ECEF
		matrixECEFToGlobal.m00 = -sinLon;
		matrixECEFToGlobal.m01 = cosLon;
		matrixECEFToGlobal.m02 = 0;
		matrixECEFToGlobal.m10 = -cosLon * sinLat;
		matrixECEFToGlobal.m11 = -sinLon * sinLat;
		matrixECEFToGlobal.m12 = cosLat;
		matrixECEFToGlobal.m20 = cosLon * cosLat;
		matrixECEFToGlobal.m21 = sinLon * cosLat;
		matrixECEFToGlobal.m22 = sinLat;

		// Create a rotation matrix that moves GLOBAL to ECEF
		// Transformations_between_ECEF_and_ENU_coordinates
		// https://gssc.esa.int/navipedia/index.php/Transformations_between_ECEF_and_ENU_coordinates
		// https://en.wikipedia.org/wiki/ECEF
		matrixGlobalToECEF.m00 = -sinLon;
		matrixGlobalToECEF.m01 = -cosLon * sinLat;
		matrixGlobalToECEF.m02 = cosLon * cosLat;
		matrixGlobalToECEF.m10 = cosLon;
		matrixGlobalToECEF.m11 = -sinLon * sinLat;
		matrixGlobalToECEF.m12 = sinLon * cosLat;
		matrixGlobalToECEF.m20 = 0;
		matrixGlobalToECEF.m21 = cosLat;
		matrixGlobalToECEF.m22 = sinLat;

		// Cache heading transforms
		cosHea = Mathf.Cos(headingOffset);
		sinHea = Mathf.Sin(headingOffset);

		// Cache the ECEF coordinate of the origin
		origin.Set(latitudeReference, longitudeReference, elevationReference);
		origin = PositionTransform(origin, CoordinateType.SPHERICAL, CoordinateType.ECEF);
	}

	public void SetWorldOrientation(in string orientation)
	{
		// world frame: world_orientation="ENU" with heading_deg=-90° == "NWU" with heading of 0°.

		switch (orientation)
		{
			case "NWU":
				haedingOrientationOffset = 0;
				break;

			case "NED":
				Debug.LogWarning("need to check NED orientaion");
				haedingOrientationOffset = 0;
				break;

			case "ENU":
			case "":
			default:
				haedingOrientationOffset = -90;
				break;
		}

	}

	public void SetSurfaceType(in string type)
	{
		var surfaceType = (SurfaceType)System.Enum.Parse(typeof(SurfaceType), type);
		SetSurfaceType(surfaceType);
	}

	public void SetSurfaceType(in SurfaceType type)
	{
		surfaceType = type;

		switch (surfaceType)
		{
			case SurfaceType.EARTH_WGS84:
				{
					// Set the semi-major axis
					ellA = WGS84.EarthAxisEquatorial;

					// Set the semi-minor axis
					ellB = WGS84.EarthAxisPolar;

					// Set the flattening parameter
					ellF = WGS84.EarthFlattening;

					// Set the first eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					ellE = Mathf.Sqrt(1.0f - Mathf.Pow(ellB, 2) / Mathf.Pow(ellA, 2));

					// Set the second eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					ellP = Mathf.Sqrt(Mathf.Pow(ellA, 2) / Mathf.Pow(ellB, 2) - 1.0f);

					break;
				}

			default:
				Debug.LogWarningFormat("Unknown surface type[{0}]", type);
				break;
		}
	}

	//
	/// <summary> Convert between positions in SPHERICAL/ECEF/LOCAL/GLOBAL frame</summary>
	//
	// Parameters:
	//    position: ECEF x, y, z in radian,
	//
	private Vector3 PositionTransform(in Vector3 position, in CoordinateType input, in CoordinateType output)
	{
		// Cache trig results
		var cosLat = Mathf.Cos(position.x);
		var sinLat = Mathf.Sin(position.x);
		var cosLon = Mathf.Cos(position.y);
		var sinLon = Mathf.Sin(position.y);

		// Radius of planet curvature (meters)
		var curvature = 1.0f - Mathf.Pow(ellE, 2) * Mathf.Pow(sinLat, 2);
		curvature = ellA / Mathf.Sqrt(curvature);

		var tmpPosition = position;

		// Convert whatever arrives to a more flexible ECEF coordinate
		switch (input)
		{
			// East, North, Up (ENU), note no break at end of case
			case CoordinateType.LOCAL:
				tmpPosition.x = -position.x * cosHea + position.y * sinHea;
				tmpPosition.y = -position.x * sinHea - position.y * cosHea;

				goto case CoordinateType.GLOBAL;

			case CoordinateType.GLOBAL:
				tmpPosition = origin + matrixGlobalToECEF.MultiplyVector(tmpPosition);
				break;

			case CoordinateType.SPHERICAL:
				tmpPosition.x = (position.z + curvature) * cosLat * cosLon;
				tmpPosition.y = (position.z + curvature) * cosLat * sinLon;
				tmpPosition.z = (Mathf.Pow(ellB, 2) / Mathf.Pow(ellA, 2) * curvature + position.z) * sinLat;
				break;

			// Do nothing
			case CoordinateType.ECEF:
				break;

			default:
				Debug.LogFormat("Invalid coordinate type[{0}]", input);
				return position;
		}

		// Convert ECEF to the requested output coordinate system
		switch (output)
		{
			case CoordinateType.SPHERICAL:

				// Convert from ECEF to SPHERICAL
				var p = Mathf.Sqrt(Mathf.Pow(tmpPosition.x, 2) + Mathf.Pow(tmpPosition.y, 2));
				var theta = Mathf.Atan((tmpPosition.z * ellA) / (p * ellB));

				// Calculate latitude and longitude
				var lat = Mathf.Atan((tmpPosition.z + Mathf.Pow(ellP, 2) * ellB * Mathf.Pow(Mathf.Sin(theta), 3)) / (p - Mathf.Pow(ellE, 2) * ellA * Mathf.Pow(Mathf.Cos(theta), 3)));
				var lon = Mathf.Atan2(tmpPosition.y, tmpPosition.x);

				// Recalculate radius of planet curvature at the current latitude.
				var nCurvature = 1.0f - Mathf.Pow(ellE, 2) * Mathf.Pow(Mathf.Sin(lat), 2);
				nCurvature = ellA / Mathf.Sqrt(nCurvature);

				tmpPosition.x = lat;
				tmpPosition.y = lon;
				tmpPosition.z = (p / Mathf.Cos(lat) - nCurvature);

				break;

			// Convert from ECEF TO GLOBAL
			case CoordinateType.GLOBAL:
				tmpPosition = matrixECEFToGlobal.MultiplyVector(tmpPosition - origin);
				break;

			// Convert from ECEF TO LOCAL
			case CoordinateType.LOCAL:
				tmpPosition = matrixECEFToGlobal.MultiplyVector(tmpPosition - origin);

				tmpPosition.Set(tmpPosition.x * cosHea - tmpPosition.y * sinHea,
								tmpPosition.x * sinHea + tmpPosition.y * cosHea,
								tmpPosition.z);
				break;

			// Return ECEF (do nothing)
			case CoordinateType.ECEF:
				break;

			default:
				Debug.LogFormat("Unknown coordinate type[{0}]", output);
				return position;
		}

		return tmpPosition;
	}

	// Based on Haversine formula (http://en.wikipedia.org/wiki/Haversine_formula).
	// float Distance(in Angle _latA, in Angle _lonA, in Angle _latB, in Angle _lonB)
	// {
	// 	Angle dLat = _latB - _latA;
	// 	Angle dLon = _lonB - _lonA;

	// 	float a = Mathf.Sin(dLat * Mathf.Deg2Rad / 2) * Mathf.Sin(dLat * Mathf.Deg2Rad / 2) + Mathf.Sin(dLon * Mathf.Deg2Rad / 2) * Mathf.Sin(dLon * Mathf.Deg2Rad / 2) * Mathf.Cos(_latA * Mathf.Deg2Rad) * Mathf.Cos(_latB * Mathf.Deg2Rad);
	// 	float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
	// 	float d = EarthRadius * c;
	// 	return d;
	// }

	//
	/// <summary>based on right handed system</summary>
	//
	// Parameters:
	//    velocity: ECEF x, y, z in radian,
	//
	private Vector3 VelocityTransform(in Vector3 velocity, in CoordinateType input, in CoordinateType output)
	{
		// Sanity check -- velocity should not be expressed in spherical coordinates
		if (input == CoordinateType.SPHERICAL || output == CoordinateType.SPHERICAL)
		{
			Debug.LogWarning("Spherical velocities are not supported");
			return velocity;
		}

		// Intermediate data type
		var tmpVelocity = velocity;

		// First, convert to an ECEF vector
		switch (input)
		{
			// ENU (note no break at end of case)
			case CoordinateType.LOCAL:
				tmpVelocity.x = velocity.x * cosHea + velocity.y * sinHea;
				tmpVelocity.y = -velocity.x * sinHea - velocity.y * cosHea;
				goto case CoordinateType.GLOBAL;

			// spherical
			case CoordinateType.GLOBAL:
				tmpVelocity = matrixGlobalToECEF.MultiplyVector(tmpVelocity);
				break;

			// Do nothing
			case CoordinateType.ECEF:
				tmpVelocity = velocity;
				break;

			default:
				Debug.LogWarningFormat("Unknown coordinate type[{0}]", input);
				return velocity;
		}

		// Then, convert to the request coordinate type
		switch (output)
		{
			// ECEF, do nothing
			case CoordinateType.ECEF:
				break;

			// Convert from ECEF to global
			case CoordinateType.GLOBAL:
				tmpVelocity = matrixECEFToGlobal.MultiplyVector(tmpVelocity);
				break;

			// Convert from ECEF to local
			case CoordinateType.LOCAL:

				tmpVelocity = matrixECEFToGlobal.MultiplyVector(tmpVelocity);

				tmpVelocity.Set(tmpVelocity.x * cosHea - tmpVelocity.y * sinHea,
								tmpVelocity.x * sinHea + tmpVelocity.y * cosHea,
								tmpVelocity.z);
				break;

			default:
				Debug.LogWarningFormat("Unknown coordinate type[{0}]", output);
				return velocity;
		}

		return tmpVelocity;
	}

	public void SetCoordinatesReference(in float latitudeAngle, in float longitudeAngle, in float elevation, in float headingAngle)
	{
		// Set the coordinate transform parameters in degree
		latitudeReference = latitudeAngle * Mathf.Deg2Rad;
		longitudeReference = longitudeAngle * Mathf.Deg2Rad;
		elevationReference = elevation;
		headingOffset = (headingAngle + haedingOrientationOffset) * Mathf.Deg2Rad;

		UpdateTransformation();
	}

	/// <summary>based on right handed system</summary>
	public Vector3 SphericalFromLocal(in Vector3 xyz)
	{
		var result = PositionTransform(xyz, CoordinateType.LOCAL, CoordinateType.SPHERICAL);
		result.x *= Mathf.Rad2Deg;
		result.y *= Mathf.Rad2Deg;

		return result;
	}

	/// <summary>based on right handed system</summary>
	public Vector3 LocalFromSpherical(in Vector3 xyz)
	{
		var convertedXYZ = xyz;
		convertedXYZ.x *= Mathf.Deg2Rad;
		convertedXYZ.y *= Mathf.Deg2Rad;

		var result = PositionTransform(convertedXYZ, CoordinateType.SPHERICAL, CoordinateType.LOCAL);
		result.x *= Mathf.Rad2Deg;
		result.y *= Mathf.Rad2Deg;

		return result;
	}

	/// <summary>based on right handed system</summary>
	public Vector3 GlobalFromLocal(in Vector3 xyz)
	{
		return VelocityTransform(xyz, CoordinateType.LOCAL, CoordinateType.GLOBAL);
	}

	/// <summary>based on right handed system</summary>
	public Vector3 LocalFromGlobal(in Vector3 xyz)
	{
		return VelocityTransform(xyz, CoordinateType.GLOBAL, CoordinateType.LOCAL);
	}

	public void SetLatitudeReference(in float angle)
	{
		latitudeReference = angle * Mathf.Deg2Rad;
		UpdateTransformation();
	}

	public void SetLongitudeReference(in float angle)
	{
		longitudeReference = angle * Mathf.Deg2Rad;
		UpdateTransformation();
	}

	public void SetElevationReference(in float elevation)
	{
		elevationReference = elevation;
		UpdateTransformation();
	}

	public void SetHeadingOffset(in float angle)
	{
		headingOffset = (angle + haedingOrientationOffset) * Mathf.Deg2Rad;
		UpdateTransformation();
	}
}