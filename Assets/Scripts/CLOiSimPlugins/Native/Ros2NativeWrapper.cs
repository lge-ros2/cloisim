using System;
using System.Runtime.InteropServices;

namespace cloisim.Native
{
    public static class Ros2NativeWrapper
    {
        private const string DllName = "cloisim_ros2_native";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool InitROS2(int argc, IntPtr argv);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ShutdownROS2();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateNode(string node_name);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyNode(IntPtr node_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateLaserScanPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyLaserScanPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishLaserScan(IntPtr pub_ptr, ref LaserScanStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateImuPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyImuPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishImu(IntPtr pub_ptr, ref ImuStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateOdometryPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyOdometryPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishOdometry(IntPtr pub_ptr, ref OdometryStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateNavSatFixPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyNavSatFixPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishNavSatFix(IntPtr pub_ptr, ref NavSatFixStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateImagePublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyImagePublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishImage(IntPtr pub_ptr, ref ImageStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateCameraInfoPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyCameraInfoPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishCameraInfo(IntPtr pub_ptr, ref CameraInfoStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateLabelInfoPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyLabelInfoPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishLabelInfo(IntPtr pub_ptr, ref LabelInfoStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreatePointCloud2Publisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyPointCloud2Publisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishPointCloud2(IntPtr pub_ptr, ref PointCloud2Struct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateRangePublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyRangePublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishRange(IntPtr pub_ptr, ref RangeStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreatePoseStampedPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyPoseStampedPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishPoseStamped(IntPtr pub_ptr, ref PoseStampedStruct data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateContactsPublisher(IntPtr node_ptr, string topic_name, int qos_depth = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyContactsPublisher(IntPtr pub_ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PublishContacts(IntPtr pub_ptr, ref ContactsStruct data);
    }
}
