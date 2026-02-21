using System;
using System.Runtime.InteropServices;

namespace cloisim.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LaserScanStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public float angle_min;
        public float angle_max;
        public float angle_increment;
        public float time_increment;
        public float scan_time;
        public float range_min;
        public float range_max;
        public IntPtr ranges; // float array
        public int ranges_length;
        public IntPtr intensities; // float array
        public int intensities_length;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ImuStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public double orientation_x;
        public double orientation_y;
        public double orientation_z;
        public double orientation_w;
        public double angular_velocity_x;
        public double angular_velocity_y;
        public double angular_velocity_z;
        public double linear_acceleration_x;
        public double linear_acceleration_y;
        public double linear_acceleration_z;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct OdometryStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        [MarshalAs(UnmanagedType.LPStr)]
        public string child_frame_id;
        public double pose_x;
        public double pose_y;
        public double pose_z;
        public double pose_orientation_x;
        public double pose_orientation_y;
        public double pose_orientation_z;
        public double pose_orientation_w;
        public double twist_linear_x;
        public double twist_linear_y;
        public double twist_linear_z;
        public double twist_angular_x;
        public double twist_angular_y;
        public double twist_angular_z;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct NavSatFixStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public sbyte status;
        public ushort service;
        public double latitude;
        public double longitude;
        public double altitude;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public double[] position_covariance;
        public byte position_covariance_type;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct ImageStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public uint height;
        public uint width;
        [MarshalAs(UnmanagedType.LPStr)]
        public string encoding;
        public byte is_bigendian;
        public uint step;
        public IntPtr data; // byte array
        public uint data_length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraInfoStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public uint height;
        public uint width;
        [MarshalAs(UnmanagedType.LPStr)]
        public string distortion_model;
        public IntPtr d; // double array
        public int d_length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public double[] k;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public double[] r;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public double[] p;
        public uint binning_x;
        public uint binning_y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LabelInfoStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public IntPtr class_id; // int array
        public IntPtr class_name; // string array
        public int label_length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointFieldStruct
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;
        public uint offset;
        public byte datatype;
        public uint count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointCloud2Struct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public uint height;
        public uint width;
        public IntPtr fields; // PointFieldStruct array
        public int fields_length;
        public byte is_bigendian;
        public uint row_step;
        public IntPtr data; // byte array
        public uint data_length;
        public byte is_dense;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RangeStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public byte radiation_type;
        public float field_of_view;
        public float min_range;
        public float max_range;
        public float range;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PoseStampedStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public double position_x;
        public double position_y;
        public double position_z;
        public double orientation_x;
        public double orientation_y;
        public double orientation_z;
        public double orientation_w;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3dStruct
    {
        public double x;
        public double y;
        public double z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ContactStruct
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string collision1;
        [MarshalAs(UnmanagedType.LPStr)]
        public string collision2;
        public IntPtr positions; // Vector3dStruct array
        public int positions_length;
        public IntPtr normals; // Vector3dStruct array
        public int normals_length;
        public IntPtr depths; // double array
        public int depths_length;
        public IntPtr times; // struct { double sec; double nsec; } array
        public int times_length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ContactsStruct
    {
        public double timestamp;
        [MarshalAs(UnmanagedType.LPStr)]
        public string frame_id;
        public IntPtr contacts; // ContactStruct array
        public int contacts_length;
    }
}
