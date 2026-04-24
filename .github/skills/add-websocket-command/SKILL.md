---
name: add-websocket-command
description: "Add a new WebSocket command to the simulation control service. Use when: adding a new runtime control command, exposing simulation state via WebSocket, adding a new service endpoint."
---

# Add a New WebSocket Command

Procedure for adding a new command to the `SimulationControlService` or creating a new WebSocket service endpoint.

## When to Use

- Adding a new runtime control command (e.g., `pause`, `step`, `teleport`)
- Exposing new simulation state via the WebSocket API
- Creating an entirely new WebSocket service endpoint

## Option A: Add Command to Existing Service

### 1. Define Request/Response DTOs

Edit `Assets/Scripts/Core/Services/SimulationControlService.cs`:

```csharp
// If the response needs a custom shape, add a new response class:
private class SimulationControlResponseMyData : SimulationControlResponseBase
{
	[JsonProperty("result")]
	public MyDataType result;
}
```

Standard responses use `SimulationControlResponseNormal` (has `string result`).

### 2. Add the Command Case

In the `OnMessage()` method's switch statement:

```csharp
case "my_new_command":
	// Simple response:
	output = new SimulationControlResponseNormal();
	(output as SimulationControlResponseNormal).result = ComputeMyResult();
	break;

	// Or with custom data:
	output = new SimulationControlResponseMyData();
	(output as SimulationControlResponseMyData).result = GetMyData();
	break;
```

The response is automatically serialized and sent:
```csharp
output.command = request.command;
Send(JsonConvert.SerializeObject(output));
```

### 3. Wire to Main.cs (If Needed)

If the command needs access to `Main` instance methods:

```csharp
// In SimulationControlService, access Main via the singleton:
case "my_command":
	Main.Instance.MyNewMethod();
	output = new SimulationControlResponseNormal();
	(output as SimulationControlResponseNormal).result = SimulationService.SUCCESS;
	break;
```

Constants: `SimulationService.SUCCESS = "ok"`, `SimulationService.FAIL = "fail"`.

For multi-part request data, use `SimulationService.Delimiter = "!%!"` to split values.

## Option B: Create a New Service Endpoint

### 1. Create the Service Class

Create `Assets/Scripts/Core/Services/MyNewService.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

public class MyNewService : WebSocketBehavior
{
	// Inject dependencies via public properties (set by factory lambda)
	public MyDependency Dependency { get; set; }

	protected override void OnMessage(MessageEventArgs e)
	{
		try
		{
			var request = JsonConvert.DeserializeObject<MyRequest>(e.Data);
			var response = ProcessRequest(request);
			Send(JsonConvert.SerializeObject(response));
		}
		catch (Exception ex)
		{
			var error = new { error = ex.Message };
			Send(JsonConvert.SerializeObject(error));
		}
	}

	protected override void OnOpen()
	{
		// Connection opened
	}

	protected override void OnClose(CloseEventArgs e)
	{
		// Connection closed
	}

	private MyResponse ProcessRequest(MyRequest request)
	{
		// Handle the request
		return new MyResponse();
	}
}
```

### 2. Register the Endpoint

Edit `Assets/Scripts/Core/Modules/SimulationService.cs`, in `InitializeServices()`:

```csharp
_wsServer.AddWebSocketService<MyNewService>("/my_endpoint", () =>
	new MyNewService()
	{
		Dependency = myDependencyInstance
	});
```

### 3. Clean Up on Dispose

In `SimulationService.Dispose()`, add removal:

```csharp
_wsServer.RemoveWebSocketService("/my_endpoint");
```

## JSON Patterns

Request format (convention from existing services):
```json
{
    "command": "my_command",
    "value": "optional_parameter"
}
```

Response format:
```json
{
    "command": "my_command",
    "result": "ok"
}
```

Use `[JsonProperty("field_name")]` attributes on DTO properties for explicit JSON field naming.

## Checklist

- [ ] Request/response DTOs defined with `[JsonProperty]` attributes
- [ ] Command case added to `OnMessage()` switch
- [ ] Uses `SimulationService.SUCCESS` / `FAIL` constants for status
- [ ] Error handling wraps the command logic
- [ ] New endpoints registered in `SimulationService.InitializeServices()`
- [ ] New endpoints removed in `SimulationService.Dispose()`
- [ ] License header on new files
