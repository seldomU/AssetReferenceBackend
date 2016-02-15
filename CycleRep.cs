using System.Collections.Generic;
using UnityEngine;
using RelationsInspector.Extensions;
using System.Linq;

namespace RelationsInspector.Backend.AssetDependency
{
	// object representing a cycle in the dependency graph
	public class CycleRep : ScriptableObject
	{
		public HashSet<Object> members; // objects that are part of the cycle
		public GameObject gameObject;

		public static CycleRep Create( IEnumerable<Object> members )
		{
			var instance = CreateInstance<CycleRep>();
			instance.members = members.ToHashSet();
			instance.gameObject = instance.GetGameObject();
			instance.name = instance.gameObject == null ? "Cycle Rep" : instance.gameObject.name;
			return instance;
		}

		public override string ToString()
		{
			return members.ToDelimitedString();
		}

		public bool EqualMembers( CycleRep other )
		{
			return other != null && other.members.SetEquals( this.members );
		}

		// returns the represented gameobject, if there is one
		// that is: if one member is a gameobject, and all others are components of it
		private GameObject GetGameObject()
		{
			var go = members.OfType<GameObject>().SingleOrDefault();
			if ( go == null )
				return null;

			foreach ( var obj in members.Except( new[] { go } ) )
			{
				var asComponent = obj as Component;
				if ( asComponent == null || asComponent.gameObject != go )
					return null;
			}

			return go;
		}
	}
}
