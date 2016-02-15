using UnityEngine;
using System.Collections;

namespace RelationsInspector.Backend.AssetDependency
{
	public class ObjectNode
	{
		public string label { get; private set; }
		public string tooltip { get; private set; }
		public Object[] objs { get; private set; }
		public bool IsSceneObject { get; private set; }

		public ObjectNode( string label, string tooltip, Object[] objs, bool isSceneObject )
		{
			this.label = label;
			this.tooltip = tooltip;
			this.objs = objs;
			this.IsSceneObject = isSceneObject;
		}
	}
}
