"""
Launch benchmark node for ZMQ legacy communication mode.

Pre-requisites:
  1. CLOiSim simulator running with BenchmarkBot world
  2. cloisim_ros bridge running:
       ros2 launch cloisim_ros_bringup bringup.launch.py

Usage:
  ros2 launch cloisim_test_app benchmark_zmq.launch.py
  ros2 launch cloisim_test_app benchmark_zmq.launch.py robot_name:=BenchmarkBot duration:=60
"""

from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription([
        DeclareLaunchArgument('robot_name', default_value='BenchmarkBot'),
        DeclareLaunchArgument('duration',   default_value='30'),
        DeclareLaunchArgument('warmup',     default_value='5'),
        DeclareLaunchArgument('output_dir',
                              default_value='benchmark_results'),

        Node(
            package='cloisim_test_app',
            executable='comm_benchmark',
            name='comm_benchmark_zmq',
            output='screen',
            parameters=[{
                'mode':       'zmq',
                'robot_name': LaunchConfiguration('robot_name'),
                'duration':   LaunchConfiguration('duration'),
                'warmup':     LaunchConfiguration('warmup'),
                'output_dir': LaunchConfiguration('output_dir'),
            }],
        ),
    ])
