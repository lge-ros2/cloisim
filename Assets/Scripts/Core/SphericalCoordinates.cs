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
		public const float EarthWGS84AxisEquatorial = 6378137.0f;

		// b: Polar radius. Semi-minor axis of the wgs84 spheroid (meters).
		public const float EarthWGS84AxisPolar = 6356752.314245f;

		// if: WGS84 inverse flattening parameter (no units)
		public const float EarthWGS84Flattening = 1.0f / 298.257223563f;
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
	private const float EarthRadius = 6371000.0f;

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
	private Matrix4x4 matrixECEFToGlobal;
	private Matrix4x4 matrixGlobalToECEF;

	private float cosHea;
	private float sinHea;

	private Vector3 origin; // It is ECEF coordinates

	private SurfaceType surfaceType;

	private float latitudeReference = 0; // in degree
	private float longitudeReference = 0; // in degree
	private float elevationReference = 0; // in meters
	private float headingOffset = 0; // in degree

	public SurfaceType Surface_Type => surfaceType;

	void Awake()
	{
		matrixECEFToGlobal = new Matrix4x4();
		matrixGlobalToECEF = new Matrix4x4();
		origin = new Vector3();

		SetSurfaceType(SurfaceType.EARTH_WGS84);

		UpdateTransformation();
	}

	private void UpdateTransformation()
	{
		// Cache trig results
		var cosLat = Mathf.Cos(latitudeReference * Mathf.Deg2Rad);
		var sinLat = Mathf.Sin(latitudeReference * Mathf.Deg2Rad);
		var cosLon = Mathf.Cos(longitudeReference * Mathf.Deg2Rad);
		var sinLon = Mathf.Sin(longitudeReference * Mathf.Deg2Rad);

		// Create a rotation matrix that moves ECEF to GLOBAL
		// Transformations_between_ECEF_and_ENU_coordinates
		// https://gssc.esa.int/navipedia/index.php/Transformations_between_ECEF_and_ENU_coordinates
		// https://en.wikipedia.org/wiki/ECEF
		matrixECEFToGlobal.m00 = -sinLon;
		matrixECEFToGlobal.m01 = cosLon;
		matrixECEFToGlobal.m02 = 0.0f;
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
		cosHea = Mathf.Cos(headingOffset * Mathf.Deg2Rad);
		sinHea = Mathf.Sin(headingOffset * Mathf.Deg2Rad);

		// Cache the ECEF coordinate of the origin
		origin.Set(latitudeReference * Mathf.Deg2Rad, longitudeReference * Mathf.Deg2Rad, elevationReference);
		origin = PositionTransform(origin, CoordinateType.SPHERICAL, CoordinateType.ECEF);
	}

	public void SetSurfaceType(in SurfaceType type)
	{
		surfaceType = type;

		switch (surfaceType)
		{
			case SurfaceType.EARTH_WGS84:
				{
					// Set the semi-major axis
					ellA = WGS84.EarthWGS84AxisEquatorial;

					// Set the semi-minor axis
					ellB = WGS84.EarthWGS84AxisPolar;

					// Set the flattening parameter
					ellF = WGS84.EarthWGS84Flattening;

					// Set the first eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					ellE = Mathf.Sqrt(1.0f - Mathf.Pow(ellB, 2) / Mathf.Pow(ellA, 2));

					// Set the second eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					ellP = Mathf.Sqrt(Mathf.Pow(ellA, 2) / Mathf.Pow(ellB, 2) - 1.0f);

					break;
				}

			default:
				Debug.LogFormat("Unknown surface type[{0}]", type);
				break;
		}
	}

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

				// Now calculate y
				tmpPosition.z = p / Mathf.Cos(lat) - nCurvature;

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
		Vector3 tmpVelocity = velocity;

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
		latitudeReference = latitudeAngle;
		longitudeReference = longitudeAngle;
		elevationReference = elevation;
		headingOffset = headingAngle;

		UpdateTransformation();
	}

	public Vector3 SphericalFromLocal(in Vector3 xyz)
	{
		var convertedXYZ = ToECEF(xyz);
		convertedXYZ.x *= Mathf.Deg2Rad;
		convertedXYZ.y *= Mathf.Deg2Rad;

		var result = PositionTransform(convertedXYZ, CoordinateType.LOCAL, CoordinateType.SPHERICAL);
		result.x *= Mathf.Rad2Deg;
		result.y *= Mathf.Rad2Deg;

		return result;
	}

	public Vector3 LocalFromSpherical(in Vector3 xyz)
	{
		var convertedXYZ = ToECEF(xyz);
		convertedXYZ.x *= Mathf.Deg2Rad;
		convertedXYZ.y *= Mathf.Deg2Rad;

		var result = PositionTransform(convertedXYZ, CoordinateType.SPHERICAL, CoordinateType.LOCAL);
		result.x *= Mathf.Rad2Deg;
		result.y *= Mathf.Rad2Deg;

		return result;
	}

	public Vector3 GlobalFromLocal(in Vector3 xyz)
	{
		return VelocityTransform(ToECEF(xyz), CoordinateType.LOCAL, CoordinateType.GLOBAL);
	}

	public Vector3 LocalFromGlobal(in Vector3 xyz)
	{
		return VelocityTransform(ToECEF(xyz), CoordinateType.GLOBAL, CoordinateType.LOCAL);
	}

	private Vector3 ToECEF(in Vector3 xyz)
	{
		var ecef = xyz;
		ecef.Set(xyz.x, xyz.z, xyz.y);
		return ecef;
	}
}