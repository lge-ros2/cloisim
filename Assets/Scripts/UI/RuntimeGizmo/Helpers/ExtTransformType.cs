using System;

namespace RuntimeGizmos
{
	public static class ExtTransformType
	{
		public static bool TransformTypeContains(this TransformType mainType, TransformType type, TransformSpace space)
		{
			if (type == mainType)
			{
				return true;
			}

			if (mainType == TransformType.All)
			{
				if (type == TransformType.Move)
				{
					return true;
				}
				else if (type == TransformType.Rotate)
				{
					return true;
				}
			}

			return false;
		}
	}
}
