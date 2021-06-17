/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class TF
{
	public string parentFrameId = string.Empty;
	public string childFrameId = string.Empty;
	public SDF.Helper.Link link = null;

	public TF(in SDF.Helper.Link link, in string childFrameId, in string parentFrameId = "base_link")
	{
		this.parentFrameId = parentFrameId;
		this.childFrameId = childFrameId;
		this.link = link;
	}
}