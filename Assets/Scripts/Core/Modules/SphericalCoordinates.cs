/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;

public class SphericalCoordinates
{
	// Parameters for EARTH_WGS84 model
	public class WGS84
	{
		// wikipedia: World_Geodetic_System#A_new_World_Geodetic_System:_WGS_84

		// a: Equatorial radius. Semi-major axis of the WGS84 spheroid (meters).
		public const double EarthAxisEquatorial = 6378137d;

		// b: Polar radius. Semi-minor axis of the wgs84 spheroid (meters).
		public const double EarthAxisPolar = 6356752.314245d;

		// if: WGS84 inverse flattening parameter (no units)
		public const double EarthFlattening = 1d / 298.257223563d;
	}

	public enum SurfaceType
	{
		// Model of reference ellipsoid for earth, based on WGS 84 standard.
		// see wikipedia: World_Geodetic_System
		EARTH_WGS84 = 1,

		// Model of the moon, based on the Selenographic coordinate system,
		// see wikipedia: Selenographic Coordinate System.
		MOON_SCS = 2
	};

	public enum CoordinateType
	{
		SPHERICAL = 1, // Latitude, Longitude and Altitude by SurfaceType
		ECEF = 2, // Earth centered, earth fixed Cartesian
		GLOBAL = 3, // Local tangent plane (East, North, Up)
		LOCAL = 4 // Heading-adjusted tangent plane (X, Y, Z)
	};

	// Radius of the Earth (meters).
	// private const double EarthRadius = 6371000.0d;

	// Semi-major axis ellipse parameter
	private double ellA;

	// Semi-minor axis ellipse parameter
	private double ellB;

	// Flattening ellipse parameter
	private double ellF;

	// First eccentricity ellipse parameter
	private double ellE;

	// Second eccentricity ellipse parameter
	private double ellP;

	// ECEF (earth-centered, earth-fixed)
	private Matrix4x4d matrixECEFToGlobal = Matrix4x4d.zero;
	private Matrix4x4d matrixGlobalToECEF = Matrix4x4d.zero;

	private double cosHea;
	private double sinHea;

	private Vector3d origin = Vector3d.zero; // It is ECEF coordinates

	private SurfaceType surfaceType;

	private double _latitudeReference = 0; // in radian
	private double _longitudeReference = 0; // in radian
	private double _elevationReference = 0; // in meters
	private double _heading = 0; // in radian

	private double _headingOrientationOffset = 0; // in degree

	public void SetLatitudeReference(in double angle)
	{
		_latitudeReference = angle * Mathf.Deg2Rad;
		UpdateTransformation();
	}

	public void SetLongitudeReference(in double angle)
	{
		_longitudeReference = angle * Mathf.Deg2Rad;
		UpdateTransformation();
	}

	public void SetElevationReference(in double elevation)
	{
		_elevationReference = elevation;
		UpdateTransformation();
	}

	public void SetHeadingOffset(in double angle)
	{
		_heading = (angle + _headingOrientationOffset) * Mathf.Deg2Rad;
		UpdateTransformation();
	}

	public float HeadingAngle => (float)(_heading * Mathf.Rad2Deg - _headingOrientationOffset);

	void Awake()
	{
		SetSurfaceType(SurfaceType.EARTH_WGS84);

		UpdateTransformation();
	}

	private void UpdateTransformation()
	{
		// Cache trig results
		var cosLat = Math.Cos(_latitudeReference);
		var sinLat = Math.Sin(_latitudeReference);
		var cosLon = Math.Cos(_longitudeReference);
		var sinLon = Math.Sin(_longitudeReference);

		// Create a rotation matrix that moves ECEF to GLOBAL
		// Transformations_between_ECEF_and_ENU_coordinates
		// https://gssc.esa.int/navipedia/index.php/Transformations_between_ECEF_and_ENU_coordinates
		// https://en.wikipedia.org/wiki/ECEF
		matrixECEFToGlobal.m00 = -sinLon;
		matrixECEFToGlobal.m01 = cosLon;

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

		matrixGlobalToECEF.m21 = cosLat;
		matrixGlobalToECEF.m22 = sinLat;

		// Cache heading transforms
		// -- note that we have to negate the heading in order to preserve backward compatibility.
		// ie. Gazebo has traditionally expressed positive angle as a CLOCKWISE rotation that takes the GLOBAL
		// frame to the LOCAL frame. However, right hand coordinate systems require this to be expressed as an ANTI-CLOCKWISE rotation. So, we negate it.
		cosHea = Math.Cos(-_heading);
		sinHea = Math.Sin(-_heading);

		// Cache the ECEF coordinate of the origin
		origin.Set(_latitudeReference, _longitudeReference, _elevationReference);
		origin = PositionTransform(origin, CoordinateType.SPHERICAL, CoordinateType.ECEF);
	}

	public void SetWorldOrientation(in string orientation)
	{
		// world frame: world_orientation="ENU" with heading_deg=-90° == "NWU" with heading of 0°.

		switch (orientation)
		{
			case "NWU":
				_headingOrientationOffset = 0;
				break;

			case "NED":
				Debug.LogWarning("need to check NED orientaion");
				_headingOrientationOffset = 0;
				break;

			case "ENU":
			case "":
			default:
				_headingOrientationOffset = -90;
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
					this.ellA = WGS84.EarthAxisEquatorial;

					// Set the semi-minor axis
					this.ellB = WGS84.EarthAxisPolar;

					// Set the flattening parameter
					this.ellF = WGS84.EarthFlattening;

					// Set the first eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					// this.ellE = Math.Sqrt(1f - Math.Pow(this.ellB, 2) / Math.Pow(ellA, 2));
					this.ellE = Math.Sqrt(1d - Math.Pow(this.ellB/this.ellA, 2));

					// Set the second eccentricity ellipse parameter
					// https://en.wikipedia.org/wiki/Eccentricity_(mathematics)#Ellipses
					// this.ellP = Math.Sqrt(Math.Pow(ellA, 2) / Math.Pow(ellB, 2) - 1d);
					this.ellP = Math.Sqrt(Math.Pow(this.ellA/this.ellB, 2) - 1d);

					_ = this.ellF;

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
	private Vector3d PositionTransform(in Vector3d position, in CoordinateType input, in CoordinateType output)
	{
		var tmpPosition = new Vector3d(position);

		// Cache trig results
		var cosLat = Math.Cos(tmpPosition.x);
		var sinLat = Math.Sin(tmpPosition.x);
		var cosLon = Math.Cos(tmpPosition.y);
		var sinLon = Math.Sin(tmpPosition.y);

		// Radius of planet curvature (meters)
		var curvature = 1d - Math.Pow(this.ellE, 2) * Math.Pow(sinLat, 2);
		curvature = this.ellA / Math.Sqrt(curvature);

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
				// Debug.LogFormat("input GLOBAL origin: {0}, {1}", origin.ToString("F9"), tmpPosition.ToString("F9"));
				break;

			case CoordinateType.SPHERICAL:
				var curvatureOffset = (double)position.z + curvature;
				tmpPosition.x = curvatureOffset * cosLat * cosLon;
				tmpPosition.y = curvatureOffset * cosLat * sinLon;
				// tmpPosition.z = (Math.Pow(this.ellB, 2) / Math.Pow(this.ellA, 2) * curvature + position.z) * sinLat;
				tmpPosition.z = (Math.Pow(this.ellB/this.ellA, 2) * curvature + position.z) * sinLat;
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
				var p = Math.Sqrt(Math.Pow(tmpPosition.x, 2) + Math.Pow(tmpPosition.y, 2));
				var theta = Math.Atan((tmpPosition.z * this.ellA) / (p * this.ellB));

				// Calculate latitude and longitude
				var lat = Math.Atan((tmpPosition.z + Math.Pow(this.ellP, 2) * this.ellB * Math.Pow(Math.Sin(theta), 3)) / (p - Math.Pow(this.ellE, 2) * this.ellA * Math.Pow(Math.Cos(theta), 3)));
				var lon = Math.Atan2(tmpPosition.y, tmpPosition.x);

				// Recalculate radius of planet curvature at the current latitude.
				var nCurvature = 1d - Math.Pow(ellE, 2) * Math.Pow(Math.Sin(lat), 2);
				nCurvature = ellA / Math.Sqrt(nCurvature);

				tmpPosition.Set(lat, lon, p / Math.Cos(lat) - nCurvature);
				// Debug.LogFormat("output SPHERICAL {0}", tmpPosition.ToString("F9"));

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
	// double Distance(in Angle _latA, in Angle _lonA, in Angle _latB, in Angle _lonB)
	// {
	// 	Angle dLat = _latB - _latA;
	// 	Angle dLon = _lonB - _lonA;

	// 	double a = Math.Sin(dLat * Math.Deg2Rad / 2) * Math.Sin(dLat * Math.Deg2Rad / 2) + Math.Sin(dLon * Math.Deg2Rad / 2) * Math.Sin(dLon * Math.Deg2Rad / 2) * Math.Cos(_latA * Math.Deg2Rad) * Math.Cos(_latB * Math.Deg2Rad);
	// 	double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
	// 	double d = EarthRadius * c;
	// 	return d;
	// }

	//
	/// <summary>based on right handed system</summary>
	//
	// Parameters:
	//    velocity: ECEF x, y, z in radian,
	//
	private Vector3d VelocityTransform(in Vector3d velocity, in CoordinateType input, in CoordinateType output)
	{
		// Sanity check -- velocity should not be expressed in spherical coordinates
		if (input == CoordinateType.SPHERICAL || output == CoordinateType.SPHERICAL)
		{
			Debug.LogWarning("Spherical velocities are not supported");
			return velocity;
		}

		// Intermediate data type
		var tmpVelocity = new Vector3d(velocity);

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

	public void SetCoordinatesReference(in double latitudeAngle, in double longitudeAngle, in double elevation, in double headingAngle)
	{
		// Set the coordinate transform parameters in degree
		_latitudeReference = latitudeAngle * Mathf.Deg2Rad;
		_longitudeReference = longitudeAngle * Mathf.Deg2Rad;
		_elevationReference = elevation;
		_heading = (headingAngle + _headingOrientationOffset) * Mathf.Deg2Rad;

		UpdateTransformation();
	}

	/// <summary>based on right handed system</summary>
	public Vector3d SphericalFromLocal(in SDF.Vector3<double> xyz)
	{
		var result = PositionTransform(xyz, CoordinateType.LOCAL, CoordinateType.SPHERICAL);
		result.x *= Mathf.Rad2Deg;
		result.y *= Mathf.Rad2Deg;

		return result;
	}

	/// <summary>based on right handed system</summary>
	public Vector3d LocalFromSpherical(in Vector3 xyz)
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
	public Vector3d GlobalFromLocal(in SDF.Vector3<double> xyz)
	{
		return VelocityTransform(xyz, CoordinateType.LOCAL, CoordinateType.GLOBAL);
	}

	/// <summary>based on right handed system</summary>
	public Vector3d LocalFromGlobal(in Vector3 xyz)
	{
		return VelocityTransform(xyz, CoordinateType.GLOBAL, CoordinateType.LOCAL);
	}
}