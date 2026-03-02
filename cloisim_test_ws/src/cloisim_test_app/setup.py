import os
from glob import glob

from setuptools import find_packages, setup

package_name = 'cloisim_test_app'

setup(
    name=package_name,
    version='0.1.0',
    packages=find_packages(exclude=['test']),
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
        (os.path.join('share', package_name, 'launch'),
            glob(os.path.join('launch', '*.launch.py'))),
        (os.path.join('share', package_name, 'scripts'),
            glob(os.path.join('scripts', '*'))),
    ],
    install_requires=['setuptools', 'psutil'],
    zip_safe=True,
    maintainer='nav',
    maintainer_email='jamie.lee@lge.com',
    description='CLOiSim communication benchmark and test tools',
    license='MIT',
    extras_require={
        'test': [
            'pytest',
        ],
    },
    entry_points={
        'console_scripts': [
            'test_node = cloisim_test_app.cloisim_test_node:main',
            'comm_benchmark = cloisim_test_app.comm_benchmark_node:main',
            'benchmark_compare = cloisim_test_app.benchmark_compare:main',
        ],
    },
)
