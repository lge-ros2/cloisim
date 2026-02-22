import rclpy
from rclpy.node import Node
from sensor_msgs.msg import LaserScan, Imu, Image, PointCloud2
from nav_msgs.msg import Odometry
from vision_msgs.msg import LabelInfo

class CloiSimTestNode(Node):
    def __init__(self):
        super().__init__('cloisim_test_node')
        
        # Original Subscriptions
        self.create_subscription(Imu, '/imu', self.imu_callback, 10)
        self.create_subscription(LaserScan, '/scan', self.scan_callback, 10)
        self.create_subscription(Odometry, '/odom', self.odom_callback, 10)
        
        # New Advanced Subscriptions
        self.create_subscription(PointCloud2, '/scan_3d', self.pc2_callback, 10)
        self.create_subscription(Image, '/camera/image_raw', self.camera_callback, 10)
        self.create_subscription(Image, '/depth_camera/image_raw', self.depth_callback, 10)
        self.create_subscription(LabelInfo, '/camera/segmentation/label_info', self.segmentation_callback, 10)

        self.get_logger().info('CLOiSim Native Plugin Advanced Test Node started!')
        self.get_logger().info('Listening to: IMU, Scan, Odom, PointCloud2, Camera, Depth, Segmentation...')

        # Message counters
        self.counts = {
            'imu': 0, 'scan': 0, 'odom': 0,
            'pc2': 0, 'camera': 0, 'depth': 0, 'segmentation': 0
        }

    def imu_callback(self, msg):
        self.counts['imu'] += 1
        if self.counts['imu'] % 100 == 0:
            self.get_logger().info(f'[IMU] q.w={msg.orientation.w:.2f}')

    def scan_callback(self, msg):
        self.counts['scan'] += 1
        if self.counts['scan'] % 50 == 0:
            self.get_logger().info(f'[LaserScan] ranges count={len(msg.ranges)}')

    def odom_callback(self, msg):
        self.counts['odom'] += 1
        if self.counts['odom'] % 50 == 0:
            self.get_logger().info(f'[Odometry] x={msg.pose.pose.position.x:.2f}, y={msg.pose.pose.position.y:.2f}')

    def pc2_callback(self, msg):
        self.counts['pc2'] += 1
        if self.counts['pc2'] % 50 == 0:
            self.get_logger().info(f'[PointCloud2] width={msg.width}, height={msg.height}, row_step={msg.row_step}')

    def camera_callback(self, msg):
        self.counts['camera'] += 1
        if self.counts['camera'] % 30 == 0:
            self.get_logger().info(f'[Camera] Frame {msg.width}x{msg.height} ({msg.encoding})')

    def depth_callback(self, msg):
        self.counts['depth'] += 1
        if self.counts['depth'] % 30 == 0:
            self.get_logger().info(f'[Depth Camera] Frame {msg.width}x{msg.height} ({msg.encoding})')

    def segmentation_callback(self, msg):
        self.counts['segmentation'] += 1
        if self.counts['segmentation'] % 30 == 0:
            self.get_logger().info(f'[Segmentation] Class maps received: {len(msg.class_map)}')

def main(args=None):
    rclpy.init(args=args)
    node = CloiSimTestNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()
