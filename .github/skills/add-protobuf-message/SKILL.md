---
name: add-protobuf-message
description: "Add a new protobuf message type for CLOiSim transport. Use when: defining a new sensor data format, adding a new command message, extending the transport protocol."
---

# Add a New Protobuf Message

Procedure for adding a new protobuf message type to CLOiSim's NetMQ transport layer.

## When to Use

- A new sensor device needs a custom message format
- An existing message type doesn't capture the required data
- Extending the transport protocol for a new feature

## Procedure

### 1. Create the .proto Definition

Add a `.proto` file to the external `cloisim_ros_protobuf_msgs/msgs/` repository:

```protobuf
syntax = "proto3";
package cloisim.msgs;

import "time.proto";
import "vector3d.proto";

message MyNewMessage {
  Time stamp = 1;
  string entity_name = 2;
  Vector3d value = 3;
  double scalar = 4;
  repeated double data_array = 5;
}
```

**Convention rules:**
- Package: `cloisim.msgs`
- Field naming: `snake_case`
- Import shared types: `time.proto`, `vector3d.proto`, `quaternion.proto`, `pose.proto`, etc.
- Use `repeated` for arrays

### 2. Add to Generation Script

Edit `Assets/Scripts/CLOiSimPlugins/Messages/.gen_proto_code.sh`:

Add the new message name to the `MSG` variable (space-delimited list):

```bash
MSG="... existing_messages my_new_message"
```

### 3. Run the Code Generator

```bash
cd Assets/Scripts/CLOiSimPlugins/Messages/
bash .gen_proto_code.sh
```

This runs `protogen` (protobuf-net code generator) and outputs C# classes into the `Messages/` directory.

### 4. Verify the Generated Class

The generated file (`MyNewMessage.cs`) should contain:

```csharp
namespace cloisim.msgs
{
    [ProtoContract]
    public partial class MyNewMessage
    {
        [ProtoMember(1)]
        public Time Stamp { get; set; }

        [ProtoMember(2)]
        public string EntityName { get; set; }

        // ... etc
    }
}
```

The class is `partial` — you can extend it in a separate file if needed.

### 5. Use in Device Code

```csharp
using messages = cloisim.msgs;

// In InitializeMessages():
_msg = new messages.MyNewMessage();
_msg.Stamp = new messages.Time();
_msg.Value = new messages.Vector3d();
// Always allocate nested sub-messages explicitly

// In GenerateMessage():
_msg.Value.Set(someVector);
_msg.Scalar = someValue;
_msg.Stamp.Set(GetNextSyntheticTime());
PushDeviceMessage<messages.MyNewMessage>(_msg);
```

### 6. Add Fast Path (For High-Bandwidth Sensors)

If the message is used for high-bandwidth binary data (images, point clouds), add a fast path in `Device.PushDeviceMessage<T>()` to bypass protobuf serialization:

```csharp
// In Device.cs PushDeviceMessage:
if (message is messages.MyNewMessage myMsg)
{
    deviceMessage.SetRawMyData(myMsg);  // Custom binary format
}
```

This is only needed for messages sent at high frequency where protobuf overhead matters. Most sensors use standard protobuf serialization.

### 7. Helper Extension Methods

Common message types have `.Set()` extension methods for Unity types:

```csharp
// Vector3d.Set(UnityEngine.Vector3)
_msg.Value.Set(transform.position);

// Quaternion.Set(UnityEngine.Quaternion)
_msg.Orientation.Set(transform.rotation);

// Time.Set(double simulationTime)
_msg.Stamp.Set(GetNextSyntheticTime());
```

If your message uses existing sub-message types, these helpers are already available. For new sub-types, add extension methods in `Messages/` following the existing pattern.

## Checklist

- [ ] `.proto` file created in external proto repository
- [ ] Message name added to `.gen_proto_code.sh` `MSG` variable
- [ ] Code generator run successfully
- [ ] Generated C# class is in `cloisim.msgs` namespace
- [ ] All nested sub-messages allocated explicitly in `InitializeMessages()`
- [ ] Uses `messages = cloisim.msgs` namespace alias
- [ ] Fast path added (only if high-bandwidth)
