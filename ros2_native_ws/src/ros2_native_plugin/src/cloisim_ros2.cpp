#include <rclcpp/rclcpp.hpp>
#include <sensor_msgs/msg/laser_scan.hpp>
#include <sensor_msgs/msg/imu.hpp>
#include <sensor_msgs/msg/nav_sat_fix.hpp>
#include <sensor_msgs/msg/image.hpp>
#include <sensor_msgs/msg/camera_info.hpp>
#include <sensor_msgs/msg/point_cloud2.hpp>
#include <sensor_msgs/msg/range.hpp>
#include <sensor_msgs/msg/joint_state.hpp>
#include <geometry_msgs/msg/pose_stamped.hpp>
#include <nav_msgs/msg/odometry.hpp>
#include <vision_msgs/msg/label_info.hpp>
#include <ros_gz_interfaces/msg/contacts.hpp>
#include <iostream>
#include <string>
#include <vector>
#include <mutex>
#include <unordered_map>
#include <cstring>

// ═══════════════════════════════════════════════
//  QoS type enum: 0 = RELIABLE (default), 1 = BEST_EFFORT sensor
// ═══════════════════════════════════════════════
enum QosType : int {
    QOS_RELIABLE = 0,
    QOS_BEST_EFFORT = 1
};

static rclcpp::QoS make_qos(int qos_depth, int qos_type) {
    rclcpp::QoS qos(qos_depth);
    if (qos_type == QOS_BEST_EFFORT) {
        qos.best_effort();
        qos.keep_last(qos_depth > 0 ? qos_depth : 1);
        qos.durability_volatile();
    }
    return qos;
}

// ═══════════════════════════════════════════════
//  Pre-allocated message cache (P2)
//  Avoids constructing + deep-copying messages every publish call
// ═══════════════════════════════════════════════
static std::mutex g_msg_cache_mutex;
static std::unordered_map<void*, sensor_msgs::msg::Image> g_image_msg_cache;
static std::unordered_map<void*, sensor_msgs::msg::LaserScan> g_laserscan_msg_cache;
static std::unordered_map<void*, sensor_msgs::msg::PointCloud2> g_pointcloud2_msg_cache;

extern "C" {

struct LaserScanStruct {
    double timestamp;
    const char* frame_id;
    float angle_min;
    float angle_max;
    float angle_increment;
    float time_increment;
    float scan_time;
    float range_min;
    float range_max;
    float* ranges;
    int ranges_length;
    float* intensities;
    int intensities_length;
};

// Global resources
static std::shared_ptr<rclcpp::Context> g_context = nullptr;
static std::mutex g_mutex;

bool InitROS2(int argc, char** argv) {
    std::lock_guard<std::mutex> lock(g_mutex);
    if (!rclcpp::ok()) {
        rclcpp::init(argc, argv);
        g_context = rclcpp::contexts::get_global_default_context();
        return true;
    }
    return false;
}

void ShutdownROS2() {
    std::lock_guard<std::mutex> lock(g_mutex);
    if (rclcpp::ok()) {
        rclcpp::shutdown();
        g_context = nullptr;
    }
}

void* CreateNode(const char* node_name) {
    if (!rclcpp::ok()) return nullptr;
    try {
        auto node = std::make_shared<rclcpp::Node>(node_name);
        // We return a raw pointer to a new shared_ptr allocated on the heap 
        // to manage the lifetime manually via C-API.
        return new std::shared_ptr<rclcpp::Node>(node);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create node: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyNode(void* node_ptr) {
    if (node_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
        delete typed_ptr;
    }
}

void* CreateLaserScanPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::LaserScan>(topic_name, qos);
        auto ptr = new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::LaserScan>>(pub);
        g_laserscan_msg_cache[ptr] = sensor_msgs::msg::LaserScan();
        return ptr;
    } catch (const std::exception& e) {
        std::cerr << "Failed to create publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyLaserScanPublisher(void* pub_ptr) {
    if (pub_ptr) {
        g_laserscan_msg_cache.erase(pub_ptr);
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::LaserScan>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishLaserScan(void* pub_ptr, LaserScanStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::LaserScan>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    // P2: Reuse pre-allocated message
    auto& msg = g_laserscan_msg_cache[pub_ptr];
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) {
        msg.header.frame_id = data->frame_id;
    }
    
    msg.angle_min = data->angle_min;
    msg.angle_max = data->angle_max;
    msg.angle_increment = data->angle_increment;
    msg.time_increment = data->time_increment;
    msg.scan_time = data->scan_time;
    msg.range_min = data->range_min;
    msg.range_max = data->range_max;

    if (data->ranges && data->ranges_length > 0) {
        msg.ranges.resize(data->ranges_length);
        std::memcpy(msg.ranges.data(), data->ranges, data->ranges_length * sizeof(float));
    } else {
        msg.ranges.clear();
    }
    if (data->intensities && data->intensities_length > 0) {
        msg.intensities.resize(data->intensities_length);
        std::memcpy(msg.intensities.data(), data->intensities, data->intensities_length * sizeof(float));
    } else {
        msg.intensities.clear();
    }

    (*typed_pub_ptr)->publish(msg);
}

struct ImuStruct {
    double timestamp;
    const char* frame_id;
    double orientation_x;
    double orientation_y;
    double orientation_z;
    double orientation_w;
    double angular_velocity_x;
    double angular_velocity_y;
    double angular_velocity_z;
    double linear_acceleration_x;
    double linear_acceleration_y;
    double linear_acceleration_z;
};

void* CreateImuPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::Imu>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Imu>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create IMU publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyImuPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Imu>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishImu(void* pub_ptr, ImuStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Imu>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    sensor_msgs::msg::Imu msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.orientation.x = data->orientation_x;
    msg.orientation.y = data->orientation_y;
    msg.orientation.z = data->orientation_z;
    msg.orientation.w = data->orientation_w;
    
    msg.angular_velocity.x = data->angular_velocity_x;
    msg.angular_velocity.y = data->angular_velocity_y;
    msg.angular_velocity.z = data->angular_velocity_z;
    
    msg.linear_acceleration.x = data->linear_acceleration_x;
    msg.linear_acceleration.y = data->linear_acceleration_y;
    msg.linear_acceleration.z = data->linear_acceleration_z;

    (*typed_pub_ptr)->publish(msg);
}

struct OdometryStruct {
    double timestamp;
    const char* frame_id;
    const char* child_frame_id;
    double pose_x;
    double pose_y;
    double pose_z;
    double pose_orientation_x;
    double pose_orientation_y;
    double pose_orientation_z;
    double pose_orientation_w;
    double twist_linear_x;
    double twist_linear_y;
    double twist_linear_z;
    double twist_angular_x;
    double twist_angular_y;
    double twist_angular_z;
};

void* CreateOdometryPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<nav_msgs::msg::Odometry>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<nav_msgs::msg::Odometry>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create Odometry publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyOdometryPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<nav_msgs::msg::Odometry>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishOdometry(void* pub_ptr, OdometryStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<nav_msgs::msg::Odometry>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    nav_msgs::msg::Odometry msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    if (data->child_frame_id) msg.child_frame_id = data->child_frame_id;
    
    msg.pose.pose.position.x = data->pose_x;
    msg.pose.pose.position.y = data->pose_y;
    msg.pose.pose.position.z = data->pose_z;
    msg.pose.pose.orientation.x = data->pose_orientation_x;
    msg.pose.pose.orientation.y = data->pose_orientation_y;
    msg.pose.pose.orientation.z = data->pose_orientation_z;
    msg.pose.pose.orientation.w = data->pose_orientation_w;
    
    msg.twist.twist.linear.x = data->twist_linear_x;
    msg.twist.twist.linear.y = data->twist_linear_y;
    msg.twist.twist.linear.z = data->twist_linear_z;
    msg.twist.twist.angular.x = data->twist_angular_x;
    msg.twist.twist.angular.y = data->twist_angular_y;
    msg.twist.twist.angular.z = data->twist_angular_z;

    (*typed_pub_ptr)->publish(msg);
}

struct NavSatFixStruct {
    double timestamp;
    const char* frame_id;
    int8_t status;
    uint16_t service;
    double latitude;
    double longitude;
    double altitude;
    double position_covariance[9];
    uint8_t position_covariance_type;
};

void* CreateNavSatFixPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::NavSatFix>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::NavSatFix>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create NavSatFix publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyNavSatFixPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::NavSatFix>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishNavSatFix(void* pub_ptr, NavSatFixStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::NavSatFix>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    sensor_msgs::msg::NavSatFix msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.status.status = data->status;
    msg.status.service = data->service;
    msg.latitude = data->latitude;
    msg.longitude = data->longitude;
    msg.altitude = data->altitude;
    msg.position_covariance_type = data->position_covariance_type;

    for (int i = 0; i < 9; ++i) {
        msg.position_covariance[i] = data->position_covariance[i];
    }

    (*typed_pub_ptr)->publish(msg);
}

struct ImageStruct {
    double timestamp;
    const char* frame_id;
    uint32_t height;
    uint32_t width;
    const char* encoding;
    uint8_t is_bigendian;
    uint32_t step;
    uint8_t* data;
    uint32_t data_length;
};

void* CreateImagePublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::Image>(topic_name, qos);
        auto ptr = new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Image>>(pub);
        g_image_msg_cache[ptr] = sensor_msgs::msg::Image();
        return ptr;
    } catch (const std::exception& e) {
        std::cerr << "Failed to create Image publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyImagePublisher(void* pub_ptr) {
    if (pub_ptr) {
        g_image_msg_cache.erase(pub_ptr);
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Image>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishImage(void* pub_ptr, ImageStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Image>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    // P2: Reuse pre-allocated message — avoid heap alloc for data vector
    auto& msg = g_image_msg_cache[pub_ptr];
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.height = data->height;
    msg.width = data->width;
    if (data->encoding) msg.encoding = data->encoding;
    msg.is_bigendian = data->is_bigendian;
    msg.step = data->step;

    if (data->data && data->data_length > 0) {
        msg.data.resize(data->data_length);
        std::memcpy(msg.data.data(), data->data, data->data_length);
    } else {
        msg.data.clear();
    }

    (*typed_pub_ptr)->publish(msg);
}

struct CameraInfoStruct {
    double timestamp;
    const char* frame_id;
    uint32_t height;
    uint32_t width;
    const char* distortion_model;
    double* d;
    int d_length;
    double k[9];
    double r[9];
    double p[12];
    uint32_t binning_x;
    uint32_t binning_y;
};

void* CreateCameraInfoPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::CameraInfo>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::CameraInfo>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create CameraInfo publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyCameraInfoPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::CameraInfo>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishCameraInfo(void* pub_ptr, CameraInfoStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::CameraInfo>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    sensor_msgs::msg::CameraInfo msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.height = data->height;
    msg.width = data->width;
    if (data->distortion_model) msg.distortion_model = data->distortion_model;
    
    if (data->d && data->d_length > 0) {
        msg.d.assign(data->d, data->d + data->d_length);
    }

    for (int i = 0; i < 9; ++i) msg.k[i] = data->k[i];
    for (int i = 0; i < 9; ++i) msg.r[i] = data->r[i];
    for (int i = 0; i < 12; ++i) msg.p[i] = data->p[i];
    
    msg.binning_x = data->binning_x;
    msg.binning_y = data->binning_y;

    (*typed_pub_ptr)->publish(msg);
}

struct LabelInfoStruct {
    double timestamp;
    const char* frame_id;
    int* class_id;
    const char** class_name;
    int label_length;
};

void* CreateLabelInfoPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<vision_msgs::msg::LabelInfo>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<vision_msgs::msg::LabelInfo>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create LabelInfo publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyLabelInfoPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<vision_msgs::msg::LabelInfo>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishLabelInfo(void* pub_ptr, LabelInfoStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<vision_msgs::msg::LabelInfo>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    vision_msgs::msg::LabelInfo msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    if (data->class_id && data->class_name && data->label_length > 0) {
        msg.class_map.reserve(data->label_length);
        for (int i = 0; i < data->label_length; i++) {
            vision_msgs::msg::VisionClass vision_class;
            vision_class.class_id = data->class_id[i];
            vision_class.class_name = data->class_name[i];
            msg.class_map.push_back(vision_class);
        }
    }

    (*typed_pub_ptr)->publish(msg);
}

struct PointFieldStruct {
    const char* name;
    uint32_t offset;
    uint8_t datatype;
    uint32_t count;
};

struct PointCloud2Struct {
    double timestamp;
    const char* frame_id;
    uint32_t height;
    uint32_t width;
    
    PointFieldStruct* fields;
    int fields_length;
    
    uint8_t is_bigendian;
    uint32_t point_step;
    uint32_t row_step;
    
    uint8_t* data;
    uint32_t data_length;
    
    uint8_t is_dense;
};

void* CreatePointCloud2Publisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::PointCloud2>(topic_name, qos);
        auto ptr = new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::PointCloud2>>(pub);
        g_pointcloud2_msg_cache[ptr] = sensor_msgs::msg::PointCloud2();
        return ptr;
    } catch (const std::exception& e) {
        std::cerr << "Failed to create PointCloud2 publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyPointCloud2Publisher(void* pub_ptr) {
    if (pub_ptr) {
        g_pointcloud2_msg_cache.erase(pub_ptr);
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::PointCloud2>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishPointCloud2(void* pub_ptr, PointCloud2Struct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::PointCloud2>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    // P2: Reuse pre-allocated message
    auto& msg = g_pointcloud2_msg_cache[pub_ptr];
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.height = data->height;
    msg.width = data->width;
    
    if (data->fields && data->fields_length > 0) {
        msg.fields.resize(data->fields_length);
        for (int i = 0; i < data->fields_length; i++) {
            if (data->fields[i].name) msg.fields[i].name = data->fields[i].name;
            msg.fields[i].offset = data->fields[i].offset;
            msg.fields[i].datatype = data->fields[i].datatype;
            msg.fields[i].count = data->fields[i].count;
        }
    } else {
        msg.fields.clear();
    }
    
    msg.is_bigendian = data->is_bigendian;
    msg.point_step = data->point_step;
    msg.row_step = data->row_step;
    
    if (data->data && data->data_length > 0) {
        msg.data.resize(data->data_length);
        std::memcpy(msg.data.data(), data->data, data->data_length);
    } else {
        msg.data.clear();
    }
    
    msg.is_dense = data->is_dense;

    (*typed_pub_ptr)->publish(msg);
}

struct RangeStruct {
    double timestamp;
    const char* frame_id;
    uint8_t radiation_type;
    float field_of_view;
    float min_range;
    float max_range;
    float range;
};

void* CreateRangePublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::Range>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Range>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create Range publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyRangePublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Range>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishRange(void* pub_ptr, RangeStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::Range>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    sensor_msgs::msg::Range msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;
    
    msg.radiation_type = data->radiation_type;
    msg.field_of_view = data->field_of_view;
    msg.min_range = data->min_range;
    msg.max_range = data->max_range;
    msg.range = data->range;

    (*typed_pub_ptr)->publish(msg);
}

struct PoseStampedStruct {
    double timestamp;
    const char* frame_id;
    double position_x;
    double position_y;
    double position_z;
    double orientation_x;
    double orientation_y;
    double orientation_z;
    double orientation_w;
};

void* CreatePoseStampedPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<geometry_msgs::msg::PoseStamped>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<geometry_msgs::msg::PoseStamped>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create PoseStamped publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyPoseStampedPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<geometry_msgs::msg::PoseStamped>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishPoseStamped(void* pub_ptr, PoseStampedStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<geometry_msgs::msg::PoseStamped>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    geometry_msgs::msg::PoseStamped msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;

    msg.pose.position.x = data->position_x;
    msg.pose.position.y = data->position_y;
    msg.pose.position.z = data->position_z;
    msg.pose.orientation.x = data->orientation_x;
    msg.pose.orientation.y = data->orientation_y;
    msg.pose.orientation.z = data->orientation_z;
    msg.pose.orientation.w = data->orientation_w;

    (*typed_pub_ptr)->publish(msg);
}

struct Vector3dStruct {
    double x;
    double y;
    double z;
};

struct ContactStruct {
    const char* collision1;
    const char* collision2;
    Vector3dStruct* positions;
    int positions_length;
    Vector3dStruct* normals;
    int normals_length;
    double* depths;
    int depths_length;
    struct {
        double sec;
        double nsec;
    }* times;
    int times_length;
};

struct ContactsStruct {
    double timestamp;
    const char* frame_id;
    ContactStruct* contacts;
    int contacts_length;
};

void* CreateContactsPublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<ros_gz_interfaces::msg::Contacts>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<ros_gz_interfaces::msg::Contacts>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create Contacts publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyContactsPublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<ros_gz_interfaces::msg::Contacts>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishContacts(void* pub_ptr, ContactsStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<ros_gz_interfaces::msg::Contacts>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    ros_gz_interfaces::msg::Contacts msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;

    if (data->contacts && data->contacts_length > 0) {
        msg.contacts.reserve(data->contacts_length);
        for (int i = 0; i < data->contacts_length; i++) {
            ros_gz_interfaces::msg::Contact contact_msg;
            if (data->contacts[i].collision1) contact_msg.collision1.name = data->contacts[i].collision1;
            if (data->contacts[i].collision2) contact_msg.collision2.name = data->contacts[i].collision2;
            
            if (data->contacts[i].positions && data->contacts[i].positions_length > 0) {
                contact_msg.positions.reserve(data->contacts[i].positions_length);
                for (int j = 0; j < data->contacts[i].positions_length; j++) {
                    geometry_msgs::msg::Vector3 vec;
                    vec.x = data->contacts[i].positions[j].x;
                    vec.y = data->contacts[i].positions[j].y;
                    vec.z = data->contacts[i].positions[j].z;
                    contact_msg.positions.push_back(vec);
                }
            }

            if (data->contacts[i].normals && data->contacts[i].normals_length > 0) {
                contact_msg.normals.reserve(data->contacts[i].normals_length);
                for (int j = 0; j < data->contacts[i].normals_length; j++) {
                    geometry_msgs::msg::Vector3 vec;
                    vec.x = data->contacts[i].normals[j].x;
                    vec.y = data->contacts[i].normals[j].y;
                    vec.z = data->contacts[i].normals[j].z;
                    contact_msg.normals.push_back(vec);
                }
            }

            if (data->contacts[i].depths && data->contacts[i].depths_length > 0) {
                contact_msg.depths.assign(data->contacts[i].depths, data->contacts[i].depths + data->contacts[i].depths_length);
            }

            msg.contacts.push_back(contact_msg);
        }
    }

    (*typed_pub_ptr)->publish(msg);
}

// ═══════════════════════════════════════════════
//  JointState
// ═══════════════════════════════════════════════

struct JointStateStruct {
    double timestamp;
    const char* frame_id;
    const char** name;
    double* position;
    double* velocity;
    double* effort;
    int length;
};

void* CreateJointStatePublisher(void* node_ptr, const char* topic_name, int qos_depth = 10, int qos_type = QOS_BEST_EFFORT) {
    if (!node_ptr || !topic_name) return nullptr;
    auto typed_node_ptr = static_cast<std::shared_ptr<rclcpp::Node>*>(node_ptr);
    if (!typed_node_ptr || !*typed_node_ptr) return nullptr;

    try {
        auto qos = make_qos(qos_depth, qos_type);
        auto pub = (*typed_node_ptr)->create_publisher<sensor_msgs::msg::JointState>(topic_name, qos);
        return new std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::JointState>>(pub);
    } catch (const std::exception& e) {
        std::cerr << "Failed to create JointState publisher: " << e.what() << std::endl;
        return nullptr;
    }
}

void DestroyJointStatePublisher(void* pub_ptr) {
    if (pub_ptr) {
        auto typed_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::JointState>>*>(pub_ptr);
        delete typed_ptr;
    }
}

void PublishJointState(void* pub_ptr, JointStateStruct* data) {
    if (!pub_ptr || !data) return;
    auto typed_pub_ptr = static_cast<std::shared_ptr<rclcpp::Publisher<sensor_msgs::msg::JointState>>*>(pub_ptr);
    if (!typed_pub_ptr || !*typed_pub_ptr) return;

    sensor_msgs::msg::JointState msg;
    msg.header.stamp = rclcpp::Time(static_cast<int64_t>(data->timestamp * 1e9));
    if (data->frame_id) msg.header.frame_id = data->frame_id;

    if (data->length > 0) {
        msg.name.resize(data->length);
        msg.position.resize(data->length);
        msg.velocity.resize(data->length);
        msg.effort.resize(data->length);

        for (int i = 0; i < data->length; i++) {
            if (data->name && data->name[i]) msg.name[i] = data->name[i];
            if (data->position) msg.position[i] = data->position[i];
            if (data->velocity) msg.velocity[i] = data->velocity[i];
            if (data->effort) msg.effort[i] = data->effort[i];
        }
    }

    (*typed_pub_ptr)->publish(msg);
}

} // extern "C"
