# Security Policy

## Supported Versions

CLOiSim has been upgraded to **Unity 6**. We provide updates and bug fixes for the current major version based on the latest simulation engine.

| Version | Supported          | Unity Version      |
| ------- | ------------------ | ------------------ |
| 5.x.x   | :white_check_mark: | Unity 6            |
| 4.x.x   | :x:                | Unity 2022.3 LTS   |
| < 4.0   | :x:                | Unity 2021 / 2020  |

> [!IMPORTANT]
> Previous Unity 2022.3 LTS based releases (4.x.x) and older versions are no longer maintained. We strongly recommend all users to upgrade to the latest version based on Unity 6 for the best performance and security.

## Reporting a Vulnerability

If you discover any security-related issues or potential vulnerabilities in CLOiSim, please report them directly through **GitHub Issues**.

### How to Report
1.  Visit the [Issues](https://github.com/lge-ros2/cloisim/issues) page of this repository.
2.  Open a new issue and label it appropriately (e.g., prefix the title with `[Security]`).
3.  Include detailed information such as:
    - Affected version and component.
    - Steps to reproduce the issue.
    - Potential impact or suggested fixes.

### ROS 2 Framework Issues
For vulnerabilities related to the core **ROS 2 framework** or its standard packages (not specific to CLOiSim's implementation), please report them to the [ROS Security Working Group](https://github.com/ros-security/community) at [security@openrobotics.org](mailto:security@openrobotics.org).

---
We value community feedback and will review reported issues as soon as possible. Thank you for helping us improve CLOiSim!
