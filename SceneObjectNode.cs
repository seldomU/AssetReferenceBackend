using UnityEngine;
using System.Collections;

namespace RelationsInspector.Backend.AssetReferenceInspector
{
	public class SceneObjectNode
	{
		public string label { get; private set; }

		public SceneObjectNode(string label) 
		{ 
			this.label = label; 
		}
	}
}
